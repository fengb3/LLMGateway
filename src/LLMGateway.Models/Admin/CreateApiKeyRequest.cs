using System.Text.Json.Serialization;

namespace LLMGateway.Models.Admin;

public class CreateApiKeyRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }
}
