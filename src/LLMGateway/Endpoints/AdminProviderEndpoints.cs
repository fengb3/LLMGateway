using LLMGateway.Data.Entities;
using LLMGateway.Data.Repositories;
using LLMGateway.Models.Admin;
using LLMGateway.Models.OpenAI;

namespace LLMGateway.Endpoints;

public static class AdminProviderEndpoints
{
    public static WebApplication MapAdminProviderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/providers");

        group.MapGet("/", async (IProviderRepository repo, CancellationToken ct) =>
        {
            var entities = await repo.GetAllAsync(ct);
            var responses = entities.Select(MapToResponse).ToList();
            return Results.Ok(responses);
        });

        group.MapGet("/{id:int}", async (int id, IProviderRepository repo, CancellationToken ct) =>
        {
            var entity = await repo.GetByIdAsync(id, ct);
            return entity is null
                ? Results.NotFound(new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = $"Provider with id '{id}' not found.",
                        Type = "invalid_request_error",
                        Code = "provider_not_found"
                    }
                })
                : Results.Ok(MapToResponse(entity));
        });

        group.MapPost("/", async (HttpContext context, IProviderRepository repo, CancellationToken ct) =>
        {
            var request = await context.Request.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.CreateProviderRequest, ct);

            if (request is null || string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.BaseUrl)
                || string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return Results.BadRequest(new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = "Name, BaseUrl, and ApiKey are required.",
                        Type = "invalid_request_error",
                        Code = "missing_fields"
                    }
                });
            }

            var existing = await repo.GetByNameAsync(request.Name, ct);
            if (existing is not null)
            {
                return Results.Conflict(new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = $"Provider with name '{request.Name}' already exists.",
                        Type = "invalid_request_error",
                        Code = "duplicate_name"
                    }
                });
            }

            var entity = new ProviderEntity
            {
                Name = request.Name,
                BaseUrl = request.BaseUrl,
                ApiKey = request.ApiKey,
                Models = request.Models,
                IsEnabled = request.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await repo.AddAsync(entity, ct);
            return Results.Created($"/admin/providers/{created.Id}", MapToResponse(created));
        });

        group.MapPut("/{id:int}", async (int id, HttpContext context, IProviderRepository repo, CancellationToken ct) =>
        {
            var entity = await repo.GetByIdAsync(id, ct);
            if (entity is null)
            {
                return Results.NotFound(new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = $"Provider with id '{id}' not found.",
                        Type = "invalid_request_error",
                        Code = "provider_not_found"
                    }
                });
            }

            var request = await context.Request.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.UpdateProviderRequest, ct);

            if (request is not null)
            {
                if (request.Name is not null)
                    entity.Name = request.Name;
                if (request.BaseUrl is not null)
                    entity.BaseUrl = request.BaseUrl;
                if (request.ApiKey is not null)
                    entity.ApiKey = request.ApiKey;
                if (request.Models is not null)
                    entity.Models = request.Models;
                if (request.IsEnabled.HasValue)
                    entity.IsEnabled = request.IsEnabled.Value;
            }

            await repo.UpdateAsync(entity, ct);
            return Results.Ok(MapToResponse(entity));
        });

        group.MapDelete("/{id:int}", async (int id, IProviderRepository repo, CancellationToken ct) =>
        {
            var entity = await repo.GetByIdAsync(id, ct);
            if (entity is null)
            {
                return Results.NotFound(new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Message = $"Provider with id '{id}' not found.",
                        Type = "invalid_request_error",
                        Code = "provider_not_found"
                    }
                });
            }

            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }

    private static ProviderResponse MapToResponse(ProviderEntity entity)
    {
        var maskedApiKey = entity.ApiKey.Length > 4
            ? new string('*', entity.ApiKey.Length - 4) + entity.ApiKey[^4..]
            : new string('*', entity.ApiKey.Length);

        return new ProviderResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            BaseUrl = entity.BaseUrl,
            ApiKey = maskedApiKey,
            Models = entity.Models,
            IsEnabled = entity.IsEnabled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
