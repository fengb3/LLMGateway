using System.Text.Json.Serialization;

namespace LLMGateway.Models.Anthropic;

public class AnthropicErrorResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "error";

    [JsonPropertyName("error")]
    public AnthropicErrorDetail Error { get; set; } = new();
}

public class AnthropicErrorDetail
{
    [JsonPropertyName("type")]
    public string ErrorType { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
