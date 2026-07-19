using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Configuration.Services;

public class ModelCostServiceTests
{
    [Fact]
    public async Task UpdateModelCostAsync_CopiesAllMutableFields()
    {
        var originalCreatedAt = DateTime.UtcNow.AddYears(-1);
        var existing = new ModelCost
        {
            Id = 42,
            CostName = "Old pricing",
            CreatedAt = originalCreatedAt
        };
        var update = new ModelCost
        {
            Id = 42,
            CostName = "New pricing",
            PricingModel = PricingModel.PerSecondVideo,
            PricingConfiguration = "{\"baseRate\":0.09}",
            InputCostPerMillionTokens = 1.1m,
            OutputCostPerMillionTokens = 2.2m,
            EmbeddingCostPerMillionTokens = 3.3m,
            ModelType = "video",
            IsActive = false,
            EffectiveDate = DateTime.UtcNow.AddDays(1),
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            Description = "Updated description",
            Priority = 7,
            BatchProcessingMultiplier = 0.5m,
            SupportsBatchProcessing = true,
            CachedInputCostPerMillionTokens = 0.11m,
            CachedInputWriteCostPerMillionTokens = 0.22m,
            CostPerSearchUnit = 0.33m,
            AudioCostPerMinute = 0.44m,
            AudioCostPerThousandCharacters = 0.55m,
            ReasoningCostPerMillionTokens = 0.66m
        };

        var repository = new Mock<IModelCostRepository>();
        repository
            .Setup(x => x.GetByIdAsync(update.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository
            .Setup(x => x.UpdateAsync(existing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new ModelCostService(
            repository.Object,
            Mock.Of<IModelProviderMappingRepository>(),
            Mock.Of<ILogger<ModelCostService>>());

        var beforeUpdate = DateTime.UtcNow;
        var result = await service.UpdateModelCostAsync(update);

        Assert.True(result);
        Assert.Equal(update.CostName, existing.CostName);
        Assert.Equal(update.PricingModel, existing.PricingModel);
        Assert.Equal(update.PricingConfiguration, existing.PricingConfiguration);
        Assert.Equal(update.InputCostPerMillionTokens, existing.InputCostPerMillionTokens);
        Assert.Equal(update.OutputCostPerMillionTokens, existing.OutputCostPerMillionTokens);
        Assert.Equal(update.EmbeddingCostPerMillionTokens, existing.EmbeddingCostPerMillionTokens);
        Assert.Equal(update.ModelType, existing.ModelType);
        Assert.Equal(update.IsActive, existing.IsActive);
        Assert.Equal(update.EffectiveDate, existing.EffectiveDate);
        Assert.Equal(update.ExpiryDate, existing.ExpiryDate);
        Assert.Equal(update.Description, existing.Description);
        Assert.Equal(update.Priority, existing.Priority);
        Assert.Equal(update.BatchProcessingMultiplier, existing.BatchProcessingMultiplier);
        Assert.Equal(update.SupportsBatchProcessing, existing.SupportsBatchProcessing);
        Assert.Equal(update.CachedInputCostPerMillionTokens, existing.CachedInputCostPerMillionTokens);
        Assert.Equal(update.CachedInputWriteCostPerMillionTokens, existing.CachedInputWriteCostPerMillionTokens);
        Assert.Equal(update.CostPerSearchUnit, existing.CostPerSearchUnit);
        Assert.Equal(update.AudioCostPerMinute, existing.AudioCostPerMinute);
        Assert.Equal(update.AudioCostPerThousandCharacters, existing.AudioCostPerThousandCharacters);
        Assert.Equal(update.ReasoningCostPerMillionTokens, existing.ReasoningCostPerMillionTokens);
        Assert.Equal(originalCreatedAt, existing.CreatedAt);
        Assert.InRange(existing.UpdatedAt, beforeUpdate, DateTime.UtcNow);
        repository.Verify(x => x.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }
}
