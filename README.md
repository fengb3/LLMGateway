# LLMGateway

An **ASP.NET Core** LLM gateway that accepts both **OpenAI** and **Anthropic** compatible requests and routes them to configured upstream providers based on model name.

## Features

- **Model-based routing** – map any model name to any OpenAI-compatible provider
- **Dual API compatibility** – OpenAI (`/v1/chat/completions`) and Anthropic (`/v1/messages`) formats, with automatic conversion
- **Dual API key authentication** – admin keys (config-based) and user keys (DB-managed)
- **Dynamic API key management** – generate / revoke user API keys via the admin API
- **Streaming support** – server-sent events (SSE) proxied transparently for both API formats
- **Multi-project architecture** – separated into Web, Data, Models, and Tests layers

## Endpoints

### Public

| Method | Path      | Auth | Description  |
|--------|-----------|------|--------------|
| `GET`  | `/health` | None | Health check |

### User API (requires user API key)

| Method | Path                   | Description                                              |
|--------|------------------------|----------------------------------------------------------|
| `GET`  | `/v1/models`           | List all configured models                               |
| `POST` | `/v1/chat/completions` | OpenAI-compatible chat completions (streaming & non-streaming) |
| `POST` | `/v1/messages`         | Anthropic-compatible messages API (streaming & non-streaming) |

### Admin API (requires admin API key)

| Method   | Path                    | Description                            |
|----------|-------------------------|----------------------------------------|
| `GET`    | `/admin/providers`      | List all providers                     |
| `GET`    | `/admin/providers/{id}` | Get a provider                         |
| `POST`   | `/admin/providers`      | Create a provider                      |
| `PUT`    | `/admin/providers/{id}` | Update a provider                      |
| `DELETE` | `/admin/providers/{id}` | Delete a provider                      |
| `GET`    | `/admin/apikeys`        | List all user API keys                 |
| `GET`    | `/admin/apikeys/{id}`   | Get an API key                         |
| `POST`   | `/admin/apikeys`        | Generate a new user API key            |
| `PUT`    | `/admin/apikeys/{id}`   | Update an API key (name/active/expiry) |
| `DELETE` | `/admin/apikeys/{id}`   | Delete an API key                      |

## Configuration

Edit `appsettings.json` (or use environment variables / user secrets):

```json
{
  "Gateway": {
    "DatabasePath": "gateway.db",
    "Providers": [
      {
        "Name": "DeepSeek",
        "BaseUrl": "https://api.deepseek.com",
        "ApiKey": "sk-YOUR_DEEPSEEK_KEY",
        "Models": ["deepseek-chat", "deepseek-reasoner"]
      }
    ],
    "AdminApiKeys": [
      {
        "Key": "sk-admin-change-me",
        "Name": "default-admin",
        "IsActive": true
      }
    ]
  }
}
```

- **Providers** are seeded into the database on first run (if the table is empty) and can then be managed via the admin API.
- **AdminApiKeys** are used to authenticate `/admin/*` requests. Configure them in `appsettings.json` or via environment variables.
- **User API keys** are managed entirely through the admin API (`POST /admin/apikeys`). The plaintext key is returned only once at creation time.

## Running

```bash
cd src/LLMGateway
dotnet run
```

The server starts at `http://localhost:5273` by default.

## Testing

```bash
# Run all tests
dotnet test tests/LLMGateway.Tests

# Run with verbose output
dotnet test tests/LLMGateway.Tests --verbosity normal
```

The test project uses **xUnit**, **Moq**, and **FluentAssertions**. It includes:
- Unit tests for the Anthropic↔OpenAI format converters
- Unit tests for `ProviderRouter` and `ProxyService`
- Middleware tests for API key authentication
- SQLite in-memory integration tests for repositories

## Quick Start

```bash
# 1. Generate a user API key (using the admin key)
curl -X POST http://localhost:5273/admin/apikeys \
  -H "Authorization: Bearer sk-admin-change-me" \
  -H "Content-Type: application/json" \
  -d '{"name": "my-app"}'
# Response includes "key": "sk-gw-..." — save it, it won't be shown again

# 2. List available models
curl http://localhost:5273/v1/models \
  -H "Authorization: Bearer sk-gw-YOUR_KEY"

# 3. OpenAI-compatible chat completion
curl http://localhost:5273/v1/chat/completions \
  -H "Authorization: Bearer sk-gw-YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "deepseek-chat",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'

# 4. Anthropic-compatible messages
curl http://localhost:5273/v1/messages \
  -H "Authorization: Bearer sk-gw-YOUR_KEY" \
  -H "Content-Type: application/json" \
  -H "anthropic-version: 2023-06-01" \
  -d '{
    "model": "deepseek-chat",
    "max_tokens": 1024,
    "messages": [{"role": "user", "content": "Hello!"}]
  }'

# 5. Streaming chat completion (OpenAI)
curl http://localhost:5273/v1/chat/completions \
  -H "Authorization: Bearer sk-gw-YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "deepseek-chat",
    "messages": [{"role": "user", "content": "Hello!"}],
    "stream": true
  }'
```

## Project Structure

```
LLMGateway.slnx
src/
  LLMGateway.Data/                # Database layer (EF Core / SQLite)
    Entities/
      ProviderEntity.cs
      ApiKeyEntity.cs
    Repositories/
      IProviderRepository.cs
      SqliteProviderRepository.cs
      IApiKeyRepository.cs
      SqliteApiKeyRepository.cs
    AppDbContext.cs
    DatabaseInitializer.cs

  LLMGateway.Models/              # Request/Response DTOs
    Admin/
      CreateProviderRequest.cs
      UpdateProviderRequest.cs
      ProviderResponse.cs
      CreateApiKeyRequest.cs
      UpdateApiKeyRequest.cs
      ApiKeyResponse.cs
      ApiKeyCreatedResponse.cs
    OpenAI/
      ChatCompletionRequest.cs
      ChatCompletionResponse.cs
      ErrorResponse.cs
      ModelListResponse.cs
      OpenAITool.cs
      OpenAIToolCall.cs
    Anthropic/
      AnthropicMessagesRequest.cs
      AnthropicMessagesResponse.cs
      AnthropicContentBlocks.cs
      AnthropicSseEvents.cs
      AnthropicTool.cs
      AnthropicErrorResponse.cs
    HealthResponse.cs

  LLMGateway/                     # Web host (ASP.NET Core)
    Configuration/
      GatewayOptions.cs
      ProviderOptions.cs
    Endpoints/
      AdminProviderEndpoints.cs
      AdminApiKeyEndpoints.cs
      ChatCompletionEndpoints.cs
      ModelEndpoints.cs
      AnthropicMessagesEndpoints.cs
    Middleware/
      ApiKeyMiddleware.cs
    Services/
      IProviderRouter.cs
      ProviderRouter.cs
      ProxyService.cs
      AnthropicOpenAIConverter.cs
      AnthropicSseConverter.cs
    AppJsonSerializerContext.cs
    Program.cs
    appsettings.json

tests/
  LLMGateway.Tests/               # Unit & integration tests
    Services/
      AnthropicOpenAIConverterTests.cs
      AnthropicSseConverterTests.cs
      ProviderRouterTests.cs
      ProxyServiceTests.cs
    Middleware/
      ApiKeyMiddlewareTests.cs
    Data/
      SqliteTestBase.cs
      SqliteProviderRepositoryTests.cs
      SqliteApiKeyRepositoryTests.cs
```
