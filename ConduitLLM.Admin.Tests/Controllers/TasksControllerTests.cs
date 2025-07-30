using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Controllers
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
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            
            var response = okResult.Value.GetType().GetProperty("cleaned_up")?.GetValue(okResult.Value);
            var hours = okResult.Value.GetType().GetProperty("older_than_hours")?.GetValue(okResult.Value);
            
            Assert.Equal(42, response);
            Assert.Equal(24, hours);

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

            // Act
            var result = await _controller.CleanupOldTasks();

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.NotNull(objectResult.Value);
            
            // Verify the error response structure
            var errorProp = objectResult.Value.GetType().GetProperty("error")?.GetValue(objectResult.Value);
            Assert.NotNull(errorProp);
            
            var messageProp = errorProp.GetType().GetProperty("message")?.GetValue(errorProp);
            var typeProp = errorProp.GetType().GetProperty("type")?.GetValue(errorProp);
            
            Assert.Equal("An error occurred while cleaning up tasks", messageProp);
            Assert.Equal("server_error", typeProp);
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
    }
}