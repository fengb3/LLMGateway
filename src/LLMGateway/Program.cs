using LLMGateway.Configuration;
using LLMGateway.Data;
using LLMGateway.Data.Repositories;
using LLMGateway.Endpoints;
using LLMGateway.Middleware;
using LLMGateway.Models;
using LLMGateway.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace LLMGateway;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.AddSerilog(loggerConfig =>
        {
            loggerConfig
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    theme: AnsiConsoleTheme.Code,
                    outputTemplate:
                        "{NewLine}╔─{Timestamp:HH:mm:ss} [{Level:u4}] ({SourceContext}) {NewLine}╚─{Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "logs/llmgateway-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    fileSizeLimitBytes: 20_000_000,
                    rollOnFileSizeLimit: true,
                    shared: true,
                    outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u4} {Message:lj}{NewLine}{Exception}");
        });

        // JSON serialization (source-generated)
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        // Gateway configuration
        builder.Services.Configure<GatewayOptions>(
            builder.Configuration.GetSection(GatewayOptions.SectionName));

        // Database – EF Core with SQLite
        var dbPath = builder.Configuration["Gateway:DatabasePath"] ?? "gateway.db";
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
        builder.Services.AddScoped<IProviderRepository, SqliteProviderRepository>();
        builder.Services.AddScoped<IApiKeyRepository, SqliteApiKeyRepository>();
        builder.Services.AddTransient<DatabaseInitializer>();

        // HTTP clients for proxying upstream requests
        builder.Services.AddHttpClient("upstream")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            })
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5);
            });

        builder.Services.AddHttpClient("upstream-streaming")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            })
            .ConfigureHttpClient(client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

        // Gateway services
        builder.Services.AddScoped<IProviderRouter, ProviderRouter>();
        builder.Services.AddScoped<ProxyService>();

        var app = builder.Build();

        // Initialize database: apply migrations and seed providers from config
        using (var scope = app.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            var gatewayOptions = scope.ServiceProvider.GetRequiredService<IOptions<GatewayOptions>>().Value;
            var seedProviders = gatewayOptions.Providers
                .Select(p => new SeedProvider
                {
                    Name = p.Name,
                    BaseUrl = p.BaseUrl,
                    ApiKey = p.ApiKey,
                    Models = p.Models
                })
                .ToList();
            await initializer.InitializeAsync(seedProviders);
        }

        // API key authentication
        app.UseMiddleware<ApiKeyMiddleware>();

        // ── Health ──────────────────────────────────────────────────────────
        app.MapGet("/health", () => TypedResults.Ok(new HealthResponse()));

        // ── Admin ───────────────────────────────────────────────────────────
        app.MapAdminProviderEndpoints();
        app.MapAdminApiKeyEndpoints();

        // ── OpenAI-compatible API ───────────────────────────────────────────
        app.MapModelEndpoints();
        app.MapChatCompletionEndpoints();

        // ── Anthropic-compatible API ────────────────────────────────────────
        app.MapAnthropicMessagesEndpoints();

        app.Run();
    }
}
