namespace LLMGateway.Configuration;

public class GatewayOptions
{
    public const string SectionName = "Gateway";

    /// <summary>
    /// List of configured upstream LLM providers (used for initial DB seeding).
    /// </summary>
    public List<ProviderOptions> Providers { get; set; } = [];

    /// <summary>
    /// Admin API keys used to access /admin endpoints (configured in appsettings).
    /// </summary>
    public List<AdminApiKeyEntry> AdminApiKeys { get; set; } = [];
}

public class AdminApiKeyEntry
{
    /// <summary>
    /// The API key value (e.g. "sk-admin-abc123").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name / owner of this key.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this key is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
