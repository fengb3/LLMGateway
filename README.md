# LLMGateway

An **ASP.NET Core** LLM gateway that accepts OpenAI-compatible requests and routes them to configured upstream providers
based on model name.

## Features

- **Model-based routing** – map any model name to any OpenAI-compatible provider
- **Dual API key authentication** – admin keys (config-based) and user keys (DB-managed)
- **Dynamic API key management** – generate / revoke user API keys via the admin API
- **Streaming support** – server-sent events (SSE) proxied transparently
- **OpenAI-compatible API** – drop-in replacement for the OpenAI endpoint
- **Multi-project architecture** – separated into Data, Models, and Web layers

## Endpoints

### Public

| Method | Path      | Auth | Description  |
|--------|-----------|------|--------------|
| `GET`  | `/health` | None | Health check |

### User API (requires user API key)

| Method | Path                   | Description                                  |
|--------|------------------------|----------------------------------------------|
| `GET`  | `/v1/models`           | List all configured models                   |
| `POST` | `/v1/chat/completions` | Chat completions (streaming & non-streaming) |

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

- **Providers** are seeded into the database on first run (if the table is empty) and can then be managed via the admin
  API.
- **AdminApiKeys** are used to authenticate `/admin/*` requests. Configure them in `appsettings.json` or via environment
  variables.
- **User API keys** are managed entirely through the admin API (`POST /admin/apikeys`). The plaintext key is returned
  only once at creation time.

## Running

```bash
cd src/LLMGateway
dotnet run
```

The server starts at `http://localhost:5273` by default.

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

# 3. Chat completion
curl http://localhost:5273/v1/chat/completions \
  -H "Authorization: Bearer sk-gw-YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "deepseek-chat",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'

# 4. Streaming chat completion
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
  LLMGateway.Data/                # Database layer (class library)
    Entities/
      ProviderEntity.cs
      ApiKeyEntity.cs
    Repositories/
      IProviderRepository.cs
      SqliteProviderRepository.cs
      IApiKeyRepository.cs
      SqliteApiKeyRepository.cs
    Migrations/
    AppDbContext.cs
    DatabaseInitializer.cs

  LLMGateway.Models/              # Request/Response DTOs (class library)
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
    Middleware/
      ApiKeyMiddleware.cs
    Services/
      IProviderRouter.cs
      ProviderRouter.cs
      ProxyService.cs
    AppJsonSerializerContext.cs
    Program.cs
    appsettings.json
```
