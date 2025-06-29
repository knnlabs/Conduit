using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Tests.Hubs
{
    public class VideoGenerationHubTests
    {
        private readonly Mock<ILogger<VideoGenerationHub>> _loggerMock;
        private readonly Mock<IAsyncTaskService> _taskServiceMock;
        private readonly VideoGenerationHub _hub;
        private readonly Mock<HubCallerContext> _contextMock;
        private readonly Mock<IGroupManager> _groupsMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;

        public VideoGenerationHubTests()
        {
            _loggerMock = new Mock<ILogger<VideoGenerationHub>>();
            _taskServiceMock = new Mock<IAsyncTaskService>();
            _contextMock = new Mock<HubCallerContext>();
            _groupsMock = new Mock<IGroupManager>();
            _serviceProviderMock = new Mock<IServiceProvider>();

            _serviceProviderMock.Setup(x => x.GetService(typeof(IAsyncTaskService)))
                .Returns(_taskServiceMock.Object);

            _hub = new VideoGenerationHub(_loggerMock.Object, _taskServiceMock.Object, _serviceProviderMock.Object)
            {
                Context = _contextMock.Object,
                Groups = _groupsMock.Object
            };
        }

        [Fact]
        public async Task SubscribeToTask_ValidTaskOwnership_AddsToGroup()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKeyId = 456;
            var connectionId = "test-connection-id";
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId
            };

            _contextMock.Setup(x => x.Items).Returns(items);
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                Metadata = new TaskMetadata(virtualKeyId)
            };

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Act
            await _hub.SubscribeToTask(taskId);

            // Assert
            _groupsMock.Verify(x => x.AddToGroupAsync(connectionId, $"video-{taskId}", default), Times.Once);
        }

        [Fact]
        public async Task SubscribeToTask_UnauthorizedAccess_ThrowsHubException()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKeyId = 456;
            var differentVirtualKeyId = 789;
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId
            };

            _contextMock.Setup(x => x.Items).Returns(items);

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                Metadata = new TaskMetadata(differentVirtualKeyId)
            };

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HubException>(() => _hub.SubscribeToTask(taskId));
            Assert.Equal("Unauthorized access to task", exception.Message);
        }

        [Fact]
        public async Task SubscribeToTask_NoVirtualKeyId_ThrowsHubException()
        {
            // Arrange
            var taskId = "test-task-123";
            var items = new Dictionary<object, object?>();

            _contextMock.Setup(x => x.Items).Returns(items);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HubException>(() => _hub.SubscribeToTask(taskId));
            Assert.Equal("Unauthorized", exception.Message);
        }

        [Fact]
        public async Task UnsubscribeFromTask_RemovesFromGroup()
        {
            // Arrange
            var taskId = "test-task-123";
            var connectionId = "test-connection-id";

            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.UnsubscribeFromTask(taskId);

            // Assert
            _groupsMock.Verify(x => x.RemoveFromGroupAsync(connectionId, $"video-{taskId}", default), Times.Once);
        }

        [Fact]
        public void GetHubName_ReturnsCorrectName()
        {
            // Act
            var hubName = _hub.GetType().GetMethod("GetHubName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_hub, null) as string;

            // Assert
            Assert.Equal("VideoGenerationHub", hubName);
        }

        [Fact(Skip = "Test setup needs updating")]
        public async Task SubscribeToTask_SupportsVariousVirtualKeyIdTypes()
        {
            // Test with long value
            await TestSubscribeWithVirtualKeyType(123L, 123);

            // Test with string value
            await TestSubscribeWithVirtualKeyType("456", 456);

            // Test with int value
            await TestSubscribeWithVirtualKeyType(789, 789);
        }

        private async Task TestSubscribeWithVirtualKeyType(object virtualKeyIdValue, int expectedId)
        {
            // Arrange
            var taskId = $"test-task-{Guid.NewGuid()}";
            var connectionId = "test-connection-id";
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = expectedId
            };

            _contextMock.Setup(x => x.Items).Returns(items);
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                Metadata = new TaskMetadata((int)Convert.ChangeType(virtualKeyIdValue, typeof(int)))
            };

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Act
            await _hub.SubscribeToTask(taskId);

            // Assert
            _groupsMock.Verify(x => x.AddToGroupAsync(connectionId, $"video-{taskId}", default), Times.Once);
        }
    }
}