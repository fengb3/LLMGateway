using LLMGateway.Configuration;
using LLMGateway.Data.Entities;
using LLMGateway.Data.Repositories;

namespace LLMGateway.Services;

/// <summary>
/// Routes a model name to the configured upstream provider.
/// </summary>
public class ProviderRouter : IProviderRouter
{
    private readonly IProviderRepository _repository;

    public ProviderRouter(IProviderRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProviderOptions?> GetProviderAsync(string modelName, CancellationToken ct = default)
    {
        var entity = await _repository.GetByModelNameAsync(modelName, ct);
        return entity is null ? null : MapToOptions(entity);
    }

    public async Task<IReadOnlyList<(string ModelName, string ProviderName)>> GetAllModelsAsync(CancellationToken ct = default)
    {
        var providers = await _repository.GetAllAsync(ct);
        var result = new List<(string, string)>();
        foreach (var provider in providers)
        {
            if (!provider.IsEnabled) continue;
            foreach (var model in provider.Models)
                result.Add((model, provider.Name));
        }
        return result;
    }

    private static ProviderOptions MapToOptions(ProviderEntity entity)
    {
        return new ProviderOptions
        {
            Name = entity.Name,
            BaseUrl = entity.BaseUrl,
            ApiKey = entity.ApiKey,
            Models = entity.Models
        };
    }
}
