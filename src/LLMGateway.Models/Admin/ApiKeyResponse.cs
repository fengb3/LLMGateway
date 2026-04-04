using System.Text.Json.Serialization;

namespace LLMGateway.Models.Admin;

public class ApiKeyResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

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
