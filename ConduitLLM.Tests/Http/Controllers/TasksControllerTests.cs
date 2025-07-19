using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Controllers;
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
    public class TasksControllerTests : ControllerTestBase
    {
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<ILogger<TasksController>> _mockLogger;
        private readonly TasksController _controller;

        public TasksControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockLogger = CreateLogger<TasksController>();
            _controller = new TasksController(_mockTaskService.Object, _mockLogger.Object);
        }

        #region Constructor Tests

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

        #endregion

        #region GetTaskStatus Tests

        [Fact]
        public async Task GetTaskStatus_WithValidTaskId_ShouldReturnOkWithStatus()
        {
            // Arrange
            var taskId = "task-123";
            var expectedStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Completed,
                Progress = 100,
                ProgressMessage = "Task completed successfully",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedStatus);

            // Act
            var result = await _controller.GetTaskStatus(taskId);

            // Assert
            AssertOkObjectResult<AsyncTaskStatus>(result, status =>
            {
                Assert.Equal(taskId, status.TaskId);
                Assert.Equal(TaskState.Completed, status.State);
                Assert.Equal(100, status.Progress);
                Assert.Equal("Task completed successfully", status.ProgressMessage);
            });

            _mockTaskService.Verify(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetTaskStatus_WithNonExistentTask_ShouldReturnNotFound()
        {
            // Arrange
            var taskId = "non-existent-task";
            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Task not found"));

            // Act
            var result = await _controller.GetTaskStatus(taskId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.NotNull(notFoundResult.Value);
            
            var errorResponse = notFoundResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Task not found", errorResponse.error.message.ToString());
            Assert.Equal("not_found", errorResponse.error.type.ToString());
        }

        [Fact]
        public async Task GetTaskStatus_WithServiceException_ShouldReturn500()
        {
            // Arrange
            var taskId = "task-123";
            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.GetTaskStatus(taskId);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            
            var errorResponse = objectResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("An error occurred while retrieving the task", errorResponse.error.message.ToString());
            Assert.Equal("server_error", errorResponse.error.type.ToString());
        }

        #endregion

        #region CancelTask Tests

        [Fact]
        public async Task CancelTask_WithValidTaskId_ShouldReturnNoContent()
        {
            // Arrange
            var taskId = "task-123";
            _mockTaskService.Setup(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.CancelTask(taskId);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockTaskService.Verify(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelTask_WithNonExistentTask_ShouldReturnNotFound()
        {
            // Arrange
            var taskId = "non-existent-task";
            _mockTaskService.Setup(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Task not found"));

            // Act
            var result = await _controller.CancelTask(taskId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = notFoundResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Task not found", errorResponse.error.message.ToString());
            Assert.Equal("not_found", errorResponse.error.type.ToString());
        }

        [Fact]
        public async Task CancelTask_WithServiceException_ShouldReturn500()
        {
            // Arrange
            var taskId = "task-123";
            _mockTaskService.Setup(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.CancelTask(taskId);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            
            var errorResponse = objectResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("An error occurred while cancelling the task", errorResponse.error.message.ToString());
            Assert.Equal("server_error", errorResponse.error.type.ToString());
        }

        #endregion

        #region PollTask Tests

        [Fact]
        public async Task PollTask_WithValidTaskId_ShouldReturnCompletedStatus()
        {
            // Arrange
            var taskId = "task-123";
            var expectedStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Completed,
                Progress = 100,
                ProgressMessage = "Task completed successfully"
            };

            _mockTaskService.Setup(x => x.PollTaskUntilCompletedAsync(
                    taskId, 
                    It.IsAny<TimeSpan>(), 
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedStatus);

            // Act
            var result = await _controller.PollTask(taskId);

            // Assert
            AssertOkObjectResult<AsyncTaskStatus>(result, status =>
            {
                Assert.Equal(taskId, status.TaskId);
                Assert.Equal(TaskState.Completed, status.State);
            });
        }

        [Fact]
        public async Task PollTask_WithCustomTimeoutAndInterval_ShouldUseClampedValues()
        {
            // Arrange
            var taskId = "task-123";
            var timeout = 700; // Above max, should be clamped to 600
            var interval = 0; // Below min, should be clamped to 1
            
            _mockTaskService.Setup(x => x.PollTaskUntilCompletedAsync(
                    taskId,
                    TimeSpan.FromSeconds(1), // Clamped interval
                    TimeSpan.FromSeconds(600), // Clamped timeout
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncTaskStatus { TaskId = taskId });

            // Act
            var result = await _controller.PollTask(taskId, timeout, interval);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockTaskService.Verify(x => x.PollTaskUntilCompletedAsync(
                taskId,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(600),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PollTask_WithTimeout_ShouldReturn408()
        {
            // Arrange
            var taskId = "task-123";
            _mockTaskService.Setup(x => x.PollTaskUntilCompletedAsync(
                    taskId,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            var result = await _controller.PollTask(taskId);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(408, objectResult.StatusCode);
            
            var errorResponse = objectResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Task polling timed out", errorResponse.error.message.ToString());
            Assert.Equal("timeout", errorResponse.error.type.ToString());
        }

        [Fact]
        public async Task PollTask_WithNonExistentTask_ShouldReturnNotFound()
        {
            // Arrange
            var taskId = "non-existent-task";
            _mockTaskService.Setup(x => x.PollTaskUntilCompletedAsync(
                    taskId,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Task not found"));

            // Act
            var result = await _controller.PollTask(taskId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = notFoundResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Task not found", errorResponse.error.message.ToString());
        }

        [Fact]
        public async Task PollTask_WithServiceException_ShouldReturn500()
        {
            // Arrange
            var taskId = "task-123";
            _mockTaskService.Setup(x => x.PollTaskUntilCompletedAsync(
                    taskId,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.PollTask(taskId);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            
            var errorResponse = objectResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("An error occurred while polling the task", errorResponse.error.message.ToString());
        }

        #endregion

        #region CleanupOldTasks Tests

        [Fact]
        public async Task CleanupOldTasks_WithValidRequest_ShouldReturnCleanedUpCount()
        {
            // Arrange
            var expectedCount = 42;
            _mockTaskService.Setup(x => x.CleanupOldTasksAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedCount);

            _controller.ControllerContext = CreateControllerContextWithUser("admin-user");

            // Act
            var result = await _controller.CleanupOldTasks();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value as dynamic;
            Assert.NotNull(response);
            Assert.Equal(42, (int)response.cleaned_up);

            _mockTaskService.Verify(x => x.CleanupOldTasksAsync(TimeSpan.FromHours(24), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CleanupOldTasks_WithCustomHours_ShouldUseProvidedValue()
        {
            // Arrange
            var olderThanHours = 48;
            _mockTaskService.Setup(x => x.CleanupOldTasksAsync(TimeSpan.FromHours(48), It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);

            _controller.ControllerContext = CreateControllerContextWithUser("admin-user");

            // Act
            var result = await _controller.CleanupOldTasks(olderThanHours);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockTaskService.Verify(x => x.CleanupOldTasksAsync(TimeSpan.FromHours(48), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CleanupOldTasks_WithInvalidHours_ShouldClampToMinimum()
        {
            // Arrange
            var olderThanHours = 0; // Below minimum
            _mockTaskService.Setup(x => x.CleanupOldTasksAsync(TimeSpan.FromHours(1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);

            _controller.ControllerContext = CreateControllerContextWithUser("admin-user");

            // Act
            var result = await _controller.CleanupOldTasks(olderThanHours);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockTaskService.Verify(x => x.CleanupOldTasksAsync(TimeSpan.FromHours(1), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CleanupOldTasks_WithServiceException_ShouldReturn500()
        {
            // Arrange
            _mockTaskService.Setup(x => x.CleanupOldTasksAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            _controller.ControllerContext = CreateControllerContextWithUser("admin-user");

            // Act
            var result = await _controller.CleanupOldTasks();

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            
            var errorResponse = objectResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("An error occurred while cleaning up tasks", errorResponse.error.message.ToString());
            Assert.Equal("server_error", errorResponse.error.type.ToString());
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(TasksController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }

        [Fact]
        public void CleanupOldTasks_ShouldRequireMasterKeyPolicy()
        {
            // Arrange & Act
            var method = typeof(TasksController).GetMethod(nameof(TasksController.CleanupOldTasks));
            var authorizeAttribute = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            // Assert
            Assert.NotNull(authorizeAttribute);
            Assert.Equal("MasterKey", authorizeAttribute.Policy);
        }

        #endregion
    }
}