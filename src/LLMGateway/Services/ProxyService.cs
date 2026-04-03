using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LLMGateway.Models.OpenAI;

namespace LLMGateway.Services;

/// <summary>
/// Forwards OpenAI-compatible requests to the configured upstream provider.
/// Handles both streaming (SSE) and non-streaming responses.
/// </summary>
public class ProxyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProxyService> _logger;

    public ProxyService(IHttpClientFactory httpClientFactory, ILogger<ProxyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Proxies a chat completion request to the upstream provider.
    /// </summary>
    public async Task ProxyChatCompletionAsync(
        HttpContext context,
        string upstreamBaseUrl,
        string upstreamApiKey,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var targetUrl = upstreamBaseUrl.TrimEnd('/') + "/chat/completions";
        var isStreaming = request.Stream == true;

        // Use a dedicated client with infinite timeout for SSE streams so long-running
        // responses are not aborted mid-stream. Non-streaming requests use a capped timeout.
        var client = _httpClientFactory.CreateClient(isStreaming ? "upstream-streaming" : "upstream");

        var jsonBody = JsonSerializer.Serialize(request, AppJsonSerializerContext.Default.ChatCompletionRequest);
        using var upstreamRequest = new HttpRequestMessage(HttpMethod.Post, targetUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        upstreamRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", upstreamApiKey);

        _logger.LogInformation("Routing model '{Model}' → {Url}", request.Model, targetUrl);

        if (isStreaming)
        {
            await ProxyStreamingResponseAsync(context, client, upstreamRequest, cancellationToken);
        }
        else
        {
            await ProxyNonStreamingResponseAsync(context, client, upstreamRequest, cancellationToken);
        }
    }

    private static async Task ProxyNonStreamingResponseAsync(
        HttpContext context,
        HttpClient client,
        HttpRequestMessage upstreamRequest,
        CancellationToken cancellationToken)
    {
        using var upstreamResponse = await client.SendAsync(upstreamRequest, cancellationToken);

        context.Response.StatusCode = (int)upstreamResponse.StatusCode;
        context.Response.ContentType = "application/json";

        var responseBody = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);
        await context.Response.WriteAsync(responseBody, cancellationToken);
    }

    private static async Task ProxyStreamingResponseAsync(
        HttpContext context,
        HttpClient client,
        HttpRequestMessage upstreamRequest,
        CancellationToken cancellationToken)
    {
        using var upstreamResponse = await client.SendAsync(
            upstreamRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        context.Response.StatusCode = (int)upstreamResponse.StatusCode;

        // Only force SSE headers when the upstream is actually streaming; for upstream errors
        // (e.g. 4xx/5xx returning JSON) pass through the upstream content type so clients
        // can parse the error payload normally.
        var upstreamContentType = upstreamResponse.Content.Headers.ContentType?.MediaType;
        if (upstreamContentType == "text/event-stream")
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
        }
        else
        {
            context.Response.ContentType = upstreamContentType ?? "application/json";
        }

        await using var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
        await upstreamStream.CopyToAsync(context.Response.Body, cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Sends a ChatCompletion request upstream and returns the raw HTTP response
    /// without piping it to the client. Used when the caller needs to transform
    /// the response before sending it (e.g., Anthropic format conversion).
    /// </summary>
    public async Task<HttpResponseMessage> SendUpstreamAsync(
        string upstreamBaseUrl,
        string upstreamApiKey,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var targetUrl = upstreamBaseUrl.TrimEnd('/') + "/chat/completions";
        var isStreaming = request.Stream == true;
        var client = _httpClientFactory.CreateClient(isStreaming ? "upstream-streaming" : "upstream");

        var jsonBody = JsonSerializer.Serialize(request, AppJsonSerializerContext.Default.ChatCompletionRequest);
        using var upstreamRequest = new HttpRequestMessage(HttpMethod.Post, targetUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        upstreamRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", upstreamApiKey);

        _logger.LogInformation("Routing model '{Model}' → {Url}", request.Model, targetUrl);

        return await client.SendAsync(
            upstreamRequest,
            isStreaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
            cancellationToken);
    }
}
