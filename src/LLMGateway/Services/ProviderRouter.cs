using LLMGateway.Configuration;
using Microsoft.Extensions.Options;

namespace LLMGateway.Services;

/// <summary>
/// Routes a model name to the configured upstream provider.
/// </summary>
public class ProviderRouter : IProviderRouter
{
    private readonly IOptionsSnapshot<GatewayOptions> _options;

    public ProviderRouter(IOptionsSnapshot<GatewayOptions> options)
    {
        _options = options;
    }

    public ProviderOptions? GetProvider(string modelName)
    {
        foreach (var provider in _options.Value.Providers)
        {
            foreach (var model in provider.Models)
            {
                if (string.Equals(model, modelName, StringComparison.OrdinalIgnoreCase))
                    return provider;
            }
        }
        return null;
    }

    public IReadOnlyList<(string ModelName, string ProviderName)> GetAllModels()
    {
        var result = new List<(string, string)>();
        foreach (var provider in _options.Value.Providers)
        {
            foreach (var model in provider.Models)
                result.Add((model, provider.Name));
        }
        return result;
    }
}
