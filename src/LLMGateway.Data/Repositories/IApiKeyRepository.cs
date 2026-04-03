using LLMGateway.Data.Entities;

namespace LLMGateway.Data.Repositories;

public interface IApiKeyRepository
{
    Task<IReadOnlyList<ApiKeyEntity>> GetAllAsync(CancellationToken ct = default);
    Task<ApiKeyEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ApiKeyEntity?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default);
    Task<ApiKeyEntity> AddAsync(ApiKeyEntity entity, CancellationToken ct = default);
    Task UpdateAsync(ApiKeyEntity entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
