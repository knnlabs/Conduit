using ConduitLLM.Core.Models;
using ConduitLLM.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class CostCalculationServiceAdditionalTests
    {
        #region Logging Tests

        [Fact]
        public async Task CalculateRefundAsync_LogsRefundInformation()
        {
            // Arrange
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };
            var refundReason = "Service interruption";
            var originalTransactionId = "txn_12345";

            var modelCost = new ModelCost
            {
                CostName = modelId,
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 30.00m
            };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCost);

            // Act
            await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, refundReason, originalTransactionId);

            // Assert
            _loggerMock.VerifyLog(
                LogLevel.Information,
                "Calculated refund for model",
                Times.Once());
        }

        [Fact]
        public async Task CalculateRefundAsync_WithModelNotFound_LogsWarning()
        {
            // Arrange
            var modelId = "non-existent-model";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };

            _modelCostServiceMock.Setup(m => m.GetCostForModelAsync(modelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelCost?)null);

            // Act
            await _service.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Test refund");

            // Assert
            _loggerMock.VerifyLog(
                LogLevel.Warning,
                "Cost information not found for model",
                Times.Once());
        }

        #endregion
    }
}