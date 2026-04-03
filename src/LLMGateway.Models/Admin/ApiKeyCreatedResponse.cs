using System.Text.Json.Serialization;

namespace LLMGateway.Models.Admin;

/// <summary>
/// Returned only once when a new API key is created.
/// The plaintext key is never stored and cannot be retrieved again.
/// </summary>
public class ApiKeyCreatedResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("keyPrefix")]
    public string KeyPrefix { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }
}
