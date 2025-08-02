using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Tests for batch processing functionality
    /// </summary>
    public class CostCalculationServiceBatchTests : CostCalculationServiceTestBase
    {
        public CostCalculationServiceBatchTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task CalculateCostAsync_WithBatchProcessing_AppliesDiscount()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                IsBatch = true
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.001m,
                OutputTokenCost = 0.002m,
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = 0.5m // 50% discount
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected without batch: (1000 * 0.001) + (500 * 0.002) = 1.0 + 1.0 = 2.0
            // Expected with 50% batch discount: 2.0 * 0.5 = 1.0
            result.Should().Be(1.0m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithBatchProcessingButNotSupported_NoDiscount()
        {
            // Arrange
            var modelId = "openai/gpt-3.5";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                IsBatch = true
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.001m,
                OutputTokenCost = 0.002m,
                SupportsBatchProcessing = false, // Model doesn't support batch
                BatchProcessingMultiplier = 0.5m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: No discount applied since model doesn't support batch
            // (1000 * 0.001) + (500 * 0.002) = 1.0 + 1.0 = 2.0
            result.Should().Be(2.0m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithBatchFalse_NoDiscount()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                IsBatch = false
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.001m,
                OutputTokenCost = 0.002m,
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = 0.5m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: No discount since IsBatch is false
            // (1000 * 0.001) + (500 * 0.002) = 1.0 + 1.0 = 2.0
            result.Should().Be(2.0m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithBatchNullMultiplier_NoDiscount()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                IsBatch = true
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.001m,
                OutputTokenCost = 0.002m,
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = null // No multiplier defined
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: No discount since multiplier is null
            // (1000 * 0.001) + (500 * 0.002) = 1.0 + 1.0 = 2.0
            result.Should().Be(2.0m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithBatchAndMultiModalUsage_AppliesDiscountToAll()
        {
            // Arrange
            var modelId = "multimodal/model";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                ImageCount = 2,
                VideoDurationSeconds = 3,
                VideoResolution = "1280x720",
                IsBatch = true
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00002m,
                ImageCostPerImage = 0.05m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1280x720"] = 0.8m
                },
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = 0.6m // 40% discount
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected without batch: 
            // Text: (1000 * 0.00001) + (500 * 0.00002) = 0.01 + 0.01 = 0.02
            // Images: 2 * 0.05 = 0.1
            // Video: 3 * 0.1 * 0.8 = 0.24
            // Total before batch: 0.02 + 0.1 + 0.24 = 0.36
            // With 40% discount (0.6 multiplier): 0.36 * 0.6 = 0.216
            result.Should().Be(0.216m);
        }

        [Theory]
        [InlineData(0.5, 1.0)]  // 50% discount
        [InlineData(0.6, 1.2)]  // 40% discount
        [InlineData(0.4, 0.8)]  // 60% discount
        [InlineData(1.0, 2.0)]  // No discount
        public async Task CalculateCostAsync_WithVariousBatchMultipliers_AppliesCorrectDiscount(decimal multiplier, decimal expectedCost)
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                IsBatch = true
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.001m,
                OutputTokenCost = 0.002m,
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = multiplier
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            result.Should().Be(expectedCost);
        }

        [Fact]
        public async Task CalculateCostAsync_WithCachedTokensAndBatchProcessing_AppliesBothDiscounts()
        {
            // Arrange
            var modelId = "anthropic/claude-3-haiku";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                CachedInputTokens = 600,
                IsBatch = true
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m,
                CachedInputTokenCost = 0.000001m,
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = 0.5m  // 50% discount for batch
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Regular input: 400 * 0.00001 = 0.004
            // Cached input: 600 * 0.000001 = 0.0006
            // Output: 500 * 0.00003 = 0.015
            // Subtotal: 0.004 + 0.0006 + 0.015 = 0.0196
            // With batch discount: 0.0196 * 0.5 = 0.0098
            result.Should().Be(0.0098m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithSearchUnitsAndBatchProcessing_AppliesDiscountToAll()
        {
            // Arrange
            var modelId = "cohere/rerank-3.5";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 0,
                TotalTokens = 1000,
                SearchUnits = 100,
                IsBatch = true
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0m,
                CostPerSearchUnit = 2.0m,
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = 0.5m // 50% discount
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Token cost: 1000 * 0.00001 = 0.01
            // Search unit cost: 100 * (2.0 / 1000) = 0.2
            // Total before discount: 0.01 + 0.2 = 0.21
            // After 50% discount: 0.21 * 0.5 = 0.105
            result.Should().Be(0.105m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithInferenceStepsAndBatchProcessing_AppliesDiscountToAll()
        {
            // Arrange
            var modelId = "fireworks/batch-model";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                InferenceSteps = 10,
                IsBatch = true
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00002m,
                CostPerInferenceStep = 0.0002m,
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = 0.5m // 50% discount
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Token cost: (1000 * 0.00001) + (500 * 0.00002) = 0.01 + 0.01 = 0.02
            // Step cost: 10 * 0.0002 = 0.002
            // Total before discount: 0.022
            // After 50% discount: 0.011
            result.Should().Be(0.011m);
        }
    }
}