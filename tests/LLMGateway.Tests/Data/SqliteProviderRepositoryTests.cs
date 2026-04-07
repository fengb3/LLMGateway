using FluentAssertions;
using LLMGateway.Data.Entities;
using LLMGateway.Data.Repositories;
using Xunit;

namespace LLMGateway.Tests.Data;

public class SqliteProviderRepositoryTests : SqliteTestBase
{
    private SqliteProviderRepository CreateRepository()
        => new(Db);

    private ProviderEntity CreateEntity(string name, List<string> models, bool enabled = true)
        => new()
        {
            Name = name,
            BaseUrl = $"https://{name.ToLower()}.com",
            ApiKey = $"sk-{name.ToLower()}",
            Models = models,
            IsEnabled = enabled
        };

    [Fact]
    public async Task AddAsync_PersistsEntity()
    {
        var repo = CreateRepository();
        var entity = CreateEntity("OpenAI", ["gpt-4o"]);

        var result = await repo.AddAsync(entity);

        result.Id.Should().BeGreaterThan(0);
        var found = await repo.GetByIdAsync(result.Id);
        found.Should().NotBeNull();
        found!.Name.Should().Be("OpenAI");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOrderedById()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEntity("B", ["b-model"]));
        await repo.AddAsync(CreateEntity("A", ["a-model"]));

        var all = await repo.GetAllAsync();

        all.Should().HaveCount(2);
        all[0].Name.Should().Be("B");
        all[1].Name.Should().Be("A");
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsCorrectProvider()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEntity("OpenAI", ["gpt-4o"]));
        await repo.AddAsync(CreateEntity("DeepSeek", ["deepseek-chat"]));

        var result = await repo.GetByNameAsync("DeepSeek");

        result.Should().NotBeNull();
        result!.BaseUrl.Should().Be("https://deepseek.com");
    }

    [Fact]
    public async Task GetByModelNameAsync_ReturnsProvider()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEntity("OpenAI", ["gpt-4o", "gpt-4o-mini"]));

        var result = await repo.GetByModelNameAsync("gpt-4o-mini");

        result.Should().NotBeNull();
        result!.Name.Should().Be("OpenAI");
    }

    [Fact]
    public async Task GetByModelNameAsync_NotFound_ReturnsNull()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEntity("OpenAI", ["gpt-4o"]));

        var result = await repo.GetByModelNameAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByModelNameAsync_DisabledProvider_ReturnsNull()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEntity("Disabled", ["disabled-model"], enabled: false));

        var result = await repo.GetByModelNameAsync("disabled-model");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var repo = CreateRepository();
        var entity = await repo.AddAsync(CreateEntity("OpenAI", ["gpt-4o"]));

        entity.BaseUrl = "https://new-url.com";
        await repo.UpdateAsync(entity);

        var updated = await repo.GetByIdAsync(entity.Id);
        updated!.BaseUrl.Should().Be("https://new-url.com");
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntity()
    {
        var repo = CreateRepository();
        var entity = await repo.AddAsync(CreateEntity("OpenAI", ["gpt-4o"]));

        await repo.DeleteAsync(entity.Id);
        DetachAll();

        var found = await repo.GetByIdAsync(entity.Id);
        found.Should().BeNull();
    }
}
