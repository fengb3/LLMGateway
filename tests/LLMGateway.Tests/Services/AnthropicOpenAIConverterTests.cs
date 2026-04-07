using System.Text.Json;
using FluentAssertions;
using LLMGateway.Models.Anthropic;
using LLMGateway.Models.OpenAI;
using LLMGateway.Services;
using Xunit;

namespace LLMGateway.Tests.Services;

public class AnthropicOpenAIConverterTests
{
    private static JsonElement Json(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    #region ToOpenAI - System Prompt

    [Fact]
    public void ToOpenAI_SystemPrompt_String_SetsSystemMessage()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            System = Json("\"You are helpful\""),
            Messages = [new AnthropicMessage { Role = "user", Content = Json("\"Hello\"") }]
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.Messages.Should().HaveCount(2);
        result.Messages[0].Role.Should().Be("system");
        result.Messages[0].Content.Value.GetString().Should().Be("You are helpful");
    }

    [Fact]
    public void ToOpenAI_SystemPrompt_Array_JoinsTextBlocks()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            System = Json("[{\"type\":\"text\",\"text\":\"Rule 1\"},{\"type\":\"text\",\"text\":\"Rule 2\"}]"),
            Messages = [new AnthropicMessage { Role = "user", Content = Json("\"Hi\"") }]
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.Messages[0].Content.Value.GetString().Should().Be("Rule 1\nRule 2");
    }

    [Fact]
    public void ToOpenAI_NoSystemPrompt_NoSystemMessage()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            Messages = [new AnthropicMessage { Role = "user", Content = Json("\"Hello\"") }]
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.Messages.Should().ContainSingle(m => m.Role != "system");
    }

    #endregion

    #region ToOpenAI - Message Content

    [Fact]
    public void ToOpenAI_SimpleStringContent_PreservesContent()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            Messages = [new AnthropicMessage { Role = "user", Content = Json("\"Hello world\"") }]
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.Messages.Should().ContainSingle();
        result.Messages[0].Content.Value.GetString().Should().Be("Hello world");
    }

    [Fact]
    public void ToOpenAI_UserToolResultBlock_ConvertsToToolMessage()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            Messages =
            [
                new AnthropicMessage
                {
                    Role = "user",
                    Content = Json("[{\"type\":\"tool_result\",\"tool_use_id\":\"tu_123\",\"content\":\"result text\"}]")
                }
            ]
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.Messages.Should().ContainSingle();
        result.Messages[0].Role.Should().Be("tool");
        result.Messages[0].ToolCallId.Should().Be("tu_123");
    }

    [Fact]
    public void ToOpenAI_AssistantToolUseBlock_ConvertsToToolCalls()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            Messages =
            [
                new AnthropicMessage
                {
                    Role = "assistant",
                    Content = Json("[{\"type\":\"text\",\"text\":\"Let me check\"},{\"type\":\"tool_use\",\"id\":\"tu_1\",\"name\":\"get_weather\",\"input\":{\"city\":\"SF\"}}]")
                }
            ]
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.Messages.Should().ContainSingle();
        var msg = result.Messages[0];
        msg.Role.Should().Be("assistant");
        msg.ToolCalls.Should().ContainSingle();
        msg.ToolCalls![0].Id.Should().Be("tu_1");
        msg.ToolCalls[0].Function.Name.Should().Be("get_weather");
        msg.ToolCalls[0].Function.Arguments.Should().Be("{\"city\":\"SF\"}");
    }

    [Fact]
    public void ToOpenAI_AssistantThinkingBlock_Skipped()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            Messages =
            [
                new AnthropicMessage
                {
                    Role = "assistant",
                    Content = Json("[{\"type\":\"thinking\",\"thinking\":\"hmm\"},{\"type\":\"text\",\"text\":\"answer\"}]")
                }
            ]
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.Messages.Should().ContainSingle();
        result.Messages[0].Content.Value.GetString().Should().Be("answer");
    }

    #endregion

    #region ToOpenAI - Tools and Parameters

    [Fact]
    public void ToOpenAI_Tools_ConvertedCorrectly()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            Messages = [new AnthropicMessage { Role = "user", Content = Json("\"Use tool\"") }],
            Tools =
            [
                new AnthropicTool
                {
                    Name = "search",
                    Description = "Search the web",
                    InputSchema = Json("{\"type\":\"object\",\"properties\":{\"q\":{\"type\":\"string\"}}}")
                }
            ]
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.Tools.Should().ContainSingle();
        result.Tools![0].Type.Should().Be("function");
        result.Tools[0].Function.Name.Should().Be("search");
        result.Tools[0].Function.Description.Should().Be("Search the web");
    }

    [Fact]
    public void ToOpenAI_ToolChoice_Auto_MapsToAuto()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            Messages = [new AnthropicMessage { Role = "user", Content = Json("\"Hi\"") }],
            ToolChoice = Json("{\"type\":\"auto\"}")
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.ToolChoice.Value.GetString().Should().Be("auto");
    }

    [Fact]
    public void ToOpenAI_ToolChoice_Any_MapsToRequired()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            Messages = [new AnthropicMessage { Role = "user", Content = Json("\"Hi\"") }],
            ToolChoice = Json("{\"type\":\"any\"}")
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.ToolChoice.Value.GetString().Should().Be("required");
    }

    [Fact]
    public void ToOpenAI_ToolChoice_Tool_MapsToFunctionObject()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            Messages = [new AnthropicMessage { Role = "user", Content = Json("\"Hi\"") }],
            ToolChoice = Json("{\"type\":\"tool\",\"name\":\"search\"}")
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.ToolChoice.Value.ValueKind.Should().Be(JsonValueKind.Object);
        result.ToolChoice.Value.GetProperty("type").GetString().Should().Be("function");
    }

    [Fact]
    public void ToOpenAI_StopSequences_MappedToStop()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 1024,
            Messages = [new AnthropicMessage { Role = "user", Content = Json("\"Hi\"") }],
            StopSequences = ["STOP", "END"]
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.Stop.Should().NotBeNull();
    }

    [Fact]
    public void ToOpenAI_Parameters_PassThrough()
    {
        var request = new AnthropicMessagesRequest
        {
            Model = "claude-3",
            MaxTokens = 2048,
            Temperature = 0.7,
            TopP = 0.9,
            Stream = true,
            Messages = [new AnthropicMessage { Role = "user", Content = Json("\"Hi\"") }]
        };

        var result = AnthropicOpenAIConverter.ToOpenAI(request);

        result.Model.Should().Be("claude-3");
        result.MaxTokens.Should().Be(2048);
        result.Temperature.Should().Be(0.7);
        result.TopP.Should().Be(0.9);
        result.Stream.Should().BeTrue();
    }

    #endregion

    #region ToAnthropicResponse

    [Fact]
    public void ToAnthropicResponse_TextOnly_CorrectBlock()
    {
        var response = new ChatCompletionResponse
        {
            Id = "chatcmpl-1",
            Choices =
            [
                new ChatChoice
                {
                    Message = new ChatMessage
                    {
                        Role = "assistant",
                        Content = Json("\"Hello from Claude\"")
                    },
                    FinishReason = "stop"
                }
            ],
            Usage = new UsageInfo { PromptTokens = 10, CompletionTokens = 5 }
        };

        var result = AnthropicOpenAIConverter.ToAnthropicResponse(response, "claude-3");

        result.Id.Should().StartWith("msg_");
        result.Model.Should().Be("claude-3");
        result.Content.Should().ContainSingle();
        result.Content[0].Should().BeOfType<AnthropicTextBlock>()
            .Which.Text.Should().Be("Hello from Claude");
        result.StopReason.Should().Be("end_turn");
        result.Usage!.InputTokens.Should().Be(10);
        result.Usage.OutputTokens.Should().Be(5);
    }

    [Fact]
    public void ToAnthropicResponse_ToolCalls_CorrectBlocks()
    {
        var response = new ChatCompletionResponse
        {
            Id = "chatcmpl-2",
            Choices =
            [
                new ChatChoice
                {
                    Message = new ChatMessage
                    {
                        Role = "assistant",
                        Content = Json("\"Let me search\""),
                        ToolCalls =
                        [
                            new OpenAIToolCall
                            {
                                Id = "call_1",
                                Type = "function",
                                Function = new OpenAIToolCallFunction
                                {
                                    Name = "search",
                                    Arguments = "{\"q\":\"test\"}"
                                }
                            }
                        ]
                    },
                    FinishReason = "tool_calls"
                }
            ]
        };

        var result = AnthropicOpenAIConverter.ToAnthropicResponse(response, "claude-3");

        result.Content.Should().HaveCount(2);
        result.Content[0].Should().BeOfType<AnthropicTextBlock>();
        var toolBlock = result.Content[1].Should().BeOfType<AnthropicToolUseBlock>().Subject;
        toolBlock.Name.Should().Be("search");
        result.StopReason.Should().Be("tool_use");
    }

    [Fact]
    public void ToAnthropicResponse_InvalidToolArguments_WrapsAsString()
    {
        var response = new ChatCompletionResponse
        {
            Id = "chatcmpl-3",
            Choices =
            [
                new ChatChoice
                {
                    Message = new ChatMessage
                    {
                        Role = "assistant",
                        Content = Json("\"\""),
                        ToolCalls =
                        [
                            new OpenAIToolCall
                            {
                                Id = "call_2",
                                Type = "function",
                                Function = new OpenAIToolCallFunction
                                {
                                    Name = "search",
                                    Arguments = "not valid json{{{"
                                }
                            }
                        ]
                    },
                    FinishReason = "tool_calls"
                }
            ]
        };

        var result = AnthropicOpenAIConverter.ToAnthropicResponse(response, "claude-3");

        var toolBlock = result.Content.OfType<AnthropicToolUseBlock>().Single();
        toolBlock.Input.Should().NotBeNull();
    }

    [Fact]
    public void ToAnthropicResponse_EmptyChoices_ReturnsEmptyTextBlock()
    {
        var response = new ChatCompletionResponse
        {
            Id = "chatcmpl-4",
            Choices = []
        };

        var result = AnthropicOpenAIConverter.ToAnthropicResponse(response, "claude-3");

        result.Content.Should().ContainSingle();
        result.Content[0].Should().BeOfType<AnthropicTextBlock>()
            .Which.Text.Should().Be("");
    }

    #endregion

    #region ConvertFinishReason

    [Theory]
    [InlineData("stop", "end_turn")]
    [InlineData("length", "max_tokens")]
    [InlineData("tool_calls", "tool_use")]
    [InlineData("content_filter", "end_turn")]
    public void ConvertFinishReason_KnownValues_MapCorrectly(string input, string expected)
    {
        AnthropicOpenAIConverter.ConvertFinishReason(input).Should().Be(expected);
    }

    [Fact]
    public void ConvertFinishReason_Null_ReturnsNull()
    {
        AnthropicOpenAIConverter.ConvertFinishReason(null).Should().BeNull();
    }

    [Fact]
    public void ConvertFinishReason_Unknown_Passthrough()
    {
        AnthropicOpenAIConverter.ConvertFinishReason("custom_reason").Should().Be("custom_reason");
    }

    #endregion
}
