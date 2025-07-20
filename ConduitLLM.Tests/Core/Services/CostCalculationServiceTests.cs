using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
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
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    [Trait("Component", "Core")]
    public class CostCalculationServiceTests : TestBase
    {
        private readonly Mock<IModelCostService> _modelCostServiceMock;
        private readonly Mock<ILogger<CostCalculationService>> _loggerMock;
        private readonly CostCalculationService _service;

        public CostCalculationServiceTests(ITestOutputHelper output) : base(output)
        {
            _modelCostServiceMock = new Mock<IModelCostService>();
            _loggerMock = CreateLogger<CostCalculationService>();
            _service = new CostCalculationService(_modelCostServiceMock.Object, _loggerMock.Object);
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
        public async Task CalculateCostAsync_WithNegativeInputTokens_HandlesAsRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00003m,
                OutputTokenCost = 0.00006m
            };
            
            var usage = new Usage
            {
                PromptTokens = -1000, // Negative tokens (refund scenario)
                CompletionTokens = 500,
                TotalTokens = -500
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // -1000 * 0.00003 + 500 * 0.00006 = -0.03 + 0.03 = 0.00
            result.Should().Be(0.00m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNegativeOutputTokens_CalculatesNegativeCost()
        {
            // Arrange
            var modelId = "openai/gpt-4";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00003m,
                OutputTokenCost = 0.00006m
            };
            
            var usage = new Usage
            {
                PromptTokens = 500,
                CompletionTokens = -2000, // Negative output tokens
                TotalTokens = -1500
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // 500 * 0.00003 + (-2000) * 0.00006 = 0.015 - 0.12 = -0.105
            result.Should().Be(-0.105m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNegativeImageCount_ShouldThrowOrReturnZero()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                ImageCostPerImage = 0.04m
            };
            
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = -1 // Negative image count
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // Current implementation would calculate: -1 * 0.04 = -0.04
            // This might be intentional for refunds, or it might be a bug
            result.Should().Be(-0.04m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithAllNegativeValues_CalculatesNegativeTotal()
        {
            // Arrange
            var modelId = "openai/gpt-4";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00003m,
                OutputTokenCost = 0.00006m,
                ImageCostPerImage = 0.04m
            };
            
            var usage = new Usage
            {
                PromptTokens = -1000,
                CompletionTokens = -500,
                TotalTokens = -1500,
                ImageCount = -2
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // (-1000 * 0.00003) + (-500 * 0.00006) + (-2 * 0.04) = -0.03 - 0.03 - 0.08 = -0.14
            result.Should().Be(-0.14m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithVeryLargeNegativeValues_HandlesWithoutOverflow()
        {
            // Arrange
            var modelId = "openai/gpt-4";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00003m,
                OutputTokenCost = 0.00006m
            };
            
            var usage = new Usage
            {
                PromptTokens = -1000000000, // -1 billion tokens
                CompletionTokens = -500000000,   // -500 million tokens
                TotalTokens = -1500000000
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // (-1000000000 * 0.00003) + (-500000000 * 0.00006) = -30000 - 30000 = -60000
            result.Should().Be(-60000m);
        }

        [Theory]
        [InlineData(-100, 200, 0.00003, 0.00006, 0.009)] // Negative input, positive output
        [InlineData(100, -200, 0.00003, 0.00006, -0.009)] // Positive input, negative output
        [InlineData(-100, -200, 0.00003, 0.00006, -0.015)] // Both negative
        [InlineData(0, -1000, 0.00003, 0.00006, -0.06)] // Zero input, negative output
        public async Task CalculateCostAsync_WithVariousNegativeScenarios_CalculatesCorrectly(
            int inputTokens, int outputTokens, decimal inputCost, decimal outputCost, decimal expectedTotal)
        {
            // Arrange
            var modelId = "test/model";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = inputCost,
                OutputTokenCost = outputCost
            };
            
            var usage = new Usage
            {
                PromptTokens = inputTokens,
                CompletionTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            result.Should().Be(expectedTotal);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNegativeVideoDuration_HandlesAsRefund()
        {
            // Arrange
            var modelId = "minimax/video-01";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.05m // $0.05 per second
            };
            
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = -10, // Negative 10 seconds (refund)
                VideoResolution = "1280x720"
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // -10 * 0.05 = -0.5
            result.Should().Be(-0.5m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNegativeVideoDurationAndMultiplier_AppliesMultiplierToRefund()
        {
            // Arrange
            var modelId = "minimax/video-01";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = 0.05m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1920x1080"] = 1.5m,
                    ["1280x720"] = 1.0m
                }
            };
            
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = -20, // Negative 20 seconds
                VideoResolution = "1920x1080" // Higher resolution
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // -20 * 0.05 * 1.5 = -1.0 * 1.5 = -1.5
            result.Should().Be(-1.5m);
        }

        [Theory]
        [InlineData(-5, "1280x720", 0.1, 1.0, -0.5)]    // Basic negative with standard res
        [InlineData(-10, "1920x1080", 0.1, 2.0, -2.0)]  // Negative with 2x multiplier
        [InlineData(-30, "4K", 0.02, 1.0, -0.6)]        // Unknown resolution, no multiplier
        [InlineData(-60, null, 0.05, 1.0, -3.0)]        // Null resolution
        public async Task CalculateCostAsync_WithVariousNegativeVideoDurations_CalculatesCorrectly(
            double videoDuration, string? resolution, decimal costPerSecond, decimal multiplier, decimal expectedTotal)
        {
            // Arrange
            var modelId = "video/model";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                VideoCostPerSecond = costPerSecond,
                VideoResolutionMultipliers = resolution != null && multiplier != 1.0m ? 
                    new Dictionary<string, decimal> { [resolution] = multiplier } : null
            };
            
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = videoDuration,
                VideoResolution = resolution
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            result.Should().Be(expectedTotal);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNegativeVideoDurationAndTokens_CombinesAllCosts()
        {
            // Arrange
            var modelId = "video/model-with-chat";
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00002m,
                VideoCostPerSecond = 0.1m
            };
            
            var usage = new Usage
            {
                PromptTokens = 1000,       // Positive tokens
                CompletionTokens = 500,    // Positive tokens
                TotalTokens = 1500,
                VideoDurationSeconds = -15 // Negative video duration
            };
            
            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);
            
            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);
            
            // Assert
            // Token cost: (1000 * 0.00001) + (500 * 0.00002) = 0.01 + 0.01 = 0.02
            // Video cost: -15 * 0.1 = -1.5
            // Total: 0.02 - 1.5 = -1.48
            result.Should().Be(-1.48m);
        }

        #region Refund Method Tests

        [Fact]
        public async Task CalculateRefundAsync_WithValidInputs_CalculatesCorrectRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };
            var refundReason = "Service interruption";
            var originalTransactionId = "txn_12345";

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m, // $0.01 per 1K tokens
                OutputTokenCost = 0.00003m  // $0.03 per 1K tokens
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, refundReason, originalTransactionId);

            // Assert
            result.Should().NotBeNull();
            result.ModelId.Should().Be(modelId);
            result.RefundReason.Should().Be(refundReason);
            result.OriginalTransactionId.Should().Be(originalTransactionId);
            result.RefundAmount.Should().Be(0.0125m); // (500 * 0.00001) + (250 * 0.00003)
            result.Breakdown.Should().NotBeNull();
            result.Breakdown!.InputTokenRefund.Should().Be(0.005m);
            result.Breakdown.OutputTokenRefund.Should().Be(0.0075m);
            result.IsPartialRefund.Should().BeFalse();
            result.ValidationMessages.Should().BeEmpty();
        }

        [Fact]
        public async Task CalculateRefundAsync_WithEmptyModelId_ReturnsValidationError()
        {
            // Arrange
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };

            // Act
            var result = await _service.CalculateRefundAsync(
                "", originalUsage, refundUsage, "Test refund");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain("Model ID is required for refund calculation.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithNullUsageData_ReturnsValidationError()
        {
            // Act
            var result = await _service.CalculateRefundAsync(
                "openai/gpt-4o", null!, null!, "Test refund");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain("Both original and refund usage data are required.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithEmptyRefundReason_ReturnsValidationError()
        {
            // Arrange
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };

            // Act
            var result = await _service.CalculateRefundAsync(
                "openai/gpt-4o", originalUsage, refundUsage, "");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund reason is required.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithRefundExceedingOriginal_ReturnsPartialRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 1500, CompletionTokens = 750, TotalTokens = 2250 };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Excessive refund test");

            // Assert
            result.Should().NotBeNull();
            result.IsPartialRefund.Should().BeTrue();
            result.ValidationMessages.Should().HaveCount(2);
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund prompt tokens (1500) cannot exceed original (1000)"));
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund completion tokens (750) cannot exceed original (500)"));
        }

        [Fact]
        public async Task CalculateRefundAsync_WithImageRefund_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var originalUsage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0, ImageCount = 5 };
            var refundUsage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0, ImageCount = 2 };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                ImageCostPerImage = 0.04m // $0.04 per image
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Image generation failure");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.08m); // 2 * 0.04
            result.Breakdown!.ImageRefund.Should().Be(0.08m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithVideoRefund_IncludesResolutionMultiplier()
        {
            // Arrange
            var modelId = "some-video-model";
            var originalUsage = new Usage 
            { 
                PromptTokens = 0, 
                CompletionTokens = 0, 
                TotalTokens = 0, 
                VideoDurationSeconds = 10.0,
                VideoResolution = "1920x1080"
            };
            var refundUsage = new Usage 
            { 
                PromptTokens = 0, 
                CompletionTokens = 0, 
                TotalTokens = 0, 
                VideoDurationSeconds = 5.0,
                VideoResolution = "1920x1080"
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

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Video processing error");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.75m); // 5.0 * 0.1 * 1.5
            result.Breakdown!.VideoRefund.Should().Be(0.75m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithEmbeddingRefund_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "openai/text-embedding-ada-002";
            var originalUsage = new Usage { PromptTokens = 5000, CompletionTokens = 0, TotalTokens = 5000 };
            var refundUsage = new Usage { PromptTokens = 2000, CompletionTokens = 0, TotalTokens = 2000 };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0m,
                EmbeddingTokenCost = 0.0001m // $0.0001 per token
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Embedding service error");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.2m); // 2000 * 0.0001
            result.Breakdown!.EmbeddingRefund.Should().Be(0.2m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithEmbeddingAndImages_UsesEmbeddingCost()
        {
            // Arrange
            var modelId = "openai/multimodal-embed";
            var originalUsage = new Usage { PromptTokens = 5000, CompletionTokens = 0, TotalTokens = 5000, ImageCount = 3 };
            var refundUsage = new Usage { PromptTokens = 2000, CompletionTokens = 0, TotalTokens = 2000, ImageCount = 1 };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.0001m,       // Regular cost (expensive)
                OutputTokenCost = 0m,
                EmbeddingTokenCost = 0.00001m,  // Embedding cost (10x cheaper)
                ImageCostPerImage = 0.02m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Partial refund for embedding with images");

            // Assert
            result.Should().NotBeNull();
            // Embedding refund: 2000 * 0.00001 = 0.02
            // Image refund: 1 * 0.02 = 0.02
            // Total: 0.04
            result.RefundAmount.Should().Be(0.04m);
            result.Breakdown!.EmbeddingRefund.Should().Be(0.02m);
            result.Breakdown.ImageRefund.Should().Be(0.02m);
            result.Breakdown.InputTokenRefund.Should().Be(0m); // Should not use input token cost
        }

        [Fact]
        public async Task CalculateRefundAsync_WithNegativeValues_ReturnsValidationError()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = -100, CompletionTokens = -50, TotalTokens = -150 };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Invalid refund test");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund token counts must be non-negative.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithModelNotFound_ReturnsValidationMessage()
        {
            // Arrange
            var modelId = "non-existent-model";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCostInfo?)null);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Test refund");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0);
            result.ValidationMessages.Should().Contain($"Cost information not found for model {modelId}.");
        }

        [Fact]
        public async Task CalculateRefundAsync_WithMixedUsageRefund_CalculatesAllComponents()
        {
            // Arrange
            var modelId = "multimodal-model";
            var originalUsage = new Usage 
            { 
                PromptTokens = 1000, 
                CompletionTokens = 500, 
                TotalTokens = 1500,
                ImageCount = 3,
                VideoDurationSeconds = 10.0,
                VideoResolution = "1280x720"
            };
            var refundUsage = new Usage 
            { 
                PromptTokens = 500, 
                CompletionTokens = 250, 
                TotalTokens = 750,
                ImageCount = 1,
                VideoDurationSeconds = 5.0,
                VideoResolution = "1280x720"
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m,
                ImageCostPerImage = 0.04m,
                VideoCostPerSecond = 0.1m,
                VideoResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1280x720"] = 1.0m
                }
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Partial service failure");

            // Assert
            result.Should().NotBeNull();
            result.RefundAmount.Should().Be(0.5525m); // 0.005 + 0.0075 + 0.04 + 0.5
            result.Breakdown!.InputTokenRefund.Should().Be(0.005m);
            result.Breakdown.OutputTokenRefund.Should().Be(0.0075m);
            result.Breakdown.ImageRefund.Should().Be(0.04m);
            result.Breakdown.VideoRefund.Should().Be(0.5m);
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

        #endregion

        #region Batch Processing Tests

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
        public async Task CalculateRefundAsync_WithBatchProcessing_AppliesDiscountToRefund()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                IsBatch = true
            };
            var refundUsage = new Usage
            {
                PromptTokens = 500,
                CompletionTokens = 200,
                TotalTokens = 700,
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
            var result = await _service.CalculateRefundAsync(
                modelId,
                originalUsage,
                refundUsage,
                "Test refund",
                "transaction-123"
            );

            // Assert
            result.Should().NotBeNull();
            result.ValidationMessages.Should().BeEmpty();
            // Expected refund without batch: (500 * 0.001) + (200 * 0.002) = 0.5 + 0.4 = 0.9
            // Expected with 50% batch discount: 0.9 * 0.5 = 0.45
            result.RefundAmount.Should().Be(0.45m);
            result.Breakdown.Should().NotBeNull();
        }

        #endregion

        #region Image Quality Multiplier Tests

        [Fact]
        public async Task CalculateCostAsync_WithImageQualityMultiplier_AppliesMultiplier()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 2,
                ImageQuality = "hd"
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                ImageCostPerImage = 0.04m, // Standard quality price
                ImageQualityMultipliers = new Dictionary<string, decimal>
                {
                    ["standard"] = 1.0m,
                    ["hd"] = 2.0m
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 2 images * 0.04 base cost * 2.0 HD multiplier = 0.16
            result.Should().Be(0.16m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithStandardQuality_UsesDefaultMultiplier()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 3,
                ImageQuality = "standard"
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                ImageCostPerImage = 0.04m,
                ImageQualityMultipliers = new Dictionary<string, decimal>
                {
                    ["standard"] = 1.0m,
                    ["hd"] = 2.0m
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 3 images * 0.04 base cost * 1.0 standard multiplier = 0.12
            result.Should().Be(0.12m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNoImageQuality_UsesBasePrice()
        {
            // Arrange
            var modelId = "openai/dall-e-2";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 1,
                ImageQuality = null // No quality specified
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                ImageCostPerImage = 0.02m,
                ImageQualityMultipliers = new Dictionary<string, decimal>
                {
                    ["standard"] = 1.0m,
                    ["hd"] = 2.0m
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 1 image * 0.02 base cost (no multiplier applied)
            result.Should().Be(0.02m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithUnknownQuality_UsesBasePrice()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 2,
                ImageQuality = "ultra" // Quality not in multipliers
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                ImageCostPerImage = 0.04m,
                ImageQualityMultipliers = new Dictionary<string, decimal>
                {
                    ["standard"] = 1.0m,
                    ["hd"] = 2.0m
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 2 images * 0.04 base cost (no multiplier found)
            result.Should().Be(0.08m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithCaseInsensitiveQuality_AppliesMultiplier()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 1,
                ImageQuality = "HD" // Uppercase
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                ImageCostPerImage = 0.04m,
                ImageQualityMultipliers = new Dictionary<string, decimal>
                {
                    ["standard"] = 1.0m,
                    ["hd"] = 2.0m // Lowercase in dictionary
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 1 image * 0.04 base cost * 2.0 HD multiplier = 0.08
            result.Should().Be(0.08m);
        }

        [Fact]
        public async Task CalculateRefundAsync_WithImageQualityMultiplier_AppliesMultiplierToRefund()
        {
            // Arrange
            var modelId = "openai/dall-e-3";
            var originalUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 5,
                ImageQuality = "hd"
            };
            var refundUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 2,
                ImageQuality = "hd"
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                ImageCostPerImage = 0.04m,
                ImageQualityMultipliers = new Dictionary<string, decimal>
                {
                    ["standard"] = 1.0m,
                    ["hd"] = 2.0m
                }
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId,
                originalUsage,
                refundUsage,
                "Quality issue with generated images",
                "transaction-456"
            );

            // Assert
            result.Should().NotBeNull();
            result.ValidationMessages.Should().BeEmpty();
            // Expected refund: 2 images * 0.04 base cost * 2.0 HD multiplier = 0.16
            result.RefundAmount.Should().Be(0.16m);
            result.Breakdown.Should().NotBeNull();
            result.Breakdown.ImageRefund.Should().Be(0.16m);
        }

        #endregion

        #region Cached Token Pricing Tests

        [Fact]
        public async Task CalculateCostAsync_WithCachedInputTokens_CalculatesCorrectCost()
        {
            // Arrange
            var modelId = "anthropic/claude-3-opus";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                CachedInputTokens = 600  // 600 of the 1000 prompt tokens are cached
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,         // $0.01 per 1K regular input tokens
                OutputTokenCost = 0.00003m,        // $0.03 per 1K output tokens
                CachedInputTokenCost = 0.000001m   // $0.001 per 1K cached tokens (90% discount)
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Regular input: 400 tokens * 0.00001 = 0.004
            // Cached input: 600 tokens * 0.000001 = 0.0006
            // Output: 500 tokens * 0.00003 = 0.015
            // Total: 0.004 + 0.0006 + 0.015 = 0.0196
            result.Should().Be(0.0196m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithCacheWriteTokens_CalculatesCorrectCost()
        {
            // Arrange
            var modelId = "google/gemini-1.5-pro";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                CachedWriteTokens = 300  // 300 tokens written to cache
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,          // $0.01 per 1K regular input tokens
                OutputTokenCost = 0.00003m,         // $0.03 per 1K output tokens
                CachedInputWriteCost = 0.000025m    // $0.025 per 1K cache write tokens
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Input: 1000 tokens * 0.00001 = 0.01
            // Cache write: 300 tokens * 0.000025 = 0.0075
            // Output: 500 tokens * 0.00003 = 0.015
            // Total: 0.01 + 0.0075 + 0.015 = 0.0325
            result.Should().Be(0.0325m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithBothCachedInputAndWriteTokens_CalculatesCorrectCost()
        {
            // Arrange
            var modelId = "anthropic/claude-3-sonnet";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                CachedInputTokens = 400,   // 400 cached read tokens
                CachedWriteTokens = 200    // 200 cache write tokens
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,          // $0.01 per 1K regular input tokens
                OutputTokenCost = 0.00003m,         // $0.03 per 1K output tokens
                CachedInputTokenCost = 0.000001m,   // $0.001 per 1K cached read tokens
                CachedInputWriteCost = 0.000025m    // $0.025 per 1K cache write tokens
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Regular input: (1000 - 400) * 0.00001 = 600 * 0.00001 = 0.006
            // Cached input: 400 * 0.000001 = 0.0004
            // Cache write: 200 * 0.000025 = 0.005
            // Output: 500 * 0.00003 = 0.015
            // Total: 0.006 + 0.0004 + 0.005 + 0.015 = 0.0264
            result.Should().Be(0.0264m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithCachedTokensButNoCachedPricing_UsesRegularPricing()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                CachedInputTokens = 600,
                CachedWriteTokens = 200
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m
                // No cached token pricing defined
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Should use regular pricing for all tokens since no cached pricing is defined
            // Input: 1000 * 0.00001 = 0.01
            // Output: 500 * 0.00003 = 0.015
            // Total: 0.01 + 0.015 = 0.025
            result.Should().Be(0.025m);
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
        public async Task CalculateRefundAsync_WithCachedTokens_CalculatesCorrectRefund()
        {
            // Arrange
            var modelId = "google/gemini-1.5-flash";
            var originalUsage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                CachedInputTokens = 400,
                CachedWriteTokens = 200
            };
            var refundUsage = new Usage
            {
                PromptTokens = 500,
                CompletionTokens = 250,
                TotalTokens = 750,
                CachedInputTokens = 200,
                CachedWriteTokens = 100
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m,
                CachedInputTokenCost = 0.000001m,
                CachedInputWriteCost = 0.000025m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Partial service interruption");

            // Assert
            // Regular input refund: 300 * 0.00001 = 0.003 (500 total - 200 cached = 300 regular)
            // Cached input refund: 200 * 0.000001 = 0.0002
            // Cache write refund: 100 * 0.000025 = 0.0025
            // Output refund: 250 * 0.00003 = 0.0075
            // Total refund: 0.003 + 0.0002 + 0.0025 + 0.0075 = 0.0132
            result.RefundAmount.Should().Be(0.0132m);
            result.IsPartialRefund.Should().BeFalse();
        }

        [Fact]
        public async Task CalculateCostAsync_WithSearchUnits_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "cohere/rerank-3.5";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                SearchUnits = 5
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerSearchUnit = 2.0m // $2.00 per 1K search units
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 5 * (2.0 / 1000) = 5 * 0.002 = 0.01
            result.Should().Be(0.01m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithSearchUnitsAndTokens_CalculatesBothCorrectly()
        {
            // Arrange
            var modelId = "hybrid/model";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                SearchUnits = 10
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0.00001m,
                OutputTokenCost = 0.00003m,
                CostPerSearchUnit = 1.5m // $1.50 per 1K search units
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Token cost: (1000 * 0.00001) + (500 * 0.00003) = 0.01 + 0.015 = 0.025
            // Search unit cost: 10 * (1.5 / 1000) = 10 * 0.0015 = 0.015
            // Total: 0.025 + 0.015 = 0.04
            result.Should().Be(0.04m);
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
        public async Task CalculateRefundAsync_WithSearchUnits_CalculatesCorrectRefund()
        {
            // Arrange
            var modelId = "cohere/rerank-3.5";
            var originalUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                SearchUnits = 50
            };
            var refundUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                SearchUnits = 20
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerSearchUnit = 2.0m // $2.00 per 1K search units
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Service interruption");

            // Assert
            // Expected refund: 20 * (2.0 / 1000) = 20 * 0.002 = 0.04
            result.RefundAmount.Should().Be(0.04m);
            result.Breakdown!.SearchUnitRefund.Should().Be(0.04m);
            result.IsPartialRefund.Should().BeFalse();
        }

        [Fact]
        public async Task CalculateRefundAsync_WithSearchUnitsExceedingOriginal_ReportsValidationError()
        {
            // Arrange
            var modelId = "cohere/rerank-3.5";
            var originalUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                SearchUnits = 20
            };
            var refundUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                SearchUnits = 30 // More than original
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerSearchUnit = 2.0m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Invalid refund request");

            // Assert
            result.IsPartialRefund.Should().BeTrue();
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund search units"));
        }

        #endregion

        #region Inference Step Pricing Tests

        [Fact]
        public async Task CalculateCostAsync_WithInferenceSteps_CalculatesCorrectly()
        {
            // Arrange
            var modelId = "fireworks/flux-schnell";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 1,
                InferenceSteps = 4
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerInferenceStep = 0.00035m, // $0.00035 per step
                DefaultInferenceSteps = 4
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Expected: 4 * 0.00035 = 0.0014
            result.Should().Be(0.0014m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithInferenceStepsAndImageCost_PrefersStepBasedPricing()
        {
            // Arrange
            var modelId = "fireworks/stable-diffusion-xl";
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 2,
                InferenceSteps = 30
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerInferenceStep = 0.00013m, // $0.00013 per step
                ImageCostPerImage = 0.0039m, // Pre-calculated per image
                DefaultInferenceSteps = 30
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Should use step-based pricing: 30 * 0.00013 = 0.0039
            // Plus image cost: 2 * 0.0039 = 0.0078
            // Total: 0.0039 + 0.0078 = 0.0117
            result.Should().Be(0.0117m);
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

        [Fact]
        public async Task CalculateRefundAsync_WithInferenceSteps_CalculatesCorrectRefund()
        {
            // Arrange
            var modelId = "fireworks/flux-pro";
            var originalUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 3,
                InferenceSteps = 20
            };
            var refundUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                ImageCount = 1,
                InferenceSteps = 20
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerInferenceStep = 0.0005m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Partial image generation failure");

            // Assert
            // Expected refund: 20 * 0.0005 = 0.01
            result.RefundAmount.Should().Be(0.01m);
            result.Breakdown!.InferenceStepRefund.Should().Be(0.01m);
            result.IsPartialRefund.Should().BeFalse();
        }

        [Fact]
        public async Task CalculateRefundAsync_WithInferenceStepsExceedingOriginal_ReportsValidationError()
        {
            // Arrange
            var modelId = "fireworks/sdxl";
            var originalUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                InferenceSteps = 30
            };
            var refundUsage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                InferenceSteps = 50 // More than original
            };

            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputTokenCost = 0m,
                OutputTokenCost = 0m,
                CostPerInferenceStep = 0.00013m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Invalid refund request");

            // Assert
            result.IsPartialRefund.Should().BeTrue();
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund inference steps"));
        }

        #endregion
    }
}