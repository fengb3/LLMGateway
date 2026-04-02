# LLMGateway

An **ASP.NET Core** LLM (Large Language Model) gateway compiled with **Native AOT**. It accepts OpenAI-format requests and routes them to the configured third-party LLM providers based on model name.

## Features

- 🔀 **Model-based routing** – map any model name to any OpenAI-compatible provider
- 🔑 **API key authentication** – issue gateway keys to control access
- ⚡ **Native AOT** – compiled to a native binary with fast startup and low memory usage
- 🌊 **Streaming support** – server-sent events (SSE) are proxied transparently
- 🔌 **OpenAI-compatible API** – works as a drop-in replacement for the OpenAI endpoint

## Supported Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET`  | `/health` | Health check (no auth required) |
| `GET`  | `/v1/models` | List all configured models |
| `POST` | `/v1/chat/completions` | Chat completions (streaming & non-streaming) |

## Configuration

Edit `appsettings.json` (or use environment variables / secrets):

```json
{
  "Gateway": {
    "Providers": [
      {
        "Name": "OpenAI",
        "BaseUrl": "https://api.openai.com",
        "ApiKey": "sk-YOUR_OPENAI_KEY",
        "Models": ["gpt-4o", "gpt-4o-mini", "gpt-3.5-turbo"]
      },
      {
        "Name": "DeepSeek",
        "BaseUrl": "https://api.deepseek.com",
        "ApiKey": "sk-YOUR_DEEPSEEK_KEY",
        "Models": ["deepseek-chat", "deepseek-reasoner"]
      }
    ],
    "ApiKeys": [
      {
        "Key": "sk-gateway-your-secret-key",
        "Name": "My App",
        "IsActive": true
      }
    ]
  }
}
```

### Providers

Each provider requires:

| Field | Description |
|-------|-------------|
| `Name` | Display name |
| `BaseUrl` | Base URL of the OpenAI-compatible API (without `/v1/...`) |
| `ApiKey` | API key for the provider |
| `Models` | List of model names handled by this provider |

### Gateway API Keys

Clients must send a gateway API key as a Bearer token:

```
Authorization: Bearer sk-gateway-your-secret-key
```

## Running

```bash
cd src/LLMGateway
dotnet run
```

## Building (Native AOT)

```bash
cd src/LLMGateway
dotnet publish -r linux-x64 -c Release
```

The native binary will be at `bin/Release/net9.0/linux-x64/publish/LLMGateway`.

## Usage Example

```bash
# List available models
curl http://localhost:5273/v1/models \
  -H "Authorization: Bearer sk-gateway-your-secret-key"

# Chat completion (non-streaming)
curl http://localhost:5273/v1/chat/completions \
  -H "Authorization: Bearer sk-gateway-your-secret-key" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-4o",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'

# Chat completion (streaming)
curl http://localhost:5273/v1/chat/completions \
  -H "Authorization: Bearer sk-gateway-your-secret-key" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "deepseek-chat",
    "messages": [{"role": "user", "content": "Hello!"}],
    "stream": true
  }'
```

## Project Structure

```
src/LLMGateway/
├── Configuration/
│   ├── GatewayOptions.cs        # Gateway + API key config
│   └── ProviderOptions.cs       # Provider (BaseUrl, ApiKey, Models)
├── Models/
│   ├── OpenAI/
│   │   ├── ChatCompletionRequest.cs
│   │   ├── ChatCompletionResponse.cs
│   │   ├── ErrorResponse.cs
│   │   └── ModelListResponse.cs
│   └── HealthResponse.cs
├── Middleware/
│   └── ApiKeyMiddleware.cs      # Bearer token validation
├── Services/
│   ├── IProviderRouter.cs
│   ├── ProviderRouter.cs        # Model → provider routing
│   └── ProxyService.cs          # HTTP proxy to upstream
├── AppJsonSerializerContext.cs  # AOT JSON source generation
├── Program.cs                   # Startup + route definitions
└── appsettings.json             # Default configuration
```
