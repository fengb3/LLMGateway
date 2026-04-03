using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMGateway.Models.OpenAI;

/// <summary>
/// OpenAI chat completions request body.
/// </summary>
public class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("n")]
    public int? N { get; set; }

    [JsonPropertyName("stop")]
    public JsonElement? Stop { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("stream_options")]
    public StreamOptions? StreamOptions { get; set; }
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class StreamOptions
{
    [JsonPropertyName("include_usage")]
    public bool? IncludeUsage { get; set; }
}
