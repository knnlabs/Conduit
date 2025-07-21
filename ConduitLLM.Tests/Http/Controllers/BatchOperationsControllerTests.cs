using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Controllers;
using ConduitLLM.Http.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class BatchOperationsControllerTests : ControllerTestBase
    {
        private readonly Mock<ILogger<BatchOperationsController>> _mockLogger;
        private readonly Mock<IBatchOperationService> _mockBatchOperationService;
        private readonly Mock<IBatchSpendUpdateOperation> _mockBatchSpendUpdateOperation;
        private readonly Mock<IBatchVirtualKeyUpdateOperation> _mockBatchVirtualKeyUpdateOperation;
        private readonly Mock<IBatchWebhookSendOperation> _mockBatchWebhookSendOperation;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly BatchOperationsController _controller;

        public BatchOperationsControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = CreateLogger<BatchOperationsController>();
            _mockBatchOperationService = new Mock<IBatchOperationService>();
            _mockBatchSpendUpdateOperation = new Mock<IBatchSpendUpdateOperation>();
            _mockBatchVirtualKeyUpdateOperation = new Mock<IBatchVirtualKeyUpdateOperation>();
            _mockBatchWebhookSendOperation = new Mock<IBatchWebhookSendOperation>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            
            _controller = new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                _mockBatchSpendUpdateOperation.Object,
                _mockBatchVirtualKeyUpdateOperation.Object,
                _mockBatchWebhookSendOperation.Object,
                _mockVirtualKeyService.Object);
                
            _controller.ControllerContext = CreateControllerContext();
        }

        #region GetOperationStatus Tests

        [Fact]
        public void GetOperationStatus_WithExistingOperation_ShouldReturnOk()
        {
            // Arrange
            var operationId = "op-123";
            var status = new BatchOperationStatus
            {
                OperationId = operationId,
                OperationType = "spend_update",
                Status = BatchOperationStatusEnum.Running,
                TotalItems = 100,
                ProcessedCount = 50,
                SuccessCount = 45,
                FailedCount = 5,
                ProgressPercentage = 50,
                ElapsedTime = TimeSpan.FromSeconds(30),
                EstimatedTimeRemaining = TimeSpan.FromSeconds(30),
                ItemsPerSecond = 1.67,
                CanCancel = true
            };

            _mockBatchOperationService.Setup(x => x.GetOperationStatus(operationId))
                .Returns(status);

            // Act
            var result = _controller.GetOperationStatus(operationId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<BatchOperationStatusResponse>(okResult.Value);
            Assert.Equal(operationId, response.OperationId);
            Assert.Equal("Running", response.Status);
            Assert.Equal(50, response.ProcessedCount);
        }

        [Fact]
        public void GetOperationStatus_WithNonExistentOperation_ShouldReturnNotFound()
        {
            // Arrange
            var operationId = "non-existent";
            _mockBatchOperationService.Setup(x => x.GetOperationStatus(operationId))
                .Returns((BatchOperationStatus)null);

            // Act
            var result = _controller.GetOperationStatus(operationId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("Operation not found", error.error.ToString());
        }

        #endregion

        #region CancelOperation Tests

        [Fact]
        public async Task CancelOperation_WithCancellableOperation_ShouldReturnNoContent()
        {
            // Arrange
            var operationId = "op-123";
            var status = new BatchOperationStatus
            {
                OperationId = operationId,
                CanCancel = true
            };

            _mockBatchOperationService.Setup(x => x.GetOperationStatus(operationId))
                .Returns(status);

            _mockBatchOperationService.Setup(x => x.CancelBatchOperationAsync(operationId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.CancelOperation(operationId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task CancelOperation_WithNonCancellableOperation_ShouldReturnConflict()
        {
            // Arrange
            var operationId = "op-123";
            var status = new BatchOperationStatus
            {
                OperationId = operationId,
                CanCancel = false
            };

            _mockBatchOperationService.Setup(x => x.GetOperationStatus(operationId))
                .Returns(status);

            // Act
            var result = await _controller.CancelOperation(operationId);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            dynamic error = conflictResult.Value;
            Assert.Equal("Operation cannot be cancelled", error.error.ToString());
        }

        [Fact]
        public async Task CancelOperation_WithFailedCancellation_ShouldReturnConflict()
        {
            // Arrange
            var operationId = "op-123";
            var status = new BatchOperationStatus
            {
                OperationId = operationId,
                CanCancel = true
            };

            _mockBatchOperationService.Setup(x => x.GetOperationStatus(operationId))
                .Returns(status);

            _mockBatchOperationService.Setup(x => x.CancelBatchOperationAsync(operationId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.CancelOperation(operationId);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            dynamic error = conflictResult.Value;
            Assert.Equal("Failed to cancel operation", error.error.ToString());
        }

        [Fact]
        public async Task CancelOperation_WithNonExistentOperation_ShouldReturnNotFound()
        {
            // Arrange
            var operationId = "non-existent";
            _mockBatchOperationService.Setup(x => x.GetOperationStatus(operationId))
                .Returns((BatchOperationStatus)null);

            // Act
            var result = await _controller.CancelOperation(operationId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("Operation not found", error.error.ToString());
        }

        #endregion

        #region StartBatchSpendUpdate Tests

        [Fact]
        public async Task StartBatchSpendUpdate_WithValidRequest_ShouldReturnAccepted()
        {
            // Arrange
            var request = new BatchSpendUpdateRequest
            {
                Updates = new List<SpendUpdateDto>
                {
                    new SpendUpdateDto
                    {
                        VirtualKeyId = 1,
                        Amount = 10.5m,
                        Model = "gpt-4",
                        Provider = "openai"
                    }
                }
            };

            // Setup claims with VirtualKeyId
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("VirtualKeyId", "1")
            }, "test"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            var expectedResult = new BatchOperationResult
            {
                OperationId = "batch-op-123",
                TotalItems = 1,
                SuccessCount = 0,
                FailedCount = 0,
                Duration = TimeSpan.Zero
            };

            _mockBatchSpendUpdateOperation.Setup(x => x.ExecuteAsync(
                    It.IsAny<List<SpendUpdateItem>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.StartBatchSpendUpdate(request);

            // Assert
            var acceptedResult = Assert.IsType<AcceptedResult>(result);
            var response = Assert.IsType<BatchOperationStartResponse>(acceptedResult.Value);
            Assert.Equal("batch-op-123", response.OperationId);
            Assert.Equal("spend_update", response.OperationType);
            Assert.Equal(1, response.TotalItems);
        }

        [Fact]
        public async Task StartBatchSpendUpdate_WithEmptyUpdates_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new BatchSpendUpdateRequest
            {
                Updates = new List<SpendUpdateDto>()
            };

            // Setup claims with VirtualKeyId
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("VirtualKeyId", "1")
            }, "test"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Act
            var result = await _controller.StartBatchSpendUpdate(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic error = badRequestResult.Value;
            Assert.Equal("No updates provided", error.error.ToString());
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new BatchOperationsController(
                null,
                _mockBatchOperationService.Object,
                _mockBatchSpendUpdateOperation.Object,
                _mockBatchVirtualKeyUpdateOperation.Object,
                _mockBatchWebhookSendOperation.Object,
                _mockVirtualKeyService.Object));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullBatchOperationService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new BatchOperationsController(
                _mockLogger.Object,
                null,
                _mockBatchSpendUpdateOperation.Object,
                _mockBatchVirtualKeyUpdateOperation.Object,
                _mockBatchWebhookSendOperation.Object,
                _mockVirtualKeyService.Object));
            Assert.Equal("batchOperationService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullBatchSpendUpdateOperation_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                null,
                _mockBatchVirtualKeyUpdateOperation.Object,
                _mockBatchWebhookSendOperation.Object,
                _mockVirtualKeyService.Object));
            Assert.Equal("batchSpendUpdateOperation", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullBatchVirtualKeyUpdateOperation_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                _mockBatchSpendUpdateOperation.Object,
                null,
                _mockBatchWebhookSendOperation.Object,
                _mockVirtualKeyService.Object));
            Assert.Equal("batchVirtualKeyUpdateOperation", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullBatchWebhookSendOperation_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                _mockBatchSpendUpdateOperation.Object,
                _mockBatchVirtualKeyUpdateOperation.Object,
                null,
                _mockVirtualKeyService.Object));
            Assert.Equal("batchWebhookSendOperation", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullVirtualKeyService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                _mockBatchSpendUpdateOperation.Object,
                _mockBatchVirtualKeyUpdateOperation.Object,
                _mockBatchWebhookSendOperation.Object,
                null));
            Assert.Equal("virtualKeyService", ex.ParamName);
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(BatchOperationsController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }

        #endregion
    }
}