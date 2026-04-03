using Microsoft.Data.Sqlite;

namespace LLMGateway.Data;

public class SqliteProviderRepository : IProviderRepository
{
    private readonly SqliteConnection _connection;

    public SqliteProviderRepository(SqliteConnectionFactory connectionFactory)
    {
        _connection = connectionFactory.CreateConnection();
        _connection.Open();
    }

    public async Task<IReadOnlyList<ProviderEntity>> GetAllAsync(CancellationToken ct = default)
    {
        var results = new List<ProviderEntity>();
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, BaseUrl, ApiKey, ModelsJson, IsEnabled, CreatedAt, UpdatedAt FROM Providers ORDER BY Id";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadEntity(reader));
        }
        return results;
    }

    public async Task<ProviderEntity?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, BaseUrl, ApiKey, ModelsJson, IsEnabled, CreatedAt, UpdatedAt FROM Providers WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadEntity(reader);
        return null;
    }

    public async Task<ProviderEntity?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, BaseUrl, ApiKey, ModelsJson, IsEnabled, CreatedAt, UpdatedAt FROM Providers WHERE Name = @name";
        cmd.Parameters.AddWithValue("@name", name);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadEntity(reader);
        return null;
    }

    public async Task<ProviderEntity> AddAsync(ProviderEntity entity, CancellationToken ct = default)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Providers (Name, BaseUrl, ApiKey, ModelsJson, IsEnabled, CreatedAt, UpdatedAt)
            VALUES (@name, @baseUrl, @apiKey, @modelsJson, @isEnabled, @createdAt, @updatedAt);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", entity.Name);
        cmd.Parameters.AddWithValue("@baseUrl", entity.BaseUrl);
        cmd.Parameters.AddWithValue("@apiKey", entity.ApiKey);
        cmd.Parameters.AddWithValue("@modelsJson", entity.ModelsJson);
        cmd.Parameters.AddWithValue("@isEnabled", entity.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@createdAt", entity.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@updatedAt", entity.UpdatedAt.ToString("O"));

        var id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
        entity.Id = (int)id;
        return entity;
    }

    public async Task UpdateAsync(ProviderEntity entity, CancellationToken ct = default)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Providers
            SET Name = @name, BaseUrl = @baseUrl, ApiKey = @apiKey,
                ModelsJson = @modelsJson, IsEnabled = @isEnabled, UpdatedAt = @updatedAt
            WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", entity.Id);
        cmd.Parameters.AddWithValue("@name", entity.Name);
        cmd.Parameters.AddWithValue("@baseUrl", entity.BaseUrl);
        cmd.Parameters.AddWithValue("@apiKey", entity.ApiKey);
        cmd.Parameters.AddWithValue("@modelsJson", entity.ModelsJson);
        cmd.Parameters.AddWithValue("@isEnabled", entity.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Providers WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ProviderEntity?> GetByModelNameAsync(string modelName, CancellationToken ct = default)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Name, BaseUrl, ApiKey, ModelsJson, IsEnabled, CreatedAt, UpdatedAt
            FROM Providers
            WHERE IsEnabled = 1 AND Id IN (
                SELECT p.Id FROM Providers p, json_each(p.ModelsJson)
                WHERE json_each.value = @modelName
                LIMIT 1
            )";
        cmd.Parameters.AddWithValue("@modelName", modelName);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadEntity(reader);
        return null;
    }

    private static ProviderEntity ReadEntity(SqliteDataReader reader)
    {
        return new ProviderEntity
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            BaseUrl = reader.GetString(2),
            ApiKey = reader.GetString(3),
            ModelsJson = reader.GetString(4),
            IsEnabled = reader.GetInt32(5) != 0,
            CreatedAt = reader.GetDateTime(6),
            UpdatedAt = reader.GetDateTime(7)
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
