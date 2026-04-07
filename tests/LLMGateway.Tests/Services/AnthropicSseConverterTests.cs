using System.Text;
using FluentAssertions;
using LLMGateway.Services;
using Xunit;

namespace LLMGateway.Tests.Services;

public class AnthropicSseConverterTests
{
    private static MemoryStream CreateSseStream(params string[] lines)
    {
        var content = string.Join("\n", lines) + "\n";
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private static async Task<string> ConvertToString(string sseInput)
    {
        var input = new MemoryStream(Encoding.UTF8.GetBytes(sseInput));
        var output = new MemoryStream();

        await AnthropicSseConverter.ConvertStreamAsync(input, output, "claude-3", "req-123", CancellationToken.None);

        output.Position = 0;
        using var reader = new StreamReader(output);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task SingleTextDelta_ProducesCorrectEventSequence()
    {
        var sseInput =
            "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Hello\"},\"finish_reason\":null}]}\n\n" +
            "data: [DONE]\n\n";

        var result = await ConvertToString(sseInput);

        result.Should().Contain("event: message_start");
        result.Should().Contain("event: ping");
        result.Should().Contain("event: content_block_start");
        result.Should().Contain("event: content_block_delta");
        result.Should().Contain("event: content_block_stop");
        result.Should().Contain("event: message_delta");
        result.Should().Contain("event: message_stop");
    }

    [Fact]
    public async Task MultipleTextDeltas_AccumulateInSameBlock()
    {
        var sseInput =
            "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hello\"},\"finish_reason\":null}]}\n\n" +
            "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\" world\"},\"finish_reason\":\"stop\"}]}\n\n" +
            "data: [DONE]\n\n";

        var result = await ConvertToString(sseInput);

        // Should have 2 content_block_delta events (one per chunk)
        var deltaCount = CountOccurrences(result, "event: content_block_delta");
        deltaCount.Should().Be(2);

        // Should only have 1 content_block_start and 1 content_block_stop
        var startCount = CountOccurrences(result, "event: content_block_start");
        var stopCount = CountOccurrences(result, "event: content_block_stop");
        startCount.Should().Be(1);
        stopCount.Should().Be(1);
    }

    [Fact]
    public async Task ToolCallDelta_ProducesToolUseBlock()
    {
        var sseInput =
            "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"index\":0,\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"search\",\"arguments\":\"\"}}]},\"finish_reason\":null}]}\n\n" +
            "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"{\\\"q\\\":\\\"test\\\"}\"}}]},\"finish_reason\":\"tool_calls\"}]}\n\n" +
            "data: [DONE]\n\n";

        var result = await ConvertToString(sseInput);

        result.Should().Contain("tool_use");
        result.Should().Contain("search");
    }

    [Fact]
    public async Task ThinkingDelta_ProducesThinkingBlock()
    {
        var sseInput =
            "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"delta\":{\"reasoning_content\":\"Let me think\"},\"finish_reason\":null}]}\n\n" +
            "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"The answer is 42\"},\"finish_reason\":\"stop\"}]}\n\n" +
            "data: [DONE]\n\n";

        var result = await ConvertToString(sseInput);

        result.Should().Contain("thinking");
        result.Should().Contain("Let me think");
        result.Should().Contain("The answer is 42");
    }

    [Fact]
    public async Task DoneSentinel_TriggersMessageStop()
    {
        var sseInput =
            "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hi\"},\"finish_reason\":\"stop\"}]}\n\n" +
            "data: [DONE]\n\n";

        var result = await ConvertToString(sseInput);

        result.Should().Contain("event: message_delta");
        result.Should().Contain("event: message_stop");
        result.Should().Contain("end_turn");
    }

    [Fact]
    public async Task Usage_CapturedFromChunks()
    {
        var sseInput =
            "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hi\"},\"finish_reason\":null}]}\n\n" +
            "data: {\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":100,\"completion_tokens\":20,\"total_tokens\":120}}\n\n" +
            "data: [DONE]\n\n";

        var result = await ConvertToString(sseInput);

        // Usage info should be in message_delta
        result.Should().Contain("output_tokens");
    }

    [Fact]
    public async Task EmptyStream_StillProducesStartAndStop()
    {
        var sseInput = "data: [DONE]\n\n";

        var result = await ConvertToString(sseInput);

        result.Should().Contain("event: message_start");
        result.Should().Contain("event: message_stop");
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
