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
    public partial class CostCalculationServiceBasicTests
    {
        [Fact]
        public async Task CalculateCostAsync_WithNullModelId_ReturnsZero()
        {
            // Arrange
            var usage = new Usage { PromptTokens = 100, CompletionTokens = 50, TotalTokens = 150 };

            // Act
            var result = await _service.CalculateCostAsync(null, usage);

            // Assert
            result.Should().Be(0m);
            _modelCostServiceMock.Verify(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CalculateCostAsync_WithEmptyModelId_ReturnsZero()
        {
            // Arrange
            var usage = new Usage { PromptTokens = 100, CompletionTokens = 50, TotalTokens = 150 };

            // Act
            var result = await _service.CalculateCostAsync(string.Empty, usage);

            // Assert
            result.Should().Be(0m);
            _modelCostServiceMock.Verify(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CalculateCostAsync_WithNullUsage_ReturnsZero()
        {
            // Arrange
            var modelId = "openai/gpt-4o";

            // Act
            var result = await _service.CalculateCostAsync(modelId, null);

            // Assert
            result.Should().Be(0m);
            _modelCostServiceMock.Verify(x => x.GetCostForModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CalculateCostAsync_WithModelCostNotFound_ReturnsZero()
        {
            // Arrange
            var modelId = "unknown/model";
            var usage = new Usage { PromptTokens = 100, CompletionTokens = 50, TotalTokens = 150 };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCostInfo?)null);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            result.Should().Be(0m);
        }

        [Fact]
        public async Task CalculateCostAsync_WithZeroCosts_ReturnsZero()
        {
            // Arrange
            var modelId = "free/model";
            var usage = new Usage
            {
                PromptTokens = 1000,
                CompletionTokens = 500,
                TotalTokens = 1500,
                ImageCount = 5,
                VideoDurationSeconds = 10.0
            };
            var modelCost = new ModelCostInfo
            {
                ModelIdPattern = modelId,
                InputCostPerMillionTokens = 0m,
                OutputCostPerMillionTokens = 0m,
                ImageCostPerImage = 0m,
                VideoCostPerSecond = 0m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            var result = await _service.CalculateCostAsync(modelId, usage);

            // Assert
            result.Should().Be(0m);
        }
    }
}