using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMGateway.Models.Anthropic;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AnthropicTextBlock), "text")]
[JsonDerivedType(typeof(AnthropicToolUseBlock), "tool_use")]
[JsonDerivedType(typeof(AnthropicToolResultBlock), "tool_result")]
[JsonDerivedType(typeof(AnthropicThinkingBlock), "thinking")]
public class AnthropicContentBlock
{
    [JsonIgnore]
    public string Type { get; set; } = string.Empty;
}

public class AnthropicTextBlock : AnthropicContentBlock
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    public AnthropicTextBlock() => Type = "text";
}

public class AnthropicToolUseBlock : AnthropicContentBlock
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public JsonElement? Input { get; set; }

    public AnthropicToolUseBlock() => Type = "tool_use";
}

public class AnthropicToolResultBlock : AnthropicContentBlock
{
    [JsonPropertyName("tool_use_id")]
    public string ToolUseId { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }

    public AnthropicToolResultBlock() => Type = "tool_result";
}

public class AnthropicThinkingBlock : AnthropicContentBlock
{
    [JsonPropertyName("thinking")]
    public string Thinking { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    public AnthropicThinkingBlock() => Type = "thinking";
}
