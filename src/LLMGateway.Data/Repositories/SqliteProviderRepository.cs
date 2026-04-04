using LLMGateway.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Data.Repositories;

public class SqliteProviderRepository : IProviderRepository
{
    private readonly AppDbContext _db;

    public SqliteProviderRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProviderEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Providers.OrderBy(p => p.Id).ToListAsync(ct);
    }

    public async Task<ProviderEntity?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Providers.FindAsync([id], ct);
    }

    public async Task<ProviderEntity?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _db.Providers.FirstOrDefaultAsync(p => p.Name == name, ct);
    }

    public async Task<ProviderEntity> AddAsync(ProviderEntity entity, CancellationToken ct = default)
    {
        _db.Providers.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(ProviderEntity entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _db.Providers.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<ProviderEntity?> GetByModelNameAsync(string modelName, CancellationToken ct = default)
    {
        return await _db.Providers
            .FromSql($@"
                SELECT * FROM Providers
                WHERE IsEnabled = 1 AND Id IN (
                    SELECT p.Id FROM Providers p, json_each(p.ModelsJson)
                    WHERE json_each.value = {modelName}
                    LIMIT 1
                )")
            .FirstOrDefaultAsync(ct);
    }
}
