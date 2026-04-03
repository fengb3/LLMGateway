using LLMGateway.Configuration;
using LLMGateway.Middleware;
using LLMGateway.Models;
using LLMGateway.Models.OpenAI;
using LLMGateway.Services;
using System.Text.Json;

namespace LLMGateway;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        // AOT-compatible JSON serialization
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        // Gateway configuration
        builder.Services.Configure<GatewayOptions>(
            builder.Configuration.GetSection(GatewayOptions.SectionName));

        // HTTP client for proxying upstream requests
        builder.Services.AddHttpClient("upstream")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });

        // Gateway services
        builder.Services.AddScoped<IProviderRouter, ProviderRouter>();
        builder.Services.AddScoped<ProxyService>();

        var app = builder.Build();

        // API key authentication for all non-health routes
        app.UseMiddleware<ApiKeyMiddleware>();

        // ── Health ──────────────────────────────────────────────────────────
        app.MapGet("/health", () => TypedResults.Ok(new HealthResponse()));

        // ── Models list ─────────────────────────────────────────────────────
        app.MapGet("/v1/models", (IProviderRouter router) =>
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var models = router.GetAllModels()
                .Select(m => new ModelInfo
                {
                    Id = m.ModelName,
                    OwnedBy = m.ProviderName,
                    Created = now
                })
                .ToList();

            return Results.Ok(new ModelListResponse { Data = models });
        });

        // ── Chat Completions ─────────────────────────────────────────────────
        app.MapPost("/v1/chat/completions", async (
            HttpContext context,
            IProviderRouter router,
            ProxyService proxy,
            CancellationToken cancellationToken) =>
        {
            ChatCompletionRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.ChatCompletionRequest,
                    cancellationToken);
            }
            catch (JsonException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse
                    {
                        Error = new ErrorDetail
                        {
                            Message = "Invalid JSON in request body.",
                            Type = "invalid_request_error",
                            Code = "invalid_json"
                        }
                    },
                    AppJsonSerializerContext.Default.ErrorResponse,
                    cancellationToken: cancellationToken);
                return;
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Model))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse
                    {
                        Error = new ErrorDetail
                        {
                            Message = "Request body must include a 'model' field.",
                            Type = "invalid_request_error",
                            Code = "missing_model"
                        }
                    },
                    AppJsonSerializerContext.Default.ErrorResponse,
                    cancellationToken: cancellationToken);
                return;
            }

            var provider = router.GetProvider(request.Model);
            if (provider is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse
                    {
                        Error = new ErrorDetail
                        {
                            Message = $"Model '{request.Model}' is not configured in the gateway.",
                            Type = "invalid_request_error",
                            Code = "model_not_found"
                        }
                    },
                    AppJsonSerializerContext.Default.ErrorResponse,
                    cancellationToken: cancellationToken);
                return;
            }

            await proxy.ProxyChatCompletionAsync(
                context,
                provider.BaseUrl,
                provider.ApiKey,
                request,
                cancellationToken);
        });

        app.Run();
    }
}
