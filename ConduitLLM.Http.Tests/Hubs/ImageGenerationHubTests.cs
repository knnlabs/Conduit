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
using ConduitLLM.Http.Authentication;

namespace ConduitLLM.Http.Tests.Hubs
{
    public class ImageGenerationHubTests
    {
        private readonly Mock<ILogger<ImageGenerationHub>> _loggerMock;
        private readonly Mock<IAsyncTaskService> _taskServiceMock;
        private readonly Mock<ISignalRAuthenticationService> _authServiceMock;
        private readonly ImageGenerationHub _hub;
        private readonly Mock<HubCallerContext> _contextMock;
        private readonly Mock<IGroupManager> _groupsMock;
        private readonly IServiceProvider _serviceProvider;

        public ImageGenerationHubTests()
        {
            _loggerMock = new Mock<ILogger<ImageGenerationHub>>();
            _taskServiceMock = new Mock<IAsyncTaskService>();
            _authServiceMock = new Mock<ISignalRAuthenticationService>();
            _contextMock = new Mock<HubCallerContext>();
            _groupsMock = new Mock<IGroupManager>();

            // Build a real service provider
            var services = new ServiceCollection();
            services.AddSingleton(_authServiceMock.Object);
            services.AddSingleton(_taskServiceMock.Object);
            _serviceProvider = services.BuildServiceProvider();

            // Setup auth service to return virtual key ID from context
            _authServiceMock.Setup(x => x.GetVirtualKeyId(It.IsAny<HubCallerContext>()))
                .Returns<HubCallerContext>(ctx =>
                {
                    if (ctx?.Items?.TryGetValue("VirtualKeyId", out var idObj) == true && idObj is int id)
                        return id;
                    return null;
                });

            _authServiceMock.Setup(x => x.GetVirtualKeyName(It.IsAny<HubCallerContext>()))
                .Returns<HubCallerContext>(ctx =>
                {
                    if (ctx?.Items?.TryGetValue("VirtualKeyName", out var nameObj) == true && nameObj is string name)
                        return name;
                    return "test-key";
                });

            _authServiceMock.Setup(x => x.CanAccessResourceAsync(It.IsAny<HubCallerContext>(), "task", It.IsAny<string>()))
                .Returns<HubCallerContext, string, string>((ctx, resourceType, taskId) =>
                {
                    // Get the virtual key ID from context
                    var virtualKeyId = 0;
                    if (ctx?.Items?.TryGetValue("VirtualKeyId", out var idObj) == true && idObj is int id)
                        virtualKeyId = id;
                    
                    // Get the task to check ownership
                    var taskStatus = _taskServiceMock.Object.GetTaskStatusAsync(taskId).GetAwaiter().GetResult();
                    if (taskStatus?.Metadata?.VirtualKeyId == virtualKeyId)
                        return Task.FromResult(true);
                    
                    return Task.FromResult(false);
                });

            _hub = new ImageGenerationHub(_loggerMock.Object, _taskServiceMock.Object, _serviceProvider)
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
            _groupsMock.Verify(x => x.AddToGroupAsync(connectionId, $"image-{taskId}", default), Times.Once);
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
            _groupsMock.Verify(x => x.RemoveFromGroupAsync(connectionId, $"image-{taskId}", default), Times.Once);
        }

        [Fact]
        public void GetHubName_ReturnsCorrectName()
        {
            // Act
            var hubName = _hub.GetType().GetMethod("GetHubName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_hub, null) as string;

            // Assert
            Assert.Equal("ImageGenerationHub", hubName);
        }
    }
}