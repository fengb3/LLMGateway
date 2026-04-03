using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMGateway.Models.Anthropic;

public class AnthropicMessagesRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 4096;

    [JsonPropertyName("messages")]
    public List<AnthropicMessage> Messages { get; set; } = [];

    [JsonPropertyName("system")]
    public JsonElement? System { get; set; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("stop_sequences")]
    public List<string>? StopSequences { get; set; }

    [JsonPropertyName("tools")]
    public List<AnthropicTool>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public JsonElement? ToolChoice { get; set; }

    [JsonPropertyName("metadata")]
    public JsonElement? Metadata { get; set; }
}

public class AnthropicMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }
}
