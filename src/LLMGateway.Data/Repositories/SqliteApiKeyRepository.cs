using LLMGateway.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Data.Repositories;

public class SqliteApiKeyRepository : IApiKeyRepository
{
    private readonly AppDbContext _db;

    public SqliteApiKeyRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApiKeyEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.ApiKeys.OrderBy(k => k.Id).ToListAsync(ct);
    }

    public async Task<ApiKeyEntity?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.ApiKeys.FindAsync([id], ct);
    }

    public async Task<ApiKeyEntity?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default)
    {
        return await _db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);
    }

    public async Task<ApiKeyEntity> AddAsync(ApiKeyEntity entity, CancellationToken ct = default)
    {
        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(ApiKeyEntity entity, CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _db.ApiKeys.Where(k => k.Id == id).ExecuteDeleteAsync(ct);
    }
}
