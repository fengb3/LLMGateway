using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using LLMGateway.Configuration;
using LLMGateway.Data.Entities;
using LLMGateway.Data.Repositories;
using LLMGateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LLMGateway.Tests.Middleware;

public class ApiKeyMiddlewareTests
{
    private const string AdminKey = "sk-admin-test";
    private const string UserKey = "sk-gw-user123";
    private static readonly string UserKeyHash = Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(UserKey)));

    private readonly Mock<IOptionsSnapshot<GatewayOptions>> _mockOptions;
    private readonly Mock<IApiKeyRepository> _mockRepo;

    public ApiKeyMiddlewareTests()
    {
        _mockOptions = new Mock<IOptionsSnapshot<GatewayOptions>>();
        _mockOptions.Setup(o => o.Value).Returns(new GatewayOptions
        {
            AdminApiKeys =
            [
                new AdminApiKeyEntry { Key = AdminKey, Name = "Test Admin", IsActive = true }
            ]
        });

        _mockRepo = new Mock<IApiKeyRepository>();
    }

    private ApiKeyMiddleware CreateMiddleware(RequestDelegate next)
        => new(next);

    private DefaultHttpContext CreateHttpContext(string path, string? bearerToken = null, string? xApiKey = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (bearerToken != null)
            context.Request.Headers.Authorization = $"Bearer {bearerToken}";
        if (xApiKey != null)
            context.Request.Headers["x-api-key"] = xApiKey;

        return context;
    }

    [Fact]
    public async Task HealthEndpoint_BypassesAuth()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateHttpContext("/health");

        await middleware.InvokeAsync(context, _mockOptions.Object, _mockRepo.Object);

        called.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task AdminRoute_ValidBearerKey_Passes()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateHttpContext("/admin/providers", bearerToken: AdminKey);

        await middleware.InvokeAsync(context, _mockOptions.Object, _mockRepo.Object);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task AdminRoute_InvalidKey_Returns401()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("/admin/providers", bearerToken: "wrong-key");

        await middleware.InvokeAsync(context, _mockOptions.Object, _mockRepo.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task AdminRoute_InactiveKey_Returns401()
    {
        var mockOptions = new Mock<IOptionsSnapshot<GatewayOptions>>();
        mockOptions.Setup(o => o.Value).Returns(new GatewayOptions
        {
            AdminApiKeys = [new AdminApiKeyEntry { Key = AdminKey, Name = "Inactive", IsActive = false }]
        });

        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("/admin/providers", bearerToken: AdminKey);

        await middleware.InvokeAsync(context, mockOptions.Object, _mockRepo.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task AdminRoute_XApiKeyHeader_Passes()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateHttpContext("/admin/providers", xApiKey: AdminKey);

        await middleware.InvokeAsync(context, _mockOptions.Object, _mockRepo.Object);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task UserRoute_ValidKey_Passes()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateHttpContext("/v1/chat/completions", bearerToken: UserKey);

        _mockRepo.Setup(r => r.GetByKeyHashAsync(UserKeyHash, default))
            .ReturnsAsync(new ApiKeyEntity { Id = 1, KeyHash = UserKeyHash, IsActive = true });

        await middleware.InvokeAsync(context, _mockOptions.Object, _mockRepo.Object);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task UserRoute_InvalidKey_Returns401()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("/v1/chat/completions", bearerToken: "invalid-key");

        _mockRepo.Setup(r => r.GetByKeyHashAsync(It.IsAny<string>(), default))
            .ReturnsAsync((ApiKeyEntity?)null);

        await middleware.InvokeAsync(context, _mockOptions.Object, _mockRepo.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task UserRoute_ExpiredKey_Returns401()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("/v1/chat/completions", bearerToken: UserKey);

        _mockRepo.Setup(r => r.GetByKeyHashAsync(UserKeyHash, default))
            .ReturnsAsync(new ApiKeyEntity
            {
                Id = 1,
                KeyHash = UserKeyHash,
                IsActive = true,
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            });

        await middleware.InvokeAsync(context, _mockOptions.Object, _mockRepo.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task UserRoute_InactiveKey_Returns401()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("/v1/chat/completions", bearerToken: UserKey);

        _mockRepo.Setup(r => r.GetByKeyHashAsync(UserKeyHash, default))
            .ReturnsAsync(new ApiKeyEntity { Id = 1, KeyHash = UserKeyHash, IsActive = false });

        await middleware.InvokeAsync(context, _mockOptions.Object, _mockRepo.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task MissingHeaders_Returns401WithMissingApiKey()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("/v1/chat/completions");

        await middleware.InvokeAsync(context, _mockOptions.Object, _mockRepo.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.Should().Contain("missing_api_key");
    }
}
