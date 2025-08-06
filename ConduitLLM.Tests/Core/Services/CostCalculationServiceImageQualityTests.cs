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
    /// Tests for image quality multiplier functionality
    /// </summary>
    public class CostCalculationServiceImageQualityTests : CostCalculationServiceTestBase
    {
        public CostCalculationServiceImageQualityTests(ITestOutputHelper output) : base(output)
        {
        }

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
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
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
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
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
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
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
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
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
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
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
    }
}