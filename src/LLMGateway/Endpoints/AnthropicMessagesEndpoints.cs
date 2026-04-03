using System.Text.Json;
using LLMGateway.Models.Anthropic;
using LLMGateway.Models.OpenAI;
using LLMGateway.Services;

namespace LLMGateway.Endpoints;

public static class AnthropicMessagesEndpoints
{
    public static WebApplication MapAnthropicMessagesEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/messages", async (
            HttpContext context,
            IProviderRouter router,
            ProxyService proxy,
            CancellationToken cancellationToken) =>
        {
            // 1. Deserialize Anthropic request
            AnthropicMessagesRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.AnthropicMessagesRequest,
                    cancellationToken);
            }
            catch (JsonException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    new AnthropicErrorResponse
                    {
                        Error = new AnthropicErrorDetail
                        {
                            ErrorType = "invalid_request_error",
                            Message = "Invalid JSON in request body."
                        }
                    },
                    AppJsonSerializerContext.Default.AnthropicErrorResponse,
                    cancellationToken: cancellationToken);
                return;
            }

            // 2. Validate required fields
            if (request is null || string.IsNullOrWhiteSpace(request.Model))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    new AnthropicErrorResponse
                    {
                        Error = new AnthropicErrorDetail
                        {
                            ErrorType = "invalid_request_error",
                            Message = "Request body must include a 'model' field."
                        }
                    },
                    AppJsonSerializerContext.Default.AnthropicErrorResponse,
                    cancellationToken: cancellationToken);
                return;
            }

            // 3. Route to provider
            var provider = await router.GetProviderAsync(request.Model, cancellationToken);
            if (provider is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(
                    new AnthropicErrorResponse
                    {
                        Error = new AnthropicErrorDetail
                        {
                            ErrorType = "not_found_error",
                            Message = $"Model '{request.Model}' is not configured in the gateway."
                        }
                    },
                    AppJsonSerializerContext.Default.AnthropicErrorResponse,
                    cancellationToken: cancellationToken);
                return;
            }

            // 4. Convert Anthropic -> OpenAI
            var openAIRequest = AnthropicOpenAIConverter.ToOpenAI(request);

            // 5. Send upstream and handle response
            var isStreaming = request.Stream == true;

            if (isStreaming)
            {
                await HandleStreamingAsync(context, proxy, provider, openAIRequest, request.Model, cancellationToken);
            }
            else
            {
                await HandleNonStreamingAsync(context, proxy, provider, openAIRequest, request.Model, cancellationToken);
            }
        });

        return app;
    }

    private static async Task HandleNonStreamingAsync(
        HttpContext context,
        ProxyService proxy,
        Configuration.ProviderOptions provider,
        ChatCompletionRequest openAIRequest,
        string originalModel,
        CancellationToken ct)
    {
        using var upstreamResponse = await proxy.SendUpstreamAsync(
            provider.BaseUrl, provider.ApiKey, openAIRequest, ct);

        if (!upstreamResponse.IsSuccessStatusCode)
        {
            await WriteUpstreamErrorAsync(context, upstreamResponse, ct);
            return;
        }

        var responseBody = await upstreamResponse.Content.ReadAsStringAsync(ct);
        var openAIResponse = JsonSerializer.Deserialize(
            responseBody, AppJsonSerializerContext.Default.ChatCompletionResponse);

        if (openAIResponse is null)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(
                new AnthropicErrorResponse
                {
                    Error = new AnthropicErrorDetail
                    {
                        ErrorType = "api_error",
                        Message = "Upstream returned an invalid response."
                    }
                },
                AppJsonSerializerContext.Default.AnthropicErrorResponse,
                cancellationToken: ct);
            return;
        }

        var anthropicResponse = AnthropicOpenAIConverter.ToAnthropicResponse(openAIResponse, originalModel);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            anthropicResponse,
            AppJsonSerializerContext.Default.AnthropicMessagesResponse,
            cancellationToken: ct);
    }

    private static async Task HandleStreamingAsync(
        HttpContext context,
        ProxyService proxy,
        Configuration.ProviderOptions provider,
        ChatCompletionRequest openAIRequest,
        string originalModel,
        CancellationToken ct)
    {
        using var upstreamResponse = await proxy.SendUpstreamAsync(
            provider.BaseUrl, provider.ApiKey, openAIRequest, ct);

        if (!upstreamResponse.IsSuccessStatusCode)
        {
            await WriteUpstreamErrorAsync(context, upstreamResponse, ct);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";

        var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(ct);
        var requestId = Guid.NewGuid().ToString("N")[..24];

        await AnthropicSseConverter.ConvertStreamAsync(
            upstreamStream,
            context.Response.Body,
            originalModel,
            requestId,
            ct);
    }

    private static async Task WriteUpstreamErrorAsync(
        HttpContext context,
        HttpResponseMessage upstreamResponse,
        CancellationToken ct)
    {
        var body = await upstreamResponse.Content.ReadAsStringAsync(ct);
        context.Response.StatusCode = (int)upstreamResponse.StatusCode;

        // Try to parse as OpenAI error and convert to Anthropic format
        try
        {
            var openAIError = JsonSerializer.Deserialize(body, AppJsonSerializerContext.Default.ErrorResponse);
            if (openAIError?.Error != null)
            {
                await context.Response.WriteAsJsonAsync(
                    new AnthropicErrorResponse
                    {
                        Error = new AnthropicErrorDetail
                        {
                            ErrorType = MapErrorType(upstreamResponse.StatusCode),
                            Message = openAIError.Error.Message
                        }
                    },
                    AppJsonSerializerContext.Default.AnthropicErrorResponse,
                    cancellationToken: ct);
                return;
            }
        }
        catch { /* fall through to passthrough */ }

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(body, ct);
    }

    private static string MapErrorType(System.Net.HttpStatusCode statusCode) => statusCode switch
    {
        System.Net.HttpStatusCode.BadRequest => "invalid_request_error",
        System.Net.HttpStatusCode.Unauthorized => "authentication_error",
        System.Net.HttpStatusCode.Forbidden => "authentication_error",
        System.Net.HttpStatusCode.NotFound => "not_found_error",
        System.Net.HttpStatusCode.TooManyRequests => "rate_limit_error",
        System.Net.HttpStatusCode.InternalServerError => "api_error",
        System.Net.HttpStatusCode.ServiceUnavailable => "overloaded_error",
        _ => "api_error"
    };
}
