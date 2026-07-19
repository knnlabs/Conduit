using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

using FluentAssertions;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Tests for provider-reported cost billing (the authoritative-cost short-circuit) in both
    /// CalculateCostAsync and CalculateCostByIdAsync.
    /// </summary>
    public partial class CostCalculationServiceBasicTests
    {
        private static Usage TrustedUsage(decimal? providerCost, decimal markup = 1.0m, bool isBatch = false) => new()
        {
            PromptTokens = 1000,
            CompletionTokens = 500,
            TotalTokens = 1500,
            IsBatch = isBatch,
            ProviderReportedCostUsd = providerCost,
            ProviderCostPolicy = new ProviderCostBillingPolicy
            {
                TrustProviderReportedCost = true,
                MarkupMultiplier = markup
            }
        };

        private static ModelCost StandardModelCost(int? id = null) => new()
        {
            Id = id ?? 42,
            CostName = "test-model-cost",
            InputCostPerMillionTokens = 10.00m,
            OutputCostPerMillionTokens = 30.00m,
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1)
        };

        [Fact]
        public async Task CalculateCostAsync_TrustedProviderCost_BillsProviderCostAndBypassesModelCostLookup()
        {
            // Arrange
            var usage = TrustedUsage(providerCost: 0.005m);
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(StandardModelCost());

            // Act
            var result = await _service.CalculateCostAsync("openai/gpt-4o", usage);

            // Assert — provider cost wins, ModelCost math (which would be 0.025) never runs
            result.Should().Be(0.005m);
            _modelCostServiceMock.Verify(
                x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CalculateCostAsync_TrustedProviderCost_WithNoModelCostRow_StillBills()
        {
            // Arrange — no ModelCost configured; historically this billed at $0 (free)
            var usage = TrustedUsage(providerCost: 0.007m);
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            var result = await _service.CalculateCostAsync("openai/gpt-4o", usage);

            // Assert
            result.Should().Be(0.007m);
        }

        [Fact]
        public async Task CalculateCostAsync_UntrustedProvider_UsesModelCost()
        {
            // Arrange — provider reports a cost, but the provider is not trusted
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                ProviderReportedCostUsd = 0.005m,
                ProviderCostPolicy = new ProviderCostBillingPolicy { TrustProviderReportedCost = false }
            };
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(StandardModelCost());

            // Act
            var result = await _service.CalculateCostAsync("openai/gpt-4o", usage);

            // Assert — computed from ModelCost, not the reported 0.005
            result.Should().Be(0.025m);
        }

        [Fact]
        public async Task CalculateCostAsync_TrustedButNoReportedCost_FallsBackToModelCost()
        {
            // Arrange — trusted, but the provider reported no cost this request
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                ProviderReportedCostUsd = null,
                ProviderCostPolicy = new ProviderCostBillingPolicy { TrustProviderReportedCost = true }
            };
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(StandardModelCost());

            // Act
            var result = await _service.CalculateCostAsync("openai/gpt-4o", usage);

            // Assert
            result.Should().Be(0.025m);
        }

        [Fact]
        public async Task CalculateCostAsync_TrustedZeroCost_ReturnsZeroWithoutLookup()
        {
            // Arrange — free variant reports cost: 0; must bill 0, not fall back to ModelCost
            var usage = TrustedUsage(providerCost: 0m);
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(StandardModelCost());

            // Act
            var result = await _service.CalculateCostAsync("openrouter/free-model:free", usage);

            // Assert
            result.Should().Be(0m);
            _modelCostServiceMock.Verify(
                x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CalculateCostAsync_AppliesMarkupMultiplier()
        {
            // Arrange
            var usage = TrustedUsage(providerCost: 0.01m, markup: 1.25m);

            // Act
            var result = await _service.CalculateCostAsync("openai/gpt-4o", usage);

            // Assert
            result.Should().Be(0.0125m);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        public async Task CalculateCostAsync_NonPositiveMarkup_TreatedAsPassThrough(double markup)
        {
            // Arrange — a misconfigured 0/negative markup must not zero out or invert billing
            var usage = TrustedUsage(providerCost: 0.01m, markup: (decimal)markup);

            // Act
            var result = await _service.CalculateCostAsync("openai/gpt-4o", usage);

            // Assert
            result.Should().Be(0.01m);
        }

        [Fact]
        public async Task CalculateCostAsync_TrustedCostWithBatch_DoesNotApplyBatchMultiplier()
        {
            // Arrange — provider cost is the actual charge; the list-price batch discount must not stack
            var usage = TrustedUsage(providerCost: 0.01m, isBatch: true);
            var modelCost = StandardModelCost();
            modelCost.SupportsBatchProcessing = true;
            modelCost.BatchProcessingMultiplier = 0.5m;
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync("openai/gpt-4o", usage);

            // Assert — full provider cost, not 0.005
            result.Should().Be(0.01m);
        }

        [Fact]
        public async Task CalculateCostByIdAsync_TrustedProviderCost_BillsProviderCostAndBypassesLookup()
        {
            // Arrange
            var usage = TrustedUsage(providerCost: 0.005m);
            _modelCostServiceMock
                .Setup(x => x.GetCostByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(StandardModelCost());

            // Act
            var result = await _service.CalculateCostByIdAsync(42, usage);

            // Assert
            result.Should().Be(0.005m);
            _modelCostServiceMock.Verify(
                x => x.GetCostByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CalculateCostByIdAsync_TrustedProviderCost_WithNoModelCostRow_StillBills()
        {
            // Arrange
            var usage = TrustedUsage(providerCost: 0.009m);
            _modelCostServiceMock
                .Setup(x => x.GetCostByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            var result = await _service.CalculateCostByIdAsync(999, usage);

            // Assert
            result.Should().Be(0.009m);
        }

        [Fact]
        public async Task CalculateCostByIdAsync_AppliesMarkupMultiplier()
        {
            // Arrange
            var usage = TrustedUsage(providerCost: 0.02m, markup: 1.5m);

            // Act
            var result = await _service.CalculateCostByIdAsync(42, usage);

            // Assert
            result.Should().Be(0.03m);
        }
    }
}
