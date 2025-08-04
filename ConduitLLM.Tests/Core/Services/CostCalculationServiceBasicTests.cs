using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Tests for basic cost calculation functionality
    /// </summary>
    public class CostCalculationServiceBasicTests : CostCalculationServiceTestBase
    {
        public CostCalculationServiceBasicTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Constructor_WithNullModelCostService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new CostCalculationService(null!, _loggerMock.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("modelCostService");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new CostCalculationService(_modelCostServiceMock.Object, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Fact]
        public async Task CalculateCostAsync_WithTextGeneration_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m, // $0.01 per 1K tokens
                OutputTokenCost = 0.00003m  // $0.03 per 1K tokens
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: (1000 * 0.00001) + (500 * 0.00003) = 0.01 + 0.015 = 0.025
            result.Should().Be(0.025m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithEmbedding_UsesEmbeddingCost()
        {
            // Arrange
            var modelId = "openai/text-embedding-ada-002";
            var usage = new Usage
            {
                PromptTokens = 2000,
                CompletionTokens = 0,
                TotalTokens = 2000
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00001m,
                EmbeddingTokenCost = 0.0000001m // $0.0001 per 1K tokens
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 2000 * 0.0000001 = 0.0002
            result.Should().Be(0.0002m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithImageGeneration_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 3
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                ImageCostPerImage = 0.04m // $0.04 per image
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 3 * 0.04 = 0.12
            result.Should().Be(0.12m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithVideoGeneration_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "runway/gen-2";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = 5.5,
                VideoResolution = "1920x1080"
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.05m, // $0.05 per second
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m,
                    ["1280x720"] = 1.0m,
                    ["640x480"] = 0.5m
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 5.5 * 0.05 * 1.5 = 0.4125
            result.Should().Be(0.4125m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithVideoGenerationNoResolutionMultiplier_UsesBaseCost()
        {
            // Arrange
            var modelId = "runway/gen-2";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = 10,
                VideoResolution = "1920x1080"
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.05m,
                VideoResolutionMultipliers = null // No multipliers defined
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 10 * 0.05 = 0.5 (no multiplier applied)
            result.Should().Be(0.5m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithCombinedUsage_CalculatesAllComponents()
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
                VideoResolution = "1280x720"
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
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: Text: (1000 * 0.00001) + (500 * 0.00002) = 0.01 + 0.01 = 0.02
            //          Images: 2 * 0.05 = 0.1
            //          Video: 3 * 0.1 * 0.8 = 0.24
            //          Total: 0.02 + 0.1 + 0.24 = 0.36
            result.Should().Be(0.36m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNullModelId_ReturnsZero()
        {
            // Arrange
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500
            };

            // Act
            var result = await _service.CalculateCostAsync(null!, usage);

            // Assert
            result.Should().Be(0m);
            _loggerMock.VerifyLog(LogLevel.Warning, "Model ID is null or empty");
        }

        [Fact]
        public async Task CalculateCostAsync_WithEmptyModelId_ReturnsZero()
        {
            // Arrange
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500
            };

            // Act
            var result = await _service.CalculateCostAsync(string.Empty, usage);

            // Assert
            result.Should().Be(0m);
            _loggerMock.VerifyLog(LogLevel.Warning, "Model ID is null or empty");
        }

        [Fact]
        public async Task CalculateCostAsync_WithNullUsage_ReturnsZero()
        {
            // Arrange
            var modelId = "openai/gpt-4o";

            // Act
            var result = await _service.CalculateCostAsync(modelId, null!);

            // Assert
            result.Should().Be(0m);
            _loggerMock.VerifyLog(LogLevel.Warning, "Usage data is null");
        }

        [Fact]
        public async Task CalculateCostAsync_WithModelCostNotFound_ReturnsZero()
        {
            // Arrange
            var modelId = "unknown/model";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCostInfo?)null);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            result.Should().Be(0m);
            _loggerMock.VerifyLog(LogLevel.Warning, "Cost information not found");
        }

        [Theory]
        [InlineData(0, 0, 0.0, 0.0)]
        [InlineData(1, 1, 0.00001, 0.00003)]
        [InlineData(1000000, 500000, 10.0, 15.0)]
        [InlineData(int.MaxValue, int.MaxValue, 21474.83647, 64424.50941)]
        public async Task CalculateCostAsync_WithVariousTokenCounts_CalculatesCorrectly(
            int promptTokens, int completionTokens, decimal expectedPromptCost, decimal expectedCompletionCost)
        {
            // Arrange
            var modelId = "test/model";
            var usage = new Usage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            result.Should().Be(expectedPromptCost + expectedCompletionCost);
        }

        [Fact]
        public async Task CalculateCostAsync_WithDecimalPrecision_MaintainsAccuracy()
        {
            // Arrange
            var modelId = "precision/model";
            var usage = new Usage
            {
                PromptTokens = 123456,
                CompletionTokens = 789012,
                TotalTokens = 912468
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.000012345m,
                OutputTokenCost = 0.000098765m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: (123456 * 0.000012345) + (789012 * 0.000098765) = 1.52428320 + 77.926551180 = 79.450834500
            result.Should().Be(79.450834500m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithCancellationToken_PropagatesToken()
        {
            // Arrange
            var modelId = "test/model";
            var usage = new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 50,
                TotalTokens = 150
            };
            using var cts = new CancellationTokenSource();

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, cts.Token))
                .ReturnsAsync(new ModelCostInfo
                {
                    ModelIdPattern = modelId,
                    InputTokenCost = 0.001m,
                    OutputTokenCost = 0.002m
                });

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage, cts.Token);

            // Assert
            result.Should().Be(0.2m);
            _modelCostServiceMock.Verify(x => x.GetCostForModelAsync(modelId, cts.Token), Times.Once);
        }

        [Fact]
        public async Task CalculateCostAsync_LogsDebugInformation()
        {
            // Arrange
            var modelId = "test/model";
            var usage = new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 50,
                TotalTokens = 150,
                ImageCount = 2,
                VideoDurationSeconds = 3.5
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.001m,
                OutputTokenCost = 0.002m,
                ImageCostPerImage = 0.05m,
                VideoCostPerSecond = 0.1m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            await _service.CalculateCostAsync(modelId, usage);

            // Assert
            _loggerMock.VerifyLog(LogLevel.Debug, "Calculated cost");
        }

        [Fact]
        public async Task CalculateCostAsync_WithUnknownVideoResolution_UsesBaseCost()
        {
            // Arrange
            var modelId = "video/model";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = 5,
                VideoResolution = "4K" // Not in multipliers
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m,
                    ["1280x720"] = 1.0m
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 5 * 0.1 = 0.5 (no multiplier applied for unknown resolution)
            result.Should().Be(0.5m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNullVideoResolution_UsesBaseCost()
        {
            // Arrange
            var modelId = "video/model";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = 5,
                VideoResolution = null
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 5 * 0.1 = 0.5 (no multiplier applied for null resolution)
            result.Should().Be(0.5m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithZeroCosts_ReturnsZero()
        {
            // Arrange
            var modelId = "free/model";
            var usage = new Usage
            {
                PromptTokens = 10000,
                CompletionTokens = 5000,
                TotalTokens = 15000
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            result.Should().Be(0m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithEmbeddingAndImageCount_UsesEmbeddingCost()
        {
            // This tests that embedding cost is correctly used even when ImageCount is present
            // When a model supports both embeddings and images, embedding cost should be used for the text portion
            // Arrange
            var modelId = "multimodal/embedding";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 0,
                TotalTokens = 1000,
                ImageCount = 1
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00001m,
                EmbeddingTokenCost = 0.0000001m,  // This is now USED for embeddings
                ImageCostPerImage = 0.05m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // NEW BEHAVIOR (correct):
            // Text: 1000 * 0.0000001 (embedding cost) = 0.0001
            // Image: 1 * 0.05 = 0.05
            // Total: 0.0001 + 0.05 = 0.0501
            result.Should().Be(0.0501m);
        }
        
        [Fact]
        public async Task CalculateCostAsync_WithEmbeddingModelGeneratingImages_UsesEmbeddingCost()
        {
            // Real-world scenario: An embedding model that can also generate images
            // Example: A multimodal model that embeds text but can also create visualizations
            // Arrange
            var modelId = "openai/multimodal-embed-v1";
            var usage = new Usage
            {
                PromptTokens = 5000,
                CompletionTokens = 0,  // Embeddings don't have completions
                TotalTokens = 5000,
                ImageCount = 2         // Generated 2 visualization images
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.0001m,       // Regular text processing cost
                OutputTokenCost = 0.0002m,      // Not used for embeddings
                EmbeddingTokenCost = 0.00001m,  // Specialized embedding cost (10x cheaper)
                ImageCostPerImage = 0.02m       // Image generation cost
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert - New behavior correctly uses EmbeddingTokenCost
            // Text: 5000 * 0.00001 = 0.05 (using embedding cost)
            // Images: 2 * 0.02 = 0.04
            // Total: 0.09
            result.Should().Be(0.09m);
            
            // This is significantly cheaper than using input cost (0.54)!
        }

        [Theory]
        [InlineData(1000, 0, null, true)]    // Pure embedding: uses embedding cost
        [InlineData(1000, 500, null, false)] // Has completions: uses regular cost
        [InlineData(1000, 0, 1, true)]       // Has images: STILL uses embedding cost (fixed!)
        [InlineData(1000, 500, 1, false)]    // Has completions and images: uses regular cost
        public async Task CalculateCostAsync_EmbeddingCostUsageRules_FollowsCurrentLogic(
            int promptTokens, int completionTokens, int? imageCount, bool shouldUseEmbeddingCost)
        {
            // This theory test documents when embedding cost is used vs regular token cost
            // Arrange
            var modelId = "test/model";
            var usage = new Usage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
                ImageCount = imageCount
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.0001m,
                OutputTokenCost = 0.0002m,
                EmbeddingTokenCost = 0.00001m,  // 10x cheaper than regular
                ImageCostPerImage = 0.05m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            decimal expectedCost;
            if (shouldUseEmbeddingCost)
            {
                // Embedding case: promptTokens * embeddingCost + images if present
                expectedCost = promptTokens * modelCost.EmbeddingTokenCost.Value;
                if (imageCount.HasValue)
                {
                    expectedCost += imageCount.Value * modelCost.ImageCostPerImage.Value;
                }
            }
            else
            {
                // Regular case: (prompt * input) + (completion * output) + (images * imageCost)
                expectedCost = (promptTokens * modelCost.InputTokenCost) + 
                              (completionTokens * modelCost.OutputTokenCost) +
                              (imageCount ?? 0) * modelCost.ImageCostPerImage.Value;
            }
            
            result.Should().Be(expectedCost);
        }

        [Fact]
        public async Task CalculateCostAsync_WithEmbeddingCostButHasCompletionTokens_UsesRegularCost()
        {
            // This tests that when a model has embedding cost defined but the request has completion tokens,
            // it uses regular input/output costs (not embedding cost) since it's not an embedding request
            // Arrange
            var modelId = "multimodal/model-with-embedding-support";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,  // Has completions, so NOT an embedding request
                TotalTokens = 1500,
                ImageCount = 1
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00002m,       // Regular input cost
                OutputTokenCost = 0.00004m,      // Regular output cost
                EmbeddingTokenCost = 0.0000001m, // Embedding cost (not used in this case)
                ImageCostPerImage = 0.03m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Should use regular costs because CompletionTokens > 0:
            // Input: 1000 * 0.00002 = 0.02
            // Output: 500 * 0.00004 = 0.02
            // Image: 1 * 0.03 = 0.03
            // Total: 0.07
            result.Should().Be(0.07m);
        }
    }
}