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
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;

        public BatchOperationsControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = CreateLogger<BatchOperationsController>();
            _mockBatchOperationService = new Mock<IBatchOperationService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
        }

        #region GetOperationStatus Tests

        [Fact]
        public void GetOperationStatus_WithExistingOperation_ShouldReturnOk()
        {
            // Arrange
            var controller = new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                null, // Can't mock these without interfaces
                null,
                null,
                _mockVirtualKeyService.Object);

            controller.ControllerContext = CreateControllerContext();

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
            var result = controller.GetOperationStatus(operationId);

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
            var controller = new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                null,
                null,
                null,
                _mockVirtualKeyService.Object);

            controller.ControllerContext = CreateControllerContext();

            var operationId = "non-existent";
            _mockBatchOperationService.Setup(x => x.GetOperationStatus(operationId))
                .Returns((BatchOperationStatus)null);

            // Act
            var result = controller.GetOperationStatus(operationId);

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
            var controller = new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                null,
                null,
                null,
                _mockVirtualKeyService.Object);

            controller.ControllerContext = CreateControllerContext();

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
            var result = await controller.CancelOperation(operationId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task CancelOperation_WithNonCancellableOperation_ShouldReturnConflict()
        {
            // Arrange
            var controller = new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                null,
                null,
                null,
                _mockVirtualKeyService.Object);

            controller.ControllerContext = CreateControllerContext();

            var operationId = "op-123";
            var status = new BatchOperationStatus
            {
                OperationId = operationId,
                CanCancel = false
            };

            _mockBatchOperationService.Setup(x => x.GetOperationStatus(operationId))
                .Returns(status);

            // Act
            var result = await controller.CancelOperation(operationId);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            dynamic error = conflictResult.Value;
            Assert.Equal("Operation cannot be cancelled", error.error.ToString());
        }

        [Fact]
        public async Task CancelOperation_WithFailedCancellation_ShouldReturnConflict()
        {
            // Arrange
            var controller = new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                null,
                null,
                null,
                _mockVirtualKeyService.Object);

            controller.ControllerContext = CreateControllerContext();

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
            var result = await controller.CancelOperation(operationId);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            dynamic error = conflictResult.Value;
            Assert.Equal("Failed to cancel operation", error.error.ToString());
        }

        [Fact]
        public async Task CancelOperation_WithNonExistentOperation_ShouldReturnNotFound()
        {
            // Arrange
            var controller = new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                null,
                null,
                null,
                _mockVirtualKeyService.Object);

            controller.ControllerContext = CreateControllerContext();

            var operationId = "non-existent";
            _mockBatchOperationService.Setup(x => x.GetOperationStatus(operationId))
                .Returns((BatchOperationStatus)null);

            // Act
            var result = await controller.CancelOperation(operationId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("Operation not found", error.error.ToString());
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BatchOperationsController(
                null,
                _mockBatchOperationService.Object,
                null,
                null,
                null,
                _mockVirtualKeyService.Object));
        }

        [Fact]
        public void Constructor_WithNullBatchOperationService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BatchOperationsController(
                _mockLogger.Object,
                null,
                null,
                null,
                null,
                _mockVirtualKeyService.Object));
        }

        [Fact]
        public void Constructor_WithNullVirtualKeyService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BatchOperationsController(
                _mockLogger.Object,
                _mockBatchOperationService.Object,
                null,
                null,
                null,
                null));
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