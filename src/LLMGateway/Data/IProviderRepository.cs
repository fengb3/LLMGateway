namespace LLMGateway.Data;

public interface IProviderRepository : IAsyncDisposable
{
    Task<IReadOnlyList<ProviderEntity>> GetAllAsync(CancellationToken ct = default);
    Task<ProviderEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProviderEntity?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<ProviderEntity> AddAsync(ProviderEntity entity, CancellationToken ct = default);
    Task UpdateAsync(ProviderEntity entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<ProviderEntity?> GetByModelNameAsync(string modelName, CancellationToken ct = default);
}
