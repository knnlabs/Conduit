using System;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Services;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Http
{
    public class RealtimeUsageTrackerTests
    {
        private readonly Mock<ILogger<RealtimeUsageTracker>> _mockLogger;
        private readonly Mock<ConduitLLM.Configuration.Services.IModelCostService> _mockCostService;
        private readonly Mock<ConduitLLM.Configuration.Services.IVirtualKeyService> _mockVirtualKeyService;
        private readonly RealtimeUsageTracker _tracker;

        public RealtimeUsageTrackerTests()
        {
            _mockLogger = new Mock<ILogger<RealtimeUsageTracker>>();
            _mockCostService = new Mock<ConduitLLM.Configuration.Services.IModelCostService>();
            _mockVirtualKeyService = new Mock<ConduitLLM.Configuration.Services.IVirtualKeyService>();
            _tracker = new RealtimeUsageTracker(
                _mockLogger.Object,
                _mockCostService.Object,
                _mockVirtualKeyService.Object);
        }

        [Fact]
        public async Task RecordFunctionCallAsync_Should_Track_Function_Calls()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var virtualKey = new VirtualKey 
            { 
                Id = 1, 
                KeyHash = "test-key",
                IsEnabled = true 
            };
            
            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByIdAsync(1))
                .ReturnsAsync(virtualKey);

            // Start tracking
            await _tracker.StartTrackingAsync(connectionId, 1, "gpt-4", "openai");

            // Act
            await _tracker.RecordFunctionCallAsync(connectionId, "get_weather");
            await _tracker.RecordFunctionCallAsync(connectionId, "search_web");
            await _tracker.RecordFunctionCallAsync(connectionId);

            // Get usage details
            var details = await _tracker.GetUsageDetailsAsync(connectionId);

            // Assert
            Assert.NotNull(details);
            Assert.Equal(3, details.FunctionCalls);
        }

        [Fact]
        public async Task GetEstimatedCostAsync_Should_Include_Function_Call_Costs()
        {
            // Arrange
            var connectionId = "test-connection-2";
            var virtualKey = new VirtualKey 
            { 
                Id = 2, 
                KeyHash = "test-key-2",
                IsEnabled = true 
            };
            
            var modelCost = new ModelCost
            {
                Model = "gpt-4",
                InputTokenCost = 0.01m, // $0.01 per 1K tokens
                OutputTokenCost = 0.03m // $0.03 per 1K tokens
            };

            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByIdAsync(2))
                .ReturnsAsync(virtualKey);
            _mockCostService.Setup(x => x.GetCostForModelAsync("gpt-4"))
                .ReturnsAsync(modelCost);

            // Start tracking
            await _tracker.StartTrackingAsync(connectionId, 2, "gpt-4", "openai");

            // Record some usage
            await _tracker.RecordTokenUsageAsync(connectionId, new Usage 
            { 
                PromptTokens = 1000, 
                CompletionTokens = 500 
            });
            
            // Record function calls
            await _tracker.RecordFunctionCallAsync(connectionId, "function1");
            await _tracker.RecordFunctionCallAsync(connectionId, "function2");

            // Act
            var estimatedCost = await _tracker.GetEstimatedCostAsync(connectionId);

            // Assert
            // Token cost: (1000/1000 * 0.01) + (500/1000 * 0.03) = 0.01 + 0.015 = 0.025
            // Function cost: 2 * (100/1000 * 0.03) = 2 * 0.003 = 0.006
            // Total: 0.025 + 0.006 = 0.031
            Assert.Equal(0.031m, estimatedCost);
        }

        [Fact]
        public async Task FinalizeUsageAsync_Should_Update_Virtual_Key_Spend_With_Function_Costs()
        {
            // Arrange
            var connectionId = "test-connection-3";
            var virtualKey = new VirtualKey 
            { 
                Id = 3, 
                KeyHash = "test-key-3",
                IsEnabled = true 
            };
            
            var modelCost = new ModelCost
            {
                Model = "gpt-4",
                InputTokenCost = 0.01m,
                OutputTokenCost = 0.03m
            };

            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByIdAsync(3))
                .ReturnsAsync(virtualKey);
            _mockCostService.Setup(x => x.GetCostForModelAsync("gpt-4"))
                .ReturnsAsync(modelCost);

            // Start tracking
            await _tracker.StartTrackingAsync(connectionId, 3, "gpt-4", "openai");

            // Record usage including function calls
            await _tracker.RecordFunctionCallAsync(connectionId, "test_function");
            
            // Act
            var finalCost = await _tracker.FinalizeUsageAsync(connectionId, null);

            // Assert
            _mockVirtualKeyService.Verify(x => x.UpdateSpendAsync(3, It.IsAny<decimal>()), Times.Once);
        }

        [Fact]
        public async Task GetUsageDetailsAsync_Should_Include_Function_Call_Cost_Breakdown()
        {
            // Arrange
            var connectionId = "test-connection-4";
            var virtualKey = new VirtualKey 
            { 
                Id = 4, 
                KeyHash = "test-key-4",
                IsEnabled = true 
            };
            
            var modelCost = new ModelCost
            {
                Model = "gpt-4",
                InputTokenCost = 0.01m,
                OutputTokenCost = 0.03m
            };

            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByIdAsync(4))
                .ReturnsAsync(virtualKey);
            _mockCostService.Setup(x => x.GetCostForModelAsync("gpt-4"))
                .ReturnsAsync(modelCost);

            // Start tracking
            await _tracker.StartTrackingAsync(connectionId, 4, "gpt-4", "openai");

            // Record function calls
            await _tracker.RecordFunctionCallAsync(connectionId, "function1");
            await _tracker.RecordFunctionCallAsync(connectionId, "function2");
            await _tracker.RecordFunctionCallAsync(connectionId, "function3");

            // Act
            var details = await _tracker.GetUsageDetailsAsync(connectionId);

            // Assert
            Assert.NotNull(details);
            Assert.Equal(3, details.FunctionCalls);
            Assert.NotNull(details.Costs);
            // 3 function calls * 100 tokens each / 1000 * $0.03 = 0.009
            Assert.Equal(0.009m, details.Costs.FunctionCallCost);
        }
    }
}