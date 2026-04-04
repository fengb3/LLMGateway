using LLMGateway.Models.OpenAI;
using LLMGateway.Services;

namespace LLMGateway.Endpoints;

public static class ModelEndpoints
{
    public static WebApplication MapModelEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/models", async (IProviderRouter router, CancellationToken cancellationToken) =>
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var models = (await router.GetAllModelsAsync(cancellationToken))
                .Select(m => new ModelInfo
                {
                    Id = m.ModelName,
                    OwnedBy = m.ProviderName,
                    Created = now
                })
                .ToList();

            return Results.Ok(new ModelListResponse { Data = models });
        });

        return app;
    }
}
