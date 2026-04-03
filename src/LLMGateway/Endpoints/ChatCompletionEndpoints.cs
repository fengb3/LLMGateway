using System.Text.Json;
using LLMGateway.Models.OpenAI;
using LLMGateway.Services;

namespace LLMGateway.Endpoints;

public static class ChatCompletionEndpoints
{
    public static WebApplication MapChatCompletionEndpoints(this WebApplication app)
    {
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

            var provider = await router.GetProviderAsync(request.Model, cancellationToken);
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

        return app;
    }
}
