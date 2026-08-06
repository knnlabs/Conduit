using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ConduitLLM.Tests.Core.Services;

/// <summary>
/// Tests for #1232: the capability service must not fabricate an exact "Cl100KBase" claim
/// for models it knows nothing about. Reporting null lets TokenizerEncodingMap.Resolve
/// apply the single documented default (approximate cl100k_base) with honest fidelity.
/// </summary>
public sealed class DatabaseModelCapabilityServiceTokenizerTests
{
    private readonly Mock<IModelProviderMappingRepository> _repository = new();

    [Fact]
    public async Task GetTokenizerTypeAsync_KnownModel_ReturnsEnumName()
    {
        _repository
            .Setup(repository => repository.GetByModelNameAsync("gpt-4o", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModelProviderMapping
            {
                ModelAlias = "gpt-4o",
                ModelProviderTypeAssociation = new ModelProviderTypeAssociation
                {
                    Model = new Model { Name = "gpt-4o", TokenizerType = TokenizerType.O200KBase }
                }
            });

        var result = await CreateService().GetTokenizerTypeAsync("gpt-4o");

        Assert.Equal("O200KBase", result);
    }

    [Fact]
    public async Task GetTokenizerTypeAsync_UnknownModel_ReturnsNullNotFabricatedDefault()
    {
        _repository
            .Setup(repository => repository.GetByModelNameAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelProviderMapping?)null);
        _repository
            .Setup(repository => repository.GetPaginatedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModelProviderMapping>(), 0));

        var result = await CreateService().GetTokenizerTypeAsync("unknown");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTokenizerTypeAsync_LookupFails_ReturnsNullNotFabricatedDefault()
    {
        _repository
            .Setup(repository => repository.GetByModelNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await CreateService().GetTokenizerTypeAsync("gpt-4o");

        Assert.Null(result);
    }

    private DatabaseModelCapabilityService CreateService() => new(
        Mock.Of<ILogger<DatabaseModelCapabilityService>>(),
        _repository.Object,
        new MemoryCache(Options.Create(new MemoryCacheOptions())));
}
