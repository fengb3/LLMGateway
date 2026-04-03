using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMGateway.Models.Anthropic;

public class AnthropicTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("input_schema")]
    public JsonElement? InputSchema { get; set; }
}
