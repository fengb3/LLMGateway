using System.Text.Json.Serialization;
using LLMGateway.Configuration;
using LLMGateway.Models;
using LLMGateway.Models.OpenAI;

namespace LLMGateway;

/// <summary>
/// AOT-compatible JSON serializer context.
/// </summary>
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatCompletionResponse))]
[JsonSerializable(typeof(ModelListResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(List<ModelInfo>))]
[JsonSerializable(typeof(GatewayOptions))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(object))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
