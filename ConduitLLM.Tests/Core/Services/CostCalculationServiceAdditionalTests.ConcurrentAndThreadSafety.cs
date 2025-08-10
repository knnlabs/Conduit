using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CostCalculationServiceAdditionalTests
    {
        #region Concurrent and Thread Safety Tests

        [Fact]
        public async Task CalculateCostAsync_ConcurrentCalls_HandledCorrectly()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m
            };

            _modelCostServiceMock
                .Setup(x => x.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            var tasks = new List<Task<decimal>>();

            // Act - Create 100 concurrent tasks
            for (int i = 0; i < 100; i++)
            {
                var usage = new Usage
                {
                    PromptTokens = i * 10,
                    CompletionTokens = i * 5,
                    TotalTokens = i * 15
                };
                tasks.Add(_service.CalculateCostAsync(modelId, usage));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - Verify all calculations are correct
            for (int i = 0; i < 100; i++)
            {
                var expectedCost = (i * 10 * 0.00001m) + (i * 5 * 0.00003m);
                results[i].Should().Be(expectedCost);
            }
        }

        #endregion
    }
}