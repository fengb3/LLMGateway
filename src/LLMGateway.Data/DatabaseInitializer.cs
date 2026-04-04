using LLMGateway.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LLMGateway.Data;

public class DatabaseInitializer
{
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(AppDbContext db, ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Applies pending migrations. Optionally seeds providers if the table is empty.
    /// </summary>
    public async Task InitializeAsync(
        IReadOnlyList<SeedProvider>? seedProviders = null,
        CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);
        _logger.LogInformation("Database migrations applied");

        if (seedProviders is { Count: > 0 } && !await _db.Providers.AnyAsync(ct))
        {
            var now = DateTime.UtcNow;
            foreach (var p in seedProviders)
            {
                _db.Providers.Add(new ProviderEntity
                {
                    Name = p.Name,
                    BaseUrl = p.BaseUrl,
                    ApiKey = p.ApiKey,
                    Models = p.Models,
                    IsEnabled = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded {Count} providers from configuration", seedProviders.Count);
        }
    }
}

/// <summary>
/// Simple DTO used to pass seed data into DatabaseInitializer without coupling to configuration classes.
/// </summary>
public class SeedProvider
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public List<string> Models { get; set; } = [];
}
