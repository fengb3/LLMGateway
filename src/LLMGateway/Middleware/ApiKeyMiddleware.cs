using System.Security.Cryptography;
using System.Text;
using LLMGateway.Configuration;
using LLMGateway.Data.Repositories;
using LLMGateway.Models.OpenAI;
using Microsoft.Extensions.Options;

namespace LLMGateway.Middleware;

/// <summary>
/// Validates the Bearer token in the Authorization header.
///   - /health              → no auth required
///   - /admin/*             → validated against AdminApiKeys in appsettings
///   - everything else      → validated against ApiKeys table in the database
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOptionsSnapshot<GatewayOptions> options,
        IApiKeyRepository apiKeyRepo)
    {
        // Health check endpoint is publicly accessible
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        var xApiKey = context.Request.Headers["x-api-key"].ToString();

        string providedKey;
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            providedKey = authHeader["Bearer ".Length..].Trim();
        }
        else if (!string.IsNullOrEmpty(xApiKey))
        {
            providedKey = xApiKey.Trim();
        }
        else
        {
            await WriteUnauthorizedAsync(context, "Missing or invalid Authorization header. Use: Authorization: Bearer <api-key> or x-api-key header.", "missing_api_key");
            return;
        }
        var isAdminRoute = context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase);

        if (isAdminRoute)
        {
            // Admin routes: validate against config-based AdminApiKeys
            if (!ValidateAdminKey(providedKey, options.Value.AdminApiKeys))
            {
                await WriteUnauthorizedAsync(context, "Invalid admin API key.", "invalid_api_key");
                return;
            }
        }
        else
        {
            // User routes (/v1/*): validate against DB-stored API keys
            if (!await ValidateUserKeyAsync(providedKey, apiKeyRepo, context.RequestAborted))
            {
                await WriteUnauthorizedAsync(context, "Invalid API key.", "invalid_api_key");
                return;
            }
        }

        await _next(context);
    }

    private static bool ValidateAdminKey(string providedKey, List<AdminApiKeyEntry> adminKeys)
    {
        var providedKeyBytes = Encoding.UTF8.GetBytes(providedKey);

        return adminKeys.Any(k =>
        {
            if (!k.IsActive)
                return false;
            var configuredBytes = Encoding.UTF8.GetBytes(k.Key);
            return configuredBytes.Length == providedKeyBytes.Length
                && CryptographicOperations.FixedTimeEquals(configuredBytes, providedKeyBytes);
        });
    }

    private static async Task<bool> ValidateUserKeyAsync(
        string providedKey,
        IApiKeyRepository repo,
        CancellationToken ct)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
        var keyHash = Convert.ToHexStringLower(hashBytes);

        var entity = await repo.GetByKeyHashAsync(keyHash, ct);
        if (entity is null || !entity.IsActive)
            return false;

        if (entity.ExpiresAt.HasValue && entity.ExpiresAt.Value < DateTime.UtcNow)
            return false;

        return true;
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string message, string code)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(
            new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Message = message,
                    Type = "auth_error",
                    Code = code
                }
            },
            AppJsonSerializerContext.Default.ErrorResponse);
    }
}
