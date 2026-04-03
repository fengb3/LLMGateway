using System.Text.Json.Serialization;

namespace LLMGateway.Models.OpenAI;

public class ErrorResponse
{
    [JsonPropertyName("error")]
    public ErrorDetail Error { get; set; } = new();
}

public class ErrorDetail
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "gateway_error";

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
