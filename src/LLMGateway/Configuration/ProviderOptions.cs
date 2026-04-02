namespace LLMGateway.Configuration;

public class ProviderOptions
{
    /// <summary>
    /// Display name of the provider (e.g. "OpenAI", "DeepSeek", "Qwen")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the provider's OpenAI-compatible API (e.g. "https://api.openai.com")
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API key used to authenticate with the provider
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// List of model names this provider handles (e.g. ["gpt-4o", "gpt-4o-mini"])
    /// </summary>
    public List<string> Models { get; set; } = [];
}
