using System.Text.Json;
using LLMGateway.Models.Anthropic;
using LLMGateway.Models.OpenAI;

namespace LLMGateway.Services;

/// <summary>
/// Converts between Anthropic Messages API format and OpenAI Chat Completion API format.
/// </summary>
public static class AnthropicOpenAIConverter
{
    /// <summary>
    /// Converts an Anthropic Messages request to an OpenAI ChatCompletion request.
    /// </summary>
    public static ChatCompletionRequest ToOpenAI(AnthropicMessagesRequest anthropicRequest)
    {
        var messages = new List<ChatMessage>();

        // 1. System prompt: Anthropic "system" -> OpenAI system message
        if (anthropicRequest.System.HasValue)
        {
            var systemText = ExtractSystemText(anthropicRequest.System.Value);
            if (!string.IsNullOrEmpty(systemText))
            {
                messages.Add(new ChatMessage
                {
                    Role = "system",
                    Content = JsonSerializer.SerializeToElement(systemText)
                });
            }
        }

        // 2. Convert each Anthropic message to OpenAI messages
        foreach (var msg in anthropicRequest.Messages)
        {
            ConvertMessage(msg, messages);
        }

        // 3. Build the OpenAI request
        var openAIRequest = new ChatCompletionRequest
        {
            Model = anthropicRequest.Model,
            Messages = messages,
            MaxTokens = anthropicRequest.MaxTokens,
            Temperature = anthropicRequest.Temperature,
            TopP = anthropicRequest.TopP,
            Stream = anthropicRequest.Stream,
            Stop = anthropicRequest.StopSequences is { Count: > 0 }
                ? JsonSerializer.SerializeToElement(anthropicRequest.StopSequences)
                : null
        };

        // 4. Convert tools: Anthropic input_schema -> OpenAI function.parameters
        if (anthropicRequest.Tools is { Count: > 0 })
        {
            openAIRequest.Tools = anthropicRequest.Tools.Select(t => new OpenAITool
            {
                Type = "function",
                Function = new OpenAIFunction
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = t.InputSchema
                }
            }).ToList();
        }

        // 5. Convert tool_choice
        openAIRequest.ToolChoice = ConvertToolChoice(anthropicRequest.ToolChoice);

