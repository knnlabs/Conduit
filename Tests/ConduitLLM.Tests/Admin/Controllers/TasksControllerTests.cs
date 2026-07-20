using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Core.Interfaces;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Admin")]
    public class TasksControllerTests
    {
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<ILogger<TasksController>> _mockLogger;
        private readonly TasksController _controller;

        public TasksControllerTests()
        {
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockLogger = new Mock<ILogger<TasksController>>();
            _controller = new TasksController(_mockTaskService.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullTaskService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TasksController(null, _mockLogger.Object));
            Assert.Equal("taskService", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TasksController(_mockTaskService.Object, null));
            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public async Task CleanupOldTasks_WithValidRequest_ShouldReturnCleanedUpCount()
        {
            // Arrange
            var expectedCount = 42;
            _mockTaskService.Setup(x => x.CleanupOldTasksAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedCount);

            // Act
            var result = await _controller.CleanupOldTasks();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<TaskCleanupResponseDto>().Subject;
            Assert.Equal(42, response.CleanedUp);
            Assert.Equal(24, response.OlderThanHours);

            _mockTaskService.Verify(x => x.CleanupOldTasksAsync(TimeSpan.FromHours(24), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CleanupOldTasks_WithCustomHours_ShouldUseProvidedValue()
        {
            // Arrange
            var olderThanHours = 48;
            _mockTaskService.Setup(x => x.CleanupOldTasksAsync(TimeSpan.FromHours(48), It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);

            // Act
            var result = await _controller.CleanupOldTasks(olderThanHours);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockTaskService.Verify(x => x.CleanupOldTasksAsync(TimeSpan.FromHours(48), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CleanupOldTasks_WithInvalidHours_ShouldClampToMinimum()
        {
            // Arrange
            var olderThanHours = 0; // Below minimum
            _mockTaskService.Setup(x => x.CleanupOldTasksAsync(TimeSpan.FromHours(1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);

            // Act
            var result = await _controller.CleanupOldTasks(olderThanHours);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockTaskService.Verify(x => x.CleanupOldTasksAsync(TimeSpan.FromHours(1), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CleanupOldTasks_WithServiceException_ShouldPropagateException()
        {
            // Arrange
            _mockTaskService.Setup(x => x.CleanupOldTasksAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var act = async () => await _controller.CleanupOldTasks();

            // Assert - exception propagates to AdminExceptionMiddleware, which owns error mapping
            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public void Controller_ShouldRequireMasterKeyAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(TasksController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))
                as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            // Assert
            Assert.NotNull(authorizeAttribute);
            Assert.Equal("MasterKeyPolicy", authorizeAttribute.Policy);
        }

        [Fact]
        public async Task ResolveIndeterminateTask_SafeToRetry_UsesGuardedServiceTransition()
        {
            _mockTaskService.Setup(x => x.ResolveIndeterminateTaskAsync(
                    "task-1", IndeterminateTaskResolution.SafeToRetry, "provider confirmed absent",
                    "provider-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _controller.ResolveIndeterminateTask(
                "task-1",
                new ResolveIndeterminateTaskDto
                {
                    Resolution = "safe_to_retry",
                    Reason = "provider confirmed absent",
                    ProviderOperationId = "provider-1"
                },
                CancellationToken.None);

            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task ResolveIndeterminateTask_InvalidResolution_ReturnsBadRequest()
        {
            var result = await _controller.ResolveIndeterminateTask(
                "task-1",
                new ResolveIndeterminateTaskDto { Resolution = "retry_now", Reason = "unsafe" },
                CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>();
            _mockTaskService.Verify(x => x.ResolveIndeterminateTaskAsync(
                It.IsAny<string>(), It.IsAny<IndeterminateTaskResolution>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
