using System.Text.Json;
using LLMGateway.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LLMGateway.Data;

public class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IOptionsSnapshot<GatewayOptions> _options;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        SqliteConnectionFactory connectionFactory,
        IOptionsSnapshot<GatewayOptions> options,
        ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await CreateSchemaAsync(connection, ct);
        await SeedFromConfigAsync(connection, ct);
    }

    private async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Providers (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL,
                BaseUrl     TEXT NOT NULL,
                ApiKey      TEXT NOT NULL,
                ModelsJson  TEXT NOT NULL DEFAULT '[]',
                IsEnabled   INTEGER NOT NULL DEFAULT 1,
                CreatedAt   TEXT NOT NULL DEFAULT (datetime('now')),
                UpdatedAt   TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Providers_Name ON Providers(Name);";
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("Database schema initialized");
    }

    private async Task SeedFromConfigAsync(SqliteConnection connection, CancellationToken ct)
    {
        // Check if table already has data
        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Providers";
        var count = Convert.ToInt64(await countCmd.ExecuteScalarAsync(ct));
        if (count > 0)
            return;

        var providers = _options.Value.Providers;
        if (providers.Count == 0)
            return;

        var now = DateTime.UtcNow.ToString("O");
        foreach (var provider in providers)
        {
            using var cmd = connection.CreateCommand();
            var modelsJson = JsonSerializer.Serialize(provider.Models, AppJsonSerializerContext.Default.ListString);
            cmd.CommandText = @"
                INSERT INTO Providers (Name, BaseUrl, ApiKey, ModelsJson, IsEnabled, CreatedAt, UpdatedAt)
                VALUES (@name, @baseUrl, @apiKey, @modelsJson, 1, @createdAt, @updatedAt)";
            cmd.Parameters.AddWithValue("@name", provider.Name);
            cmd.Parameters.AddWithValue("@baseUrl", provider.BaseUrl);
            cmd.Parameters.AddWithValue("@apiKey", provider.ApiKey);
            cmd.Parameters.AddWithValue("@modelsJson", modelsJson);
            cmd.Parameters.AddWithValue("@createdAt", now);
            cmd.Parameters.AddWithValue("@updatedAt", now);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        _logger.LogInformation("Seeded {Count} providers from configuration", providers.Count);
    }
}
