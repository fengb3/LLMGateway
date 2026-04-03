using System.Security.Cryptography;
using System.Text;
using LLMGateway.Data.Entities;
using LLMGateway.Data.Repositories;
using LLMGateway.Models.Admin;
using LLMGateway.Models.OpenAI;

namespace LLMGateway.Endpoints;

public static class AdminApiKeyEndpoints
{
    public static WebApplication MapAdminApiKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/apikeys");

        group.MapGet("/", async (IApiKeyRepository repo, CancellationToken ct) =>
        {
            var entities = await repo.GetAllAsync(ct);
            var responses = entities.Select(MapToResponse).ToList();
            return Results.Ok(responses);
        });

        group.MapGet("/{id:int}", async (int id, IApiKeyRepository repo, CancellationToken ct) =>
        {
            var entity = await repo.GetByIdAsync(id, ct);
            return entity is null
                ? Results.NotFound(new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = $"API key with id '{id}' not found.",
                        Type = "invalid_request_error",
                        Code = "apikey_not_found"
                    }
                })
                : Results.Ok(MapToResponse(entity));
        });

        group.MapPost("/", async (HttpContext context, IApiKeyRepository repo, CancellationToken ct) =>
        {
            var request = await context.Request.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.CreateApiKeyRequest, ct);

            if (request is null || string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = "Name is required.",
                        Type = "invalid_request_error",
                        Code = "missing_fields"
                    }
                });
            }

            // Generate a random API key
            var keyBytes = RandomNumberGenerator.GetBytes(32);
            var keyHex = Convert.ToHexStringLower(keyBytes);
            var plainTextKey = $"sk-gw-{keyHex}";
            var keyPrefix = plainTextKey[..12] + "...";

            // Hash the key for storage
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainTextKey));
            var keyHash = Convert.ToHexStringLower(hashBytes);

            var now = DateTime.UtcNow;
            var entity = new ApiKeyEntity
            {
                KeyHash = keyHash,
                KeyPrefix = keyPrefix,
                Name = request.Name,
                IsActive = true,
                CreatedAt = now,
                ExpiresAt = request.ExpiresAt
            };

            var created = await repo.AddAsync(entity, ct);

            // Return the plaintext key only this once
            return Results.Created($"/admin/apikeys/{created.Id}", new ApiKeyCreatedResponse
            {
                Id = created.Id,
                Key = plainTextKey,
                KeyPrefix = keyPrefix,
                Name = created.Name,
                IsActive = created.IsActive,
                CreatedAt = created.CreatedAt,
                ExpiresAt = created.ExpiresAt
            });
        });

        group.MapPut("/{id:int}", async (int id, HttpContext context, IApiKeyRepository repo, CancellationToken ct) =>
        {
            var entity = await repo.GetByIdAsync(id, ct);
            if (entity is null)
            {
                return Results.NotFound(new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = $"API key with id '{id}' not found.",
                        Type = "invalid_request_error",
                        Code = "apikey_not_found"
                    }
                });
            }

            var request = await context.Request.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.UpdateApiKeyRequest, ct);

            if (request is not null)
            {
                if (request.Name is not null)
                    entity.Name = request.Name;
                if (request.IsActive.HasValue)
                    entity.IsActive = request.IsActive.Value;
                if (request.ExpiresAt.HasValue)
                    entity.ExpiresAt = request.ExpiresAt.Value;
            }

            await repo.UpdateAsync(entity, ct);
            return Results.Ok(MapToResponse(entity));
        });

        group.MapDelete("/{id:int}", async (int id, IApiKeyRepository repo, CancellationToken ct) =>
        {
            var entity = await repo.GetByIdAsync(id, ct);
            if (entity is null)
            {
                return Results.NotFound(new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = $"API key with id '{id}' not found.",
                        Type = "invalid_request_error",
                        Code = "apikey_not_found"
                    }
                });
            }

            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }

    private static ApiKeyResponse MapToResponse(ApiKeyEntity entity)
    {
        return new ApiKeyResponse
        {
            Id = entity.Id,
            KeyPrefix = entity.KeyPrefix,
            Name = entity.Name,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt
        };
    }
}
