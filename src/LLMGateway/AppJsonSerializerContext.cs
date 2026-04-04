using System.Text.Json;
using System.Text.Json.Serialization;
using LLMGateway.Configuration;
using LLMGateway.Models;
using LLMGateway.Models.Admin;
using LLMGateway.Models.Anthropic;
using LLMGateway.Models.OpenAI;

namespace LLMGateway;

[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatCompletionResponse))]
[JsonSerializable(typeof(ModelListResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(List<ModelInfo>))]
[JsonSerializable(typeof(GatewayOptions))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(ProviderResponse))]
[JsonSerializable(typeof(List<ProviderResponse>))]
[JsonSerializable(typeof(CreateProviderRequest))]
[JsonSerializable(typeof(UpdateProviderRequest))]
[JsonSerializable(typeof(ApiKeyResponse))]
[JsonSerializable(typeof(List<ApiKeyResponse>))]
[JsonSerializable(typeof(ApiKeyCreatedResponse))]
[JsonSerializable(typeof(CreateApiKeyRequest))]
[JsonSerializable(typeof(UpdateApiKeyRequest))]
// OpenAI tool support
[JsonSerializable(typeof(OpenAITool))]
[JsonSerializable(typeof(List<OpenAITool>))]
[JsonSerializable(typeof(OpenAIFunction))]
[JsonSerializable(typeof(OpenAIToolCall))]
[JsonSerializable(typeof(List<OpenAIToolCall>))]
[JsonSerializable(typeof(OpenAIToolCallFunction))]
// Anthropic models
[JsonSerializable(typeof(AnthropicMessagesRequest))]
[JsonSerializable(typeof(AnthropicMessagesResponse))]
[JsonSerializable(typeof(AnthropicErrorResponse))]
[JsonSerializable(typeof(AnthropicContentBlock))]
[JsonSerializable(typeof(AnthropicTextBlock))]
[JsonSerializable(typeof(AnthropicToolUseBlock))]
[JsonSerializable(typeof(AnthropicToolResultBlock))]
[JsonSerializable(typeof(AnthropicThinkingBlock))]
[JsonSerializable(typeof(AnthropicTool))]
[JsonSerializable(typeof(List<AnthropicTool>))]
[JsonSerializable(typeof(AnthropicUsage))]
[JsonSerializable(typeof(AnthropicErrorDetail))]
[JsonSerializable(typeof(List<AnthropicContentBlock>))]
// Anthropic SSE events
[JsonSerializable(typeof(MessageStartEvent))]
[JsonSerializable(typeof(ContentBlockStartEvent))]
[JsonSerializable(typeof(ContentBlockDeltaEvent))]
[JsonSerializable(typeof(ContentBlockStopEvent))]
[JsonSerializable(typeof(MessageDeltaEvent))]
[JsonSerializable(typeof(MessageDeltaData))]
[JsonSerializable(typeof(MessageStopEvent))]
[JsonSerializable(typeof(PingEvent))]
[JsonSerializable(typeof(AnthropicContentDelta))]
[JsonSerializable(typeof(TextDelta))]
[JsonSerializable(typeof(InputJsonDelta))]
[JsonSerializable(typeof(ThinkingDelta))]
[JsonSerializable(typeof(SignatureDelta))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
