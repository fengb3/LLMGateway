using LLMGateway.Configuration;

namespace LLMGateway.Services;

public interface IProviderRouter
{
    /// <summary>
    /// Returns the provider configured for the given model name, or null if not found.
    /// </summary>
    Task<ProviderOptions?> GetProviderAsync(string modelName, CancellationToken ct = default);

    /// <summary>
    /// Returns all configured model names across all providers.
    /// </summary>
    Task<IReadOnlyList<(string ModelName, string ProviderName)>> GetAllModelsAsync(CancellationToken ct = default);
}
