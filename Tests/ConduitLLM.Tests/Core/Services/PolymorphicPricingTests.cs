using System.Text.Json;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Pricing;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    public class PolymorphicPricingTests
    {
        private readonly Mock<IModelCostService> _mockModelCostService;
        private readonly Mock<ILogger<CostCalculationService>> _mockLogger;
        private readonly CostCalculationService _service;

        public PolymorphicPricingTests()
        {
            _mockModelCostService = new Mock<IModelCostService>();
            _mockLogger = new Mock<ILogger<CostCalculationService>>();
            _service = new CostCalculationService(_mockModelCostService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task CalculateCost_PerVideo_MiniMaxHailuo_CorrectFlatRate()
        {
            // Arrange
            var modelId = "minimax/hailuo-02";
            var usage = new Usage
            {
                VideoDurationSeconds = 6,
                VideoResolution = "768p"
            };

            var config = new PerVideoPricingConfig
            {
                Rates = new Dictionary<string, decimal>
                {
                    ["512p_6"] = 0.10m,
                    ["512p_10"] = 0.15m,
                    ["768p_6"] = 0.28m,
                    ["768p_10"] = 0.56m,
                    ["1080p_6"] = 0.49m
                }
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerVideo,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            Assert.Equal(0.28m, cost); // Should return flat rate for 768p_6
        }

        [Fact]
        public async Task CalculateCost_PerVideo_CamelCaseJson_ReturnsConfiguredRate()
        {
            var modelId = "minimax/hailuo-camel-case";
            var usage = new Usage
            {
                VideoDurationSeconds = 6,
                VideoResolution = "768p"
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerVideo,
                PricingConfiguration = "{\"rates\":{\"768p_6\":0.28}}"
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            var cost = await _service.CalculateCostAsync(modelId, usage);

            Assert.Equal(0.28m, cost);
        }

        [Fact]
        public async Task CalculateCost_PerVideo_MissingExactKey_UsesHighestConfiguredRateAndMarksFallback()
        {
            var modelId = "video/measured-duration";
            var usage = new Usage
            {
                VideoDurationSeconds = 6.2,
                VideoResolution = "1280x720"
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerVideo,
                PricingConfiguration = "{\"rates\":{\"720p_5\":0.25,\"1080p_10\":0.75}}"
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            var cost = await _service.CalculateCostAsync(modelId, usage);

            Assert.Equal(0.25m, cost);
            Assert.Contains("Missing per-video rate '720p_6'", usage.PricingFallbackReason);
            Assert.Contains("720p_5", usage.PricingFallbackReason);
        }

        [Fact]
        public async Task CalculateCost_PerVideo_MissingResolutionDuration_UsesConservativeFallback()
        {
            // Arrange
            var modelId = "minimax/hailuo-02";
            var usage = new Usage
            {
                VideoDurationSeconds = 7, // No pricing for 7 seconds
                VideoResolution = "768p"
            };

            var config = new PerVideoPricingConfig
            {
                Rates = new Dictionary<string, decimal>
                {
                    ["768p_6"] = 0.28m,
                    ["768p_10"] = 0.56m
                }
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerVideo,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            var cost = await _service.CalculateCostAsync(modelId, usage);

            Assert.Equal(0.56m, cost);
            Assert.NotNull(usage.PricingFallbackReason);
        }

        [Fact]
        public async Task CalculateCost_PerSecondVideo_Replicate_WithMultiplier()
        {
            // Arrange
            var modelId = "replicate/minimax-video";
            var usage = new Usage
            {
                VideoDurationSeconds = 10,
                VideoResolution = "1080p"
            };

            var config = new PerSecondVideoPricingConfig
            {
                BaseRate = 0.09m,
                ResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["720p"] = 1.0m,
                    ["1080p"] = 1.5m,
                    ["4k"] = 2.5m
                }
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerSecondVideo,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            Assert.Equal(1.35m, cost); // 10 seconds * 0.09 * 1.5 = 1.35
        }

        [Fact]
        public async Task CalculateCost_PerSecondVideo_UnknownResolution_UsesHighestMultiplierAndMarksFallback()
        {
            var modelId = "video/unknown-resolution";
            var usage = new Usage
            {
                VideoDurationSeconds = 10,
                VideoResolution = "1440p"
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerSecondVideo,
                PricingConfiguration = "{\"baseRate\":0.10,\"resolutionMultipliers\":{\"720p\":1.0,\"1080p\":1.5}}"
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            var cost = await _service.CalculateCostAsync(modelId, usage);

            Assert.Equal(1.50m, cost);
            Assert.Contains("Unknown per-second video resolution '1440p'", usage.PricingFallbackReason);
            Assert.Contains("1080p", usage.PricingFallbackReason);
        }

        [Fact]
        public async Task CalculateCost_InferenceSteps_Fireworks_UsesProvidedSteps()
        {
            // Arrange
            var modelId = "fireworks/flux-schnell";
            var usage = new Usage
            {
                InferenceSteps = 4,
                ImageCount = 1
            };

            var config = new InferenceStepsPricingConfig
            {
                CostPerStep = 0.00035m,
                DefaultSteps = 4,
                ModelSteps = new Dictionary<string, int>
                {
                    ["flux-schnell"] = 4,
                    ["flux-dev"] = 20,
                    ["sdxl"] = 30
                }
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.InferenceSteps,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            Assert.Equal(0.0014m, cost); // 4 steps * 0.00035 = 0.0014
        }

        [Fact]
        public async Task CalculateCost_InferenceSteps_UsesDefaultWhenNotProvided()
        {
            // Arrange
            var modelId = "fireworks/sdxl";
            var usage = new Usage
            {
                ImageCount = 1
                // No InferenceSteps provided
            };

            var config = new InferenceStepsPricingConfig
            {
                CostPerStep = 0.00013m,
                DefaultSteps = 30
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.InferenceSteps,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            Assert.Equal(0.0039m, cost); // 30 steps * 0.00013 = 0.0039
        }

        [Fact]
        public async Task CalculateCost_InferenceSteps_CamelCaseJson_UsesConfiguredValues()
        {
            var modelId = "fireworks/sdxl-camel-case";
            var usage = new Usage { ImageCount = 1 };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.InferenceSteps,
                PricingConfiguration = "{\"costPerStep\":0.00013,\"defaultSteps\":30}"
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            var cost = await _service.CalculateCostAsync(modelId, usage);

            Assert.Equal(0.0039m, cost);
        }

        [Fact]
        public async Task CalculateCost_TieredTokens_MiniMaxM1_Under200K()
        {
            // Arrange
            var modelId = "minimax/m1";
            var usage = new Usage
            {
                PromptTokens = 150000,
                CompletionTokens = 30000
            };

            var config = new TieredTokensPricingConfig
            {
                Tiers = new List<TokenPricingTier>
                {
                    new TokenPricingTier { MaxContext = 200000, InputCost = 400, OutputCost = 2200 },
                    new TokenPricingTier { MaxContext = null, InputCost = 1300, OutputCost = 2200 }
                }
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.TieredTokens,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Prompt context: 150000 (under 200K)
            // Input: 150000 * 400 / 1000000 = 60
            // Output: 30000 * 2200 / 1000000 = 66
            Assert.Equal(126m, cost);
        }

        [Fact]
        public async Task CalculateCost_TieredTokens_MiniMaxM1_Over200K()
        {
            // Arrange
            var modelId = "minimax/m1";
            var usage = new Usage
            {
                PromptTokens = 250000,
                CompletionTokens = 50000
            };

            var config = new TieredTokensPricingConfig
            {
                Tiers = new List<TokenPricingTier>
                {
                    new TokenPricingTier { MaxContext = 200000, InputCost = 400, OutputCost = 2200 },
                    new TokenPricingTier { MaxContext = null, InputCost = 1300, OutputCost = 2200 }
                }
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.TieredTokens,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Prompt context: 250000 (over 200K, use higher tier)
            // Input: 250000 * 1300 / 1000000 = 325
            // Output: 50000 * 2200 / 1000000 = 110
            Assert.Equal(435m, cost);
        }

        [Fact]
        public async Task CalculateCost_TieredTokens_CamelCaseJson_UsesConfiguredTier()
        {
            var modelId = "minimax/m1-camel-case";
            var usage = new Usage
            {
                PromptTokens = 1_000,
                CompletionTokens = 500
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.TieredTokens,
                PricingConfiguration =
                    "{\"tiers\":[{\"maxContext\":200000,\"inputCost\":10,\"outputCost\":20}]}"
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            var cost = await _service.CalculateCostAsync(modelId, usage);

            Assert.Equal(0.02m, cost);
        }

        [Fact]
        public async Task CalculateCost_TieredTokens_SelectsTierFromPromptTokensOnly()
        {
            var modelId = "tiered/prompt-context";
            var usage = new Usage { PromptTokens = 190000, CompletionTokens = 50000 };
            var config = new TieredTokensPricingConfig
            {
                Tiers =
                [
                    new TokenPricingTier { MaxContext = 200000, InputCost = 1m, OutputCost = 2m },
                    new TokenPricingTier { MaxContext = null, InputCost = 10m, OutputCost = 20m }
                ]
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.TieredTokens,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };
            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default)).ReturnsAsync(modelCost);

            var cost = await _service.CalculateCostAsync(modelId, usage);

            Assert.Equal(0.29m, cost);
        }

        [Fact]
        public async Task CalculateCost_TieredTokens_UsesSortedHighestTierForOverLimitPrompt()
        {
            var modelId = "tiered/over-limit";
            var usage = new Usage { PromptTokens = 300000, CompletionTokens = 10000 };
            var config = new TieredTokensPricingConfig
            {
                Tiers =
                [
                    new TokenPricingTier { MaxContext = 200000, InputCost = 10m, OutputCost = 20m },
                    new TokenPricingTier { MaxContext = 100000, InputCost = 1m, OutputCost = 2m }
                ]
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.TieredTokens,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };
            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default)).ReturnsAsync(modelCost);

            var cost = await _service.CalculateCostAsync(modelId, usage);

            Assert.Equal(3.2m, cost);
        }

        [Fact]
        public async Task CalculateCost_TieredTokens_PricesCachedReasoningAndSearchUsage()
        {
            var modelId = "tiered/additional-usage";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                CachedInputTokens = 400,
                CachedWriteTokens = 200,
                ReasoningTokens = 300,
                SearchUnits = 10
            };
            var config = new TieredTokensPricingConfig
            {
                Tiers = [new TokenPricingTier { MaxContext = null, InputCost = 10m, OutputCost = 20m }]
            };
            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.TieredTokens,
                PricingConfiguration = JsonSerializer.Serialize(config),
                CachedInputCostPerMillionTokens = 2m,
                CachedInputWriteCostPerMillionTokens = 15m,
                ReasoningCostPerMillionTokens = 30m,
                CostPerSearchUnit = 1m
            };
            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default)).ReturnsAsync(modelCost);

            var cost = await _service.CalculateCostAsync(modelId, usage);

            // regular input .004 + cache read .0008 + cache write .003
            // regular output .004 + reasoning .009 + search .01
            Assert.Equal(0.0308m, cost);
        }

        [Fact]
        public async Task CalculateCost_PerImage_WithQualityAndResolutionMultipliers()
        {
            // Arrange
            var modelId = "replicate/flux-pro";
            var usage = new Usage
            {
                ImageCount = 2,
                ImageQuality = "hd",
                ImageResolution = "1792x1024"
            };

            var config = new PerImagePricingConfig
            {
                BaseRate = 0.04m,
                QualityMultipliers = new Dictionary<string, decimal>
                {
                    ["standard"] = 1.0m,
                    ["hd"] = 2.0m
                },
                ResolutionMultipliers = new Dictionary<string, decimal>
                {
                    ["1024x1024"] = 1.0m,
                    ["1792x1024"] = 1.5m,
                    ["1024x1792"] = 1.5m
                }
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerImage,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // 2 images * 0.04 base * 2.0 quality * 1.5 resolution = 0.24
            Assert.Equal(0.24m, cost);
        }

        [Fact]
        public async Task CalculateCost_PerImage_CamelCaseJson_ReturnsNonZeroCost()
        {
            // Regression test: documented/WebAdmin-produced PricingConfiguration is camelCase
            // (e.g. {"baseRate":0.05}). Case-sensitive parsing silently yielded BaseRate=0
            // and billed $0.00 for media generation.
            // Arrange
            var modelId = "minimax/image-01";
            var usage = new Usage
            {
                ImageCount = 2
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerImage,
                PricingConfiguration = "{\"baseRate\":0.05}"
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            Assert.Equal(0.10m, cost); // 2 images * 0.05 base rate
        }

        [Fact]
        public async Task CalculateCost_PerImage_CamelCaseJsonWithMultipliers_AppliesMultipliers()
        {
            // Arrange
            var modelId = "replicate/flux-pro";
            var usage = new Usage
            {
                ImageCount = 1,
                ImageQuality = "hd",
                ImageResolution = "1792x1024"
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerImage,
                PricingConfiguration =
                    "{\"baseRate\":0.04,\"qualityMultipliers\":{\"hd\":2.0},\"resolutionMultipliers\":{\"1792x1024\":1.5}}"
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            Assert.Equal(0.12m, cost); // 1 image * 0.04 base * 2.0 quality * 1.5 resolution
        }

        [Fact]
        public async Task CalculateCost_PerSecondVideo_CamelCaseJson_ReturnsNonZeroCost()
        {
            // Regression test: camelCase config must not deserialize BaseRate=0 (silent $0 billing).
            // Arrange
            var modelId = "replicate/minimax-video";
            var usage = new Usage
            {
                VideoDurationSeconds = 10,
                VideoResolution = "1080p"
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerSecondVideo,
                PricingConfiguration =
                    "{\"baseRate\":0.09,\"resolutionMultipliers\":{\"720p\":1.0,\"1080p\":1.5}}"
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            Assert.Equal(1.35m, cost); // 10 seconds * 0.09 * 1.5
        }

        [Fact]
        public async Task CalculateCost_Standard_WithBatchProcessing()
        {
            // Arrange
            var modelId = "minimax/text-01";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                IsBatch = true
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.Standard,
                InputCostPerMillionTokens = 200m,
                OutputCostPerMillionTokens = 1100m,
                SupportsBatchProcessing = true,
                BatchProcessingMultiplier = 0.5m // 50% discount
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            // Input: 1000 * 200 / 1000000 = 0.2
            // Output: 500 * 1100 / 1000000 = 0.55
            // Total: 0.75 * 0.5 (batch) = 0.375
            Assert.Equal(0.375m, cost);
        }

        [Fact]
        public async Task CalculateCost_NoModelCost_ThrowsForReconciliation()
        {
            // Arrange
            var modelId = "unknown/model";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync((ModelCost?)null);

            // Act
            var act = () => _service.CalculateCostAsync(modelId, usage);

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(act);
        }

        [Fact]
        public async Task CalculateCost_PerVideo_NoUsageData_ReturnsZero()
        {
            // Arrange
            var modelId = "minimax/hailuo-02";
            var usage = new Usage(); // No video data

            var config = new PerVideoPricingConfig
            {
                Rates = new Dictionary<string, decimal>
                {
                    ["768p_6"] = 0.28m
                }
            };

            var modelCost = new ModelCost
            {
                CostName = modelId,
                PricingModel = PricingModel.PerVideo,
                PricingConfiguration = JsonSerializer.Serialize(config)
            };

            _mockModelCostService.Setup(x => x.GetCostForModelAsync(modelId, default))
                .ReturnsAsync(modelCost);

            // Act
            var cost = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            Assert.Equal(0m, cost);
        }
    }
}
