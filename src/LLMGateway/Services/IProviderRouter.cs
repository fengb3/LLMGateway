using LLMGateway.Configuration;

namespace LLMGateway.Services;

public interface IProviderRouter
{
    /// <summary>
    /// Returns the provider configured for the given model name, or null if not found.
    /// </summary>
    ProviderOptions? GetProvider(string modelName);

    /// <summary>
    /// Returns all configured model names across all providers.
    /// </summary>
    IReadOnlyList<(string ModelName, string ProviderName)> GetAllModels();
}
