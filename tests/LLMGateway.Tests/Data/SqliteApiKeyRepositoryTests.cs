using FluentAssertions;
using LLMGateway.Data.Entities;
using LLMGateway.Data.Repositories;
using Xunit;

namespace LLMGateway.Tests.Data;

public class SqliteApiKeyRepositoryTests : SqliteTestBase
{
    private SqliteApiKeyRepository CreateRepository()
        => new(Db);

    private ApiKeyEntity CreateEntity(string name, string keyHash, bool active = true, DateTime? expiresAt = null)
        => new()
        {
            KeyHash = keyHash,
            KeyPrefix = keyHash[..Math.Min(8, keyHash.Length)],
            Name = name,
            IsActive = active,
            ExpiresAt = expiresAt
        };

    [Fact]
    public async Task AddAsync_PersistsEntity()
    {
        var repo = CreateRepository();
        var entity = CreateEntity("Test Key", "abc123hash_longenough");

        var result = await repo.AddAsync(entity);

        result.Id.Should().BeGreaterThan(0);
        var found = await repo.GetByIdAsync(result.Id);
        found.Should().NotBeNull();
        found!.Name.Should().Be("Test Key");
    }

    [Fact]
    public async Task GetByKeyHashAsync_Found_ReturnsEntity()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEntity("Key1", "hash1_abcdefg1234567890"));
        await repo.AddAsync(CreateEntity("Key2", "hash2_abcdefg1234567890"));

        var result = await repo.GetByKeyHashAsync("hash2_abcdefg1234567890");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Key2");
    }

    [Fact]
    public async Task GetByKeyHashAsync_NotFound_ReturnsNull()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEntity("Key1", "hash1_abcdefg1234567890"));

        var result = await repo.GetByKeyHashAsync("nonexistent_hash_value");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOrderedById()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEntity("Key1", "hash1_abcdefg1234567890"));
        await repo.AddAsync(CreateEntity("Key2", "hash2_abcdefg1234567890"));

        var all = await repo.GetAllAsync();

        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var repo = CreateRepository();
        var entity = await repo.AddAsync(CreateEntity("Original", "hash1_abcdefg1234567890", active: true));

        entity.IsActive = false;
        await repo.UpdateAsync(entity);

        var updated = await repo.GetByIdAsync(entity.Id);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntity()
    {
        var repo = CreateRepository();
        var entity = await repo.AddAsync(CreateEntity("ToDelete", "hash_del_abcdefg123456"));

        await repo.DeleteAsync(entity.Id);
        DetachAll();

        var found = await repo.GetByIdAsync(entity.Id);
        found.Should().BeNull();
    }
}
