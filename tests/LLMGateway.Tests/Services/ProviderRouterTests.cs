using FluentAssertions;
using LLMGateway.Data.Entities;
using LLMGateway.Data.Repositories;
using LLMGateway.Services;
using Moq;
using Xunit;

namespace LLMGateway.Tests.Services;

public class ProviderRouterTests
{
    private static ProviderEntity CreateEntity(string name, string baseUrl, string apiKey, List<string> models, bool enabled = true)
    {
        return new ProviderEntity
        {
            Id = 1,
            Name = name,
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            Models = models,
            IsEnabled = enabled
        };
    }

    [Fact]
    public async Task GetProviderAsync_ModelFound_ReturnsMappedOptions()
    {
        var entity = CreateEntity("OpenAI", "https://api.openai.com", "sk-test", ["gpt-4o", "gpt-4o-mini"]);
        var mockRepo = new Mock<IProviderRepository>();
        mockRepo.Setup(r => r.GetByModelNameAsync("gpt-4o", default))
            .ReturnsAsync(entity);

        var router = new ProviderRouter(mockRepo.Object);
        var result = await router.GetProviderAsync("gpt-4o");

        result.Should().NotBeNull();
        result!.Name.Should().Be("OpenAI");
        result.BaseUrl.Should().Be("https://api.openai.com");
        result.ApiKey.Should().Be("sk-test");
        result.Models.Should().BeEquivalentTo(["gpt-4o", "gpt-4o-mini"]);
    }

    [Fact]
    public async Task GetProviderAsync_ModelNotFound_ReturnsNull()
    {
        var mockRepo = new Mock<IProviderRepository>();
        mockRepo.Setup(r => r.GetByModelNameAsync("unknown-model", default))
            .ReturnsAsync((ProviderEntity?)null);

        var router = new ProviderRouter(mockRepo.Object);
        var result = await router.GetProviderAsync("unknown-model");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllModelsAsync_ReturnsOnlyEnabledProviders()
    {
        var entities = new List<ProviderEntity>
        {
            CreateEntity("OpenAI", "https://api.openai.com", "sk-1", ["gpt-4o"]),
            CreateEntity("Disabled", "https://disabled.com", "sk-2", ["disabled-model"], enabled: false)
        };
        var mockRepo = new Mock<IProviderRepository>();
        mockRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(entities);

        var router = new ProviderRouter(mockRepo.Object);
        var result = await router.GetAllModelsAsync();

        result.Should().ContainSingle();
        result[0].ModelName.Should().Be("gpt-4o");
        result[0].ProviderName.Should().Be("OpenAI");
    }

    [Fact]
    public async Task GetAllModelsAsync_AllDisabled_ReturnsEmptyList()
    {
        var entities = new List<ProviderEntity>
        {
            CreateEntity("Disabled1", "https://d1.com", "sk-1", ["m1"], enabled: false),
            CreateEntity("Disabled2", "https://d2.com", "sk-2", ["m2"], enabled: false)
        };
        var mockRepo = new Mock<IProviderRepository>();
        mockRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(entities);

        var router = new ProviderRouter(mockRepo.Object);
        var result = await router.GetAllModelsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllModelsAsync_MultipleModels_SpreadAcrossProviders()
    {
        var entities = new List<ProviderEntity>
        {
            CreateEntity("OpenAI", "https://api.openai.com", "sk-1", ["gpt-4o", "gpt-4o-mini"]),
            CreateEntity("DeepSeek", "https://api.deepseek.com", "sk-2", ["deepseek-chat"])
        };
        var mockRepo = new Mock<IProviderRepository>();
        mockRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(entities);

        var router = new ProviderRouter(mockRepo.Object);
        var result = await router.GetAllModelsAsync();

        result.Should().HaveCount(3);
        result.Select(m => m.ModelName).Should().BeEquivalentTo(["gpt-4o", "gpt-4o-mini", "deepseek-chat"]);
    }
}
