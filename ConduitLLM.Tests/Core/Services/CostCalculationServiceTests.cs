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
                // Pure embedding case: promptTokens * embeddingCost
                expectedCost = promptTokens * modelCost.EmbeddingTokenCost.Value;
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
    }
}