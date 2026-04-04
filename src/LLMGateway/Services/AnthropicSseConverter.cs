using System.Text;
using System.Text.Json;
using LLMGateway.Models.Anthropic;
using LLMGateway.Models.OpenAI;

namespace LLMGateway.Services;

/// <summary>
/// Converts an OpenAI SSE streaming response into an Anthropic SSE streaming response.
/// Reads OpenAI chunks and emits Anthropic events (message_start, content_block_start,
/// content_block_delta, content_block_stop, message_delta, message_stop).
/// </summary>
public static class AnthropicSseConverter
{
    public static async Task ConvertStreamAsync(
        Stream upstreamStream,
        Stream outputStream,
        string originalModel,
        string requestId,
        CancellationToken ct)
    {
        var writer = new StreamWriter(outputStream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true);
        var messageId = $"msg_{requestId}";
        var blockIndex = -1;
        var currentBlockType = "";  // "text", "thinking", or "tool_use"
        var hasOpenBlock = false;
        var finalStopReason = "end_turn";
        var totalOutputTokens = 0;
        var inputTokens = 0;

        // Track tool calls by their OpenAI index
        var toolCallIndices = new Dictionary<int, int>();  // openai tool_call index -> anthropic block index

        try
        {
            // Emit message_start
            await EmitSseEventAsync(writer, "message_start", SerializeMessageStart(messageId, originalModel), ct);

            // Emit ping
            await EmitSseEventAsync(writer, "ping", """{"type":"ping"}""", ct);

            using var reader = new StreamReader(upstreamStream, Encoding.UTF8, leaveOpen: true);

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

                var data = line["data: ".Length..];

                if (data == "[DONE]")
                {
                    // Close any open content block
                    if (hasOpenBlock)
                    {
                        await EmitContentBlockStopAsync(writer, blockIndex, ct);
                        hasOpenBlock = false;
                    }

                    // Emit message_delta with final stop_reason and usage
                    await EmitSseEventAsync(writer, "message_delta",
                        SerializeMessageDelta(finalStopReason, totalOutputTokens), ct);

                    // Emit message_stop
                    await EmitSseEventAsync(writer, "message_stop", """{"type":"message_stop"}""", ct);
                    break;
                }

                // Parse the OpenAI chunk
                ChatCompletionResponse? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize(data, AppJsonSerializerContext.Default.ChatCompletionResponse);
                }
                catch
                {
                    continue;
                }

                if (chunk == null) continue;

                var choice = chunk.Choices.FirstOrDefault();
                if (choice == null) continue;

                // Capture usage from chunks (OpenAI sends usage in the last chunk when stream_options.include_usage is true)
                if (chunk.Usage != null)
                {
                    inputTokens = chunk.Usage.PromptTokens > 0 ? chunk.Usage.PromptTokens : inputTokens;
                    totalOutputTokens = chunk.Usage.CompletionTokens > 0 ? chunk.Usage.CompletionTokens : totalOutputTokens;
                }

                // Handle finish_reason
                if (!string.IsNullOrEmpty(choice.FinishReason))
                {
                    finalStopReason = AnthropicOpenAIConverter.ConvertFinishReason(choice.FinishReason) ?? "end_turn";
                }

                var delta = choice.Delta;
                if (delta == null) continue;

                // Handle reasoning_content delta → thinking block
                if (!string.IsNullOrEmpty(delta.ReasoningContent))
                {
                    // Start a thinking block if not in one
                    if (!hasOpenBlock || currentBlockType != "thinking")
                    {
                        if (hasOpenBlock)
                        {
                            await EmitContentBlockStopAsync(writer, blockIndex, ct);
                        }
                        blockIndex++;
                        currentBlockType = "thinking";
                        hasOpenBlock = true;
                        await EmitContentBlockStartAsync(writer, blockIndex,
                            """{"type":"thinking","thinking":""}""", ct);
                    }

                    var thinkingDeltaJson = JsonSerializer.Serialize(
                        new ContentBlockDeltaEvent
                        {
                            Index = blockIndex,
                            Delta = new ThinkingDelta { Thinking = delta.ReasoningContent }
                        },
                        AppJsonSerializerContext.Default.ContentBlockDeltaEvent);
                    await EmitSseEventAsync(writer, "content_block_delta", thinkingDeltaJson, ct);
                    totalOutputTokens++;
                }

                // Handle text content delta
                if (delta.Content.HasValue &&
                    delta.Content.Value.ValueKind == JsonValueKind.String)
                {
                    var text = delta.Content.Value.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        // Start a text block if not in one
                        if (!hasOpenBlock || currentBlockType != "text")
                        {
                            if (hasOpenBlock)
                            {
                                await EmitContentBlockStopAsync(writer, blockIndex, ct);
                            }
                            blockIndex++;
                            currentBlockType = "text";
                            hasOpenBlock = true;
                            await EmitContentBlockStartAsync(writer, blockIndex, """{"type":"text","text":""}""", ct);
                        }

                        // Emit text delta
                        var textDeltaJson = JsonSerializer.Serialize(
                            new ContentBlockDeltaEvent
                            {
                                Index = blockIndex,
                                Delta = new TextDelta { Text = text }
                            },
                            AppJsonSerializerContext.Default.ContentBlockDeltaEvent);
                        await EmitSseEventAsync(writer, "content_block_delta", textDeltaJson, ct);
                        totalOutputTokens++;
                    }
                }

