# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build the entire solution
dotnet build LLMGateway.slnx

# Run the gateway (from repo root)
dotnet run --project src/LLMGateway

# Run with watch for hot reload during development
dotnet watch --project src/LLMGateway

# Run all tests
dotnet test tests/LLMGateway.Tests

# Run a specific test class
dotnet test tests/LLMGateway.Tests --filter "FullyQualifiedName~AnthropicOpenAIConverterTests"
```

The test project uses xUnit + Moq + FluentAssertions.

## Architecture

This is an ASP.NET Core 9.0 LLM gateway that proxies requests to upstream providers. It uses **minimal APIs** (not controllers).

### Four-Project Solution

- **`src/LLMGateway`** – Web host. Contains endpoints, middleware, services, and `Program.cs` wiring. This is the startup project.
- **`src/LLMGateway.Data`** – Data access layer with EF Core (SQLite). Entities, repositories, `AppDbContext`.
- **`src/LLMGateway.Models`** – Pure DTOs for admin, OpenAI-compatible, and Anthropic-compatible APIs. No dependencies on other projects.
- **`tests/LLMGateway.Tests`** – Unit tests covering converters, routing, middleware, data layer, and proxy service.

### Request Flow

1. `ApiKeyMiddleware` authenticates every request (except `/health`):
   - `/admin/*` routes → validated against `AdminApiKeys` from config (constant-time comparison)
   - All other routes → validated against `ApiKeys` table in DB (SHA256-hashed)
2. Endpoints use `IProviderRouter` to resolve a model name to an upstream provider (URL + API key) from the database.
3. `ProxyService` forwards the request to the upstream provider. It uses two named HTTP clients:
   - `"upstream"` – 5-minute timeout for standard requests
   - `"upstream-streaming"` – infinite timeout for SSE streaming

### Dual API Compatibility

The gateway supports both OpenAI and Anthropic request formats:
- `/v1/chat/completions` – OpenAI-compatible chat completions
- `/v1/messages` – Anthropic-compatible messages API
- `AnthropicOpenAIConverter` and `AnthropicSseConverter` translate between formats

### Endpoint Registration

Each endpoint group is defined as a static class with a `Map*Endpoints(this WebApplication)` extension method, then registered in `Program.cs`:
- `AdminProviderEndpoints` → `/admin/providers`
- `AdminApiKeyEndpoints` → `/admin/apikeys`
- `ChatCompletionEndpoints` → `/v1/chat/completions`
- `ModelEndpoints` → `/v1/models`
- `AnthropicMessagesEndpoints` → `/v1/messages`

### Database

- SQLite via EF Core, path configured in `Gateway:DatabasePath` (defaults to `gateway.db`)
- Providers are seeded from `appsettings.json` on first run (only when table is empty), then managed via admin API
- User API keys are generated/managed entirely through the admin API; plaintext is returned only once at creation

### JSON Serialization

Uses source-generated JSON via `AppJsonSerializerContext` for performance. New DTOs added to `LLMGateway.Models` may need to be registered there. It is `internal` — `LLMGateway.Tests` has access via `InternalsVisibleTo`.

## Code Style

- `Directory.Build.props` enforces `EnforceCodeStyleInBuild>true` and `AnalysisLevel>latest`
- `.editorconfig` defines comprehensive C# formatting rules (expression-bodied properties, pattern matching, etc.)
- Target framework: .NET 9.0 across all projects