        return openAIRequest;
    }

    /// <summary>
    /// Converts an OpenAI ChatCompletion response to an Anthropic Messages response.
    /// </summary>
    public static AnthropicMessagesResponse ToAnthropicResponse(
        ChatCompletionResponse openAIResponse,
        string originalModel)
    {
        var contentBlocks = new List<AnthropicContentBlock>();
        var choice = openAIResponse.Choices.FirstOrDefault();

        if (choice?.Message != null)
        {
            // Text content
            var text = ExtractMessageText(choice.Message);
            if (!string.IsNullOrEmpty(text))
            {
                contentBlocks.Add(new AnthropicTextBlock { Text = text });
            }

            // Tool calls -> tool_use blocks
            if (choice.Message.ToolCalls is { Count: > 0 })
            {
                foreach (var tc in choice.Message.ToolCalls)
                {
                    JsonElement? input = null;
                    if (!string.IsNullOrEmpty(tc.Function.Arguments))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(tc.Function.Arguments);
                            input = doc.RootElement.Clone();
                        }
                        catch
                        {
                            // If arguments aren't valid JSON, wrap as string
                            input = JsonSerializer.SerializeToElement(tc.Function.Arguments);
                        }
                    }

                    contentBlocks.Add(new AnthropicToolUseBlock
                    {
                        Id = tc.Id,
                        Name = tc.Function.Name,
                        Input = input
                    });
                }
            }
        }

        // Ensure at least one content block
        if (contentBlocks.Count == 0)
        {
            contentBlocks.Add(new AnthropicTextBlock { Text = "" });
        }

        return new AnthropicMessagesResponse
        {
            Id = "msg_" + (openAIResponse.Id ?? Guid.NewGuid().ToString("N")[..24]),
            Model = originalModel,
            Content = contentBlocks,
            StopReason = ConvertFinishReason(choice?.FinishReason),
            StopSequence = null,
            Usage = new AnthropicUsage
            {
                InputTokens = openAIResponse.Usage?.PromptTokens ?? 0,
                OutputTokens = openAIResponse.Usage?.CompletionTokens ?? 0
            }
        };
    }

    /// <summary>
    /// Converts an OpenAI finish_reason to an Anthropic stop_reason.
    /// </summary>
    public static string? ConvertFinishReason(string? finishReason) => finishReason switch
    {
        "stop" => "end_turn",
        "length" => "max_tokens",
        "tool_calls" => "tool_use",
        "content_filter" => "end_turn",
        _ => finishReason
    };

    private static string ExtractSystemText(JsonElement system)
    {
        if (system.ValueKind == JsonValueKind.String)
            return system.GetString() ?? "";

        if (system.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var block in system.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var typeEl) &&
                    typeEl.GetString() == "text" &&
                    block.TryGetProperty("text", out var textEl))
                {
                    parts.Add(textEl.GetString() ?? "");
                }
            }
            return string.Join("\n", parts);
        }

        return "";
    }

    private static void ConvertMessage(AnthropicMessage msg, List<ChatMessage> messages)
    {
        if (!msg.Content.HasValue)
        {
            messages.Add(new ChatMessage { Role = msg.Role, Content = JsonSerializer.SerializeToElement("") });
            return;
        }

        var content = msg.Content.Value;

        // Simple string content
        if (content.ValueKind == JsonValueKind.String)
        {
            messages.Add(new ChatMessage { Role = msg.Role, Content = content.Clone() });
            return;
        }

        // Array of content blocks
        if (content.ValueKind != JsonValueKind.Array)
        {
            messages.Add(new ChatMessage { Role = msg.Role, Content = content.Clone() });
            return;
        }

        if (msg.Role == "user")
        {
            ConvertUserContentBlocks(content, messages);
        }
        else if (msg.Role == "assistant")
        {
            ConvertAssistantContentBlocks(content, messages);
        }
        else
        {
            messages.Add(new ChatMessage { Role = msg.Role, Content = content.Clone() });
        }
    }

    private static void ConvertUserContentBlocks(JsonElement content, List<ChatMessage> messages)
    {
        var textParts = new List<string>();

        foreach (var block in content.EnumerateArray())
        {
            var type = block.TryGetProperty("type", out var t) ? t.GetString() : null;

            switch (type)
            {
                case "text":
                    var text = block.TryGetProperty("text", out var te) ? te.GetString() : "";
                    textParts.Add(text ?? "");
                    break;

                case "tool_result":
                    // Flush accumulated text as a user message
                    FlushTextAsUserMessage(textParts, messages);

                    var toolUseId = block.TryGetProperty("tool_use_id", out var tui)
                        ? tui.GetString() ?? ""
                        : "";
                    var resultContent = block.TryGetProperty("content", out var rc)
                        ? ExtractTextFromContent(rc)
                        : "";

                    messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        ToolCallId = toolUseId,
                        Content = JsonSerializer.SerializeToElement(resultContent)
                    });
                    break;

                case "image":
                    // Flush text, skip image blocks (not supported by OpenAI text-only API)
                    FlushTextAsUserMessage(textParts, messages);
                    break;

                default:
                    // For unknown blocks, try to extract text
                    if (block.TryGetProperty("text", out var unknownText))
                        textParts.Add(unknownText.GetString() ?? "");
                    break;
            }
        }

        FlushTextAsUserMessage(textParts, messages);
    }

    private static void ConvertAssistantContentBlocks(JsonElement content, List<ChatMessage> messages)
    {
        var textParts = new List<string>();
        var toolCalls = new List<OpenAIToolCall>();

        foreach (var block in content.EnumerateArray())
        {
            var type = block.TryGetProperty("type", out var t) ? t.GetString() : null;

            switch (type)
            {
                case "text":
                    var text = block.TryGetProperty("text", out var te) ? te.GetString() : "";
                    textParts.Add(text ?? "");
                    break;

                case "tool_use":
                    var id = block.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    var name = block.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                    var inputJson = "";
                    if (block.TryGetProperty("input", out var inputEl))
                    {
                        inputJson = inputEl.ValueKind == JsonValueKind.Object
                            ? inputEl.GetRawText()
                            : "{}";
                    }

                    toolCalls.Add(new OpenAIToolCall
                    {
                        Id = id,
                        Type = "function",
                        Function = new OpenAIToolCallFunction
                        {
                            Name = name,
                            Arguments = inputJson
                        }
                    });
                    break;

                case "thinking":
                    // Skip thinking blocks for OpenAI conversion
                    break;
            }
        }

        var combinedText = string.Join("", textParts);
        var msg = new ChatMessage
        {
            Role = "assistant",
            Content = string.IsNullOrEmpty(combinedText) && toolCalls.Count > 0
                ? null
                : JsonSerializer.SerializeToElement(combinedText)
        };

        if (toolCalls.Count > 0)
            msg.ToolCalls = toolCalls;

        messages.Add(msg);
    }

    private static void FlushTextAsUserMessage(List<string> textParts, List<ChatMessage> messages)
    {
        if (textParts.Count == 0) return;
        var combined = string.Join("\n", textParts);
        messages.Add(new ChatMessage { Role = "user", Content = JsonSerializer.SerializeToElement(combined) });
        textParts.Clear();
    }

    private static string ExtractTextFromContent(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? "";

        if (content.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("text", out var te))
                    parts.Add(te.GetString() ?? "");
                else
                    parts.Add(block.GetRawText());
            }
            return string.Join("\n", parts);
        }

        return content.GetRawText();
    }

    private static string? ExtractMessageText(ChatMessage message)
    {
        if (!message.Content.HasValue || message.Content.Value.ValueKind == JsonValueKind.Null)
            return null;
        return message.Content.Value.ValueKind == JsonValueKind.String
            ? message.Content.Value.GetString()
            : message.Content.Value.GetRawText();
    }

    private static JsonElement? ConvertToolChoice(JsonElement? toolChoice)
    {
        if (!toolChoice.HasValue) return null;

        var tc = toolChoice.Value;
        if (tc.ValueKind != JsonValueKind.Object) return null;

        if (!tc.TryGetProperty("type", out var typeEl)) return null;
        var type = typeEl.GetString();

        return type switch
        {
            "auto" => JsonSerializer.SerializeToElement("auto"),
            "any" => JsonSerializer.SerializeToElement("required"),
            "tool" => tc.TryGetProperty("name", out var nameEl)
                ? JsonSerializer.SerializeToElement(new
                {
                    type = "function",
                    @function = new { name = nameEl.GetString() }
                })
                : null,
            "none" => JsonSerializer.SerializeToElement("none"),
            _ => null
        };
    }
}
