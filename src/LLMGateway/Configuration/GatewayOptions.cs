namespace LLMGateway.Configuration;

public class GatewayOptions
{
    public const string SectionName = "Gateway";

    /// <summary>
    /// List of configured upstream LLM providers
    /// </summary>
    public List<ProviderOptions> Providers { get; set; } = [];

    /// <summary>
    /// API keys that clients use to access the gateway
    /// </summary>
    public List<ApiKeyEntry> ApiKeys { get; set; } = [];
}

public class ApiKeyEntry
{
    /// <summary>
    /// The API key value (e.g. "sk-gateway-abc123")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name / owner of this key
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this key is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
}
