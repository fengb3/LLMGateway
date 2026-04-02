using LLMGateway.Configuration;
using LLMGateway.Models.OpenAI;
using Microsoft.Extensions.Options;

namespace LLMGateway.Middleware;

/// <summary>
/// Middleware that validates the Bearer token (gateway API key) present in
/// the Authorization header against the configured list of API keys.
/// Requests to /health are always allowed through.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptionsSnapshot<GatewayOptions> options)
    {
        // Health check endpoint is publicly accessible
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = "Missing or invalid Authorization header. Use: Authorization: Bearer <api-key>",
                        Type = "auth_error",
                        Code = "missing_api_key"
                    }
                },
                AppJsonSerializerContext.Default.ErrorResponse);
            return;
        }

        var providedKey = authHeader["Bearer ".Length..].Trim();
        var validKey = options.Value.ApiKeys
            .FirstOrDefault(k => k.IsActive && k.Key == providedKey);

        if (validKey is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = "Invalid API key.",
                        Type = "auth_error",
                        Code = "invalid_api_key"
                    }
                },
                AppJsonSerializerContext.Default.ErrorResponse);
            return;
        }

        await _next(context);
    }
}
