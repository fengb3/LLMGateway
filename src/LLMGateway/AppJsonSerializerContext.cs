using System.Text.Json;
using System.Text.Json.Serialization;
using LLMGateway.Configuration;
using LLMGateway.Models;
using LLMGateway.Models.Admin;
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
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