                // Handle tool_calls delta
                if (delta.ToolCalls is { Count: > 0 })
                {
                    foreach (var tcDelta in delta.ToolCalls)
                    {
                        var tcIndex = tcDelta.Index;

                        // New tool call: has id and function name
                        if (!string.IsNullOrEmpty(tcDelta.Id) ||
                            (!string.IsNullOrEmpty(tcDelta.Function.Name) && !toolCallIndices.ContainsKey(tcIndex)))
                        {
                            // Close current block if open
                            if (hasOpenBlock)
                            {
                                await EmitContentBlockStopAsync(writer, blockIndex, ct);
                                hasOpenBlock = false;
                            }

                            blockIndex++;
                            toolCallIndices[tcIndex] = blockIndex;
                            currentBlockType = "tool_use";
                            hasOpenBlock = true;

                            var toolUseId = string.IsNullOrEmpty(tcDelta.Id)
                                ? $"toolu_{Guid.NewGuid().ToString("N")[..24]}"
                                : tcDelta.Id;
                            var toolName = tcDelta.Function.Name ?? "";

                            var startJson = JsonSerializer.Serialize(
                                new ContentBlockStartEvent
                                {
                                    Index = blockIndex,
                                    ContentBlock = new AnthropicToolUseBlock
                                    {
                                        Id = toolUseId,
                                        Name = toolName
                                    }
                                },
                                AppJsonSerializerContext.Default.ContentBlockStartEvent);
                            await EmitSseEventAsync(writer, "content_block_start", startJson, ct);

                            // If there are initial arguments, emit them
                            var initialArgs = tcDelta.Function.Arguments ?? "";
                            if (!string.IsNullOrEmpty(initialArgs))
                            {
                                var argsDeltaJson = JsonSerializer.Serialize(
                                    new ContentBlockDeltaEvent
                                    {
                                        Index = blockIndex,
                                        Delta = new InputJsonDelta { PartialJson = initialArgs }
                                    },
                                    AppJsonSerializerContext.Default.ContentBlockDeltaEvent);
                                await EmitSseEventAsync(writer, "content_block_delta", argsDeltaJson, ct);
                            }
                        }
                        // Continuing tool call: arguments fragment
                        else if (!string.IsNullOrEmpty(tcDelta.Function.Arguments) &&
                                 toolCallIndices.TryGetValue(tcIndex, out var bi))
                        {
                            var argsDeltaJson = JsonSerializer.Serialize(
                                new ContentBlockDeltaEvent
                                {
                                    Index = bi,
                                    Delta = new InputJsonDelta { PartialJson = tcDelta.Function.Arguments }
                                },
                                AppJsonSerializerContext.Default.ContentBlockDeltaEvent);
                            await EmitSseEventAsync(writer, "content_block_delta", argsDeltaJson, ct);
                        }
                    }
                }
            }

            await writer.FlushAsync(ct);
        }
        finally
        {
            await writer.DisposeAsync();
        }
    }

    private static async Task EmitSseEventAsync(StreamWriter writer, string eventType, string jsonData, CancellationToken ct)
    {
        await writer.WriteAsync($"event: {eventType}\n");
        await writer.WriteAsync($"data: {jsonData}\n");
        await writer.WriteAsync("\n");
        await writer.FlushAsync(ct);
    }

    private static async Task EmitContentBlockStartAsync(StreamWriter writer, int index, string contentBlockJson, CancellationToken ct)
    {
        var evtJson = $"{{\"type\":\"content_block_start\",\"index\":{index},\"content_block\":{contentBlockJson}}}";
        await EmitSseEventAsync(writer, "content_block_start", evtJson, ct);
    }

    private static async Task EmitContentBlockStopAsync(StreamWriter writer, int index, CancellationToken ct)
    {
        await EmitSseEventAsync(writer, "content_block_stop", $"{{\"type\":\"content_block_stop\",\"index\":{index}}}", ct);
    }

    private static string SerializeMessageStart(string messageId, string model)
    {
        var evt = new MessageStartEvent
        {
            Message = new AnthropicMessagesResponse
            {
                Id = messageId,
                Model = model,
                Usage = new AnthropicUsage()
            }
        };
        return JsonSerializer.Serialize(evt, AppJsonSerializerContext.Default.MessageStartEvent);
    }

    private static string SerializeMessageDelta(string stopReason, int outputTokens)
    {
        var evt = new MessageDeltaEvent
        {
            Delta = new MessageDeltaData { StopReason = stopReason },
            Usage = new AnthropicUsage { OutputTokens = outputTokens }
        };
        return JsonSerializer.Serialize(evt, AppJsonSerializerContext.Default.MessageDeltaEvent);
    }
}
