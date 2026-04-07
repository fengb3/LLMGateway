using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LLMGateway.Models.OpenAI;
using LLMGateway.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LLMGateway.Tests.Services;

public class ProxyServiceTests
{
    private readonly Mock<ILogger<ProxyService>> _mockLogger = new();

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"test\"}", Encoding.UTF8, "application/json")
        };
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public string? LastRequestContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(ct);
                LastRequestContentType = request.Content.Headers.ContentType?.MediaType;
            }
            return Response;
        }
    }

    private (ProxyService service, MockHttpMessageHandler handler) CreateService(bool streaming = false)
    {
        var handler = new MockHttpMessageHandler();
        var client = new HttpClient(handler);

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var service = new ProxyService(mockFactory.Object, _mockLogger.Object);
        return (service, handler);
    }

    [Fact]
    public async Task SendUpstreamAsync_SendsToCorrectUrl_WithBearerToken()
    {
        var (service, handler) = CreateService();
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = [new ChatMessage { Role = "user", Content = JsonDocument.Parse("\"Hello\"").RootElement.Clone() }]
        };

        await service.SendUpstreamAsync("https://api.openai.com", "sk-test-key", request, CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://api.openai.com/chat/completions");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("sk-test-key");
    }

    [Fact]
    public async Task SendUpstreamAsync_SerializesBodyAsJson()
    {
        var (service, handler) = CreateService();
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = [new ChatMessage { Role = "user", Content = JsonDocument.Parse("\"Hi\"").RootElement.Clone() }]
        };

        await service.SendUpstreamAsync("https://api.example.com", "key", request, CancellationToken.None);

        handler.LastRequestBody.Should().Contain("gpt-4o");
        handler.LastRequestContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task ProxyChatCompletionAsync_NonStreaming_PipesResponse()
    {
        var (service, handler) = CreateService();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"chatcmpl-1\",\"choices\":[]}", Encoding.UTF8, "application/json")
        };

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Stream = false,
            Messages = [new ChatMessage { Role = "user", Content = JsonDocument.Parse("\"Hi\"").RootElement.Clone() }]
        };

        await service.ProxyChatCompletionAsync(context, "https://api.example.com", "key", request, CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task ProxyChatCompletionAsync_UpstreamError_ForwardsStatusCode()
    {
        var (service, handler) = CreateService();
        handler.Response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"error\":\"rate limited\"}", Encoding.UTF8, "application/json")
        };

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = [new ChatMessage { Role = "user", Content = JsonDocument.Parse("\"Hi\"").RootElement.Clone() }]
        };

        await service.ProxyChatCompletionAsync(context, "https://api.example.com", "key", request, CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task ProxyChatCompletionAsync_Streaming_SetsSseHeaders()
    {
        var (service, handler) = CreateService();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("data: test\n\n")))
        };
        handler.Response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Stream = true,
            Messages = [new ChatMessage { Role = "user", Content = JsonDocument.Parse("\"Hi\"").RootElement.Clone() }]
        };

        await service.ProxyChatCompletionAsync(context, "https://api.example.com", "key", request, CancellationToken.None);

        context.Response.ContentType.Should().Be("text/event-stream");
    }
}
