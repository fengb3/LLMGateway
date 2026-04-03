using System.Text.Json.Serialization;

namespace LLMGateway.Models.Anthropic;

public class AnthropicSseEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class MessageStartEvent : AnthropicSseEvent
{
    [JsonPropertyName("message")]
    public AnthropicMessagesResponse Message { get; set; } = new();

    public MessageStartEvent() => Type = "message_start";
}

public class ContentBlockStartEvent : AnthropicSseEvent
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("content_block")]
    public AnthropicContentBlock ContentBlock { get; set; } = new();

    public ContentBlockStartEvent() => Type = "content_block_start";
}

public class ContentBlockDeltaEvent : AnthropicSseEvent
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public AnthropicContentDelta Delta { get; set; } = new();

    public ContentBlockDeltaEvent() => Type = "content_block_delta";
}

public class ContentBlockStopEvent : AnthropicSseEvent
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    public ContentBlockStopEvent() => Type = "content_block_stop";
}

public class MessageDeltaEvent : AnthropicSseEvent
{
    [JsonPropertyName("delta")]
    public MessageDeltaData Delta { get; set; } = new();

    [JsonPropertyName("usage")]
    public AnthropicUsage Usage { get; set; } = new();

    public MessageDeltaEvent() => Type = "message_delta";
}

public class MessageDeltaData
{
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }
}

public class MessageStopEvent : AnthropicSseEvent
{
    public MessageStopEvent() => Type = "message_stop";
}

public class PingEvent : AnthropicSseEvent
{
    public PingEvent() => Type = "ping";
}

// Delta subtypes
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextDelta), "text_delta")]
[JsonDerivedType(typeof(InputJsonDelta), "input_json_delta")]
[JsonDerivedType(typeof(ThinkingDelta), "thinking_delta")]
[JsonDerivedType(typeof(SignatureDelta), "signature_delta")]
public class AnthropicContentDelta
{
    [JsonIgnore]
    public string Type { get; set; } = string.Empty;
}

public class TextDelta : AnthropicContentDelta
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    public TextDelta() => Type = "text_delta";
}

public class InputJsonDelta : AnthropicContentDelta
{
    [JsonPropertyName("partial_json")]
    public string PartialJson { get; set; } = string.Empty;

    public InputJsonDelta() => Type = "input_json_delta";
}

public class ThinkingDelta : AnthropicContentDelta
{
    [JsonPropertyName("thinking")]
    public string Thinking { get; set; } = string.Empty;

    public ThinkingDelta() => Type = "thinking_delta";
}

public class SignatureDelta : AnthropicContentDelta
{
    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    public SignatureDelta() => Type = "signature_delta";
}
