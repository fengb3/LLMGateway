using LLMGateway.Data.Entities;

namespace LLMGateway.Data.Repositories;

public interface IProviderRepository
{
    Task<IReadOnlyList<ProviderEntity>> GetAllAsync(CancellationToken ct = default);
    Task<ProviderEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProviderEntity?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<ProviderEntity> AddAsync(ProviderEntity entity, CancellationToken ct = default);
    Task UpdateAsync(ProviderEntity entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<ProviderEntity?> GetByModelNameAsync(string modelName, CancellationToken ct = default);
}
