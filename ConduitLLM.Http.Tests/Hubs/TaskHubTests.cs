using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Tests.Hubs
{
    public class TaskHubTests
    {
        private readonly Mock<ILogger<TaskHub>> _loggerMock;
        private readonly Mock<IAsyncTaskService> _taskServiceMock;
        private readonly Mock<IHubContext<TaskHub>> _hubContextMock;
        private readonly Mock<IHubClients> _clientsMock;
        private readonly Mock<IClientProxy> _clientProxyMock;
        private readonly TaskHub _hub;
        private readonly Mock<HubCallerContext> _contextMock;
        private readonly Mock<IGroupManager> _groupsMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;

        public TaskHubTests()
        {
            _loggerMock = new Mock<ILogger<TaskHub>>();
            _taskServiceMock = new Mock<IAsyncTaskService>();
            _hubContextMock = new Mock<IHubContext<TaskHub>>();
            _clientsMock = new Mock<IHubClients>();
            _clientProxyMock = new Mock<IClientProxy>();
            _contextMock = new Mock<HubCallerContext>();
            _groupsMock = new Mock<IGroupManager>();
            _serviceProviderMock = new Mock<IServiceProvider>();

            _hubContextMock.Setup(x => x.Clients).Returns(_clientsMock.Object);
            _clientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);

            _hub = new TaskHub(_loggerMock.Object, _taskServiceMock.Object, _hubContextMock.Object, _serviceProviderMock.Object)
            {
                Context = _contextMock.Object,
                Groups = _groupsMock.Object
            };
        }

        [Fact]
        public async Task OnConnectedAsync_WithValidVirtualKey_AddsToGroup()
        {
            // Arrange
            var virtualKeyId = 123;
            var virtualKeyName = "TestKey";
            var connectionId = "test-connection-id";
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId,
                ["VirtualKeyName"] = virtualKeyName
            };

            _contextMock.Setup(x => x.Items).Returns(items);
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _groupsMock.Verify(x => x.AddToGroupAsync(connectionId, $"vkey-{virtualKeyId}", default), Times.Once);
        }

        [Fact]
        public async Task OnConnectedAsync_WithoutVirtualKey_AbortsConnection()
        {
            // Arrange
            var items = new Dictionary<object, object?>();
            _contextMock.Setup(x => x.Items).Returns(items);

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _contextMock.Verify(x => x.Abort(), Times.Once);
            _groupsMock.Verify(x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        }

        [Fact]
        public async Task SubscribeToTask_WithValidTask_AddsToTaskGroup()
        {
            // Arrange
            var virtualKeyId = 123;
            var taskId = "task-123";
            var connectionId = "test-connection-id";
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                Metadata = new TaskMetadata(virtualKeyId)
            };

            _contextMock.Setup(x => x.Items).Returns(items);
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);
            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, default))
                .ReturnsAsync(taskStatus);

            // Act
            await _hub.SubscribeToTask(taskId);

            // Assert
            _groupsMock.Verify(x => x.AddToGroupAsync(connectionId, $"task-{taskId}", default), Times.Once);
        }

        [Fact]
        public async Task SubscribeToTask_WithNonExistentTask_ThrowsHubException()
        {
            // Arrange
            var virtualKeyId = 123;
            var taskId = "task-123";
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId
            };

            _contextMock.Setup(x => x.Items).Returns(items);
            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, default))
                .ReturnsAsync((AsyncTaskStatus?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HubException>(() => _hub.SubscribeToTask(taskId));
            Assert.Equal("Task not found", exception.Message);
        }

        [Fact]
        public async Task SubscribeToTask_WithUnauthorizedAccess_ThrowsHubException()
        {
            // Arrange
            var virtualKeyId = 123;
            var otherKeyId = 456;
            var taskId = "task-123";
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                Metadata = new TaskMetadata(otherKeyId)
            };

            _contextMock.Setup(x => x.Items).Returns(items);
            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, default))
                .ReturnsAsync(taskStatus);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HubException>(() => _hub.SubscribeToTask(taskId));
            Assert.Equal("Unauthorized access to task", exception.Message);
        }

        [Fact(Skip = "Test setup needs updating")]
        public async Task TaskStarted_SendsNotificationToCorrectGroups()
        {
            // Arrange
            var taskId = "task-123";
            var taskType = "test_operation";
            var virtualKeyId = 123;
            var metadata = new Dictionary<string, object> { ["virtualKeyId"] = virtualKeyId };

            // Act
            await _hub.TaskStarted(taskId, taskType, metadata);

            // Assert
            _clientsMock.Verify(x => x.Group($"task-{taskId}"), Times.Once);
            _clientsMock.Verify(x => x.Group($"vkey-{virtualKeyId}-{taskType}"), Times.Once);
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "TaskStarted",
                It.Is<object[]>(args => args.Length == 3 && 
                    args[0].ToString() == taskId && 
                    args[1].ToString() == taskType),
                default), Times.Exactly(2));
        }

        [Fact]
        public async Task TaskProgress_SendsNotificationToTaskGroup()
        {
            // Arrange
            var taskId = "task-123";
            var progress = 50;
            var message = "Processing...";

            // Act
            await _hub.TaskProgress(taskId, progress, message);

            // Assert
            _clientsMock.Verify(x => x.Group($"task-{taskId}"), Times.Once);
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "TaskProgress",
                It.Is<object[]>(args => args.Length == 3 && 
                    args[0].ToString() == taskId && 
                    (int)args[1] == progress &&
                    args[2].ToString() == message),
                default), Times.Once);
        }

        [Fact]
        public async Task UnsubscribeFromTask_RemovesFromTaskGroup()
        {
            // Arrange
            var taskId = "task-123";
            var connectionId = "test-connection-id";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.UnsubscribeFromTask(taskId);

            // Assert
            _groupsMock.Verify(x => x.RemoveFromGroupAsync(connectionId, $"task-{taskId}", default), Times.Once);
        }

        [Fact]
        public async Task SubscribeToTaskType_AddsToTaskTypeGroup()
        {
            // Arrange
            var virtualKeyId = 123;
            var taskType = "test_operation";
            var connectionId = "test-connection-id";
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId
            };

            _contextMock.Setup(x => x.Items).Returns(items);
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.SubscribeToTaskType(taskType);

            // Assert
            _groupsMock.Verify(x => x.AddToGroupAsync(connectionId, $"vkey-{virtualKeyId}-{taskType}", default), Times.Once);
        }
    }
}