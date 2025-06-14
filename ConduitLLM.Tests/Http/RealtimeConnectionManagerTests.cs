using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Http.Services;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Http
{
    public class RealtimeConnectionManagerTests
    {
        private readonly Mock<ILogger<RealtimeConnectionManager>> _mockLogger;
        private readonly RealtimeConnectionManager _connectionManager;

        public RealtimeConnectionManagerTests()
        {
            _mockLogger = new Mock<ILogger<RealtimeConnectionManager>>();
            _connectionManager = new RealtimeConnectionManager(_mockLogger.Object);
        }

        [Fact]
        public void RegisterConnection_Should_Add_Connection_Successfully()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var virtualKey = "test-key-1";
            var model = "test-model";
            var provider = "openai";

            // Act
            _connectionManager.RegisterConnection(connectionId, virtualKey, model, provider);

            // Assert
            var connections = _connectionManager.GetActiveConnections();
            Assert.Single(connections);
            Assert.Equal(connectionId, connections[0].ConnectionId);
            Assert.Equal(virtualKey, connections[0].VirtualKey);
            Assert.Equal(model, connections[0].Model);
            Assert.Equal(provider, connections[0].Provider);
        }

        [Fact]
        public void UnregisterConnection_Should_Remove_Connection()
        {
            // Arrange
            var connectionId = "test-connection-1";
            _connectionManager.RegisterConnection(connectionId, "key1", "model1", "provider1");

            // Act
            _connectionManager.UnregisterConnection(connectionId);

            // Assert
            var connections = _connectionManager.GetActiveConnections();
            Assert.Empty(connections);
        }

        [Fact]
        public void UpdateConnectionProvider_Should_Update_Provider_Info()
        {
            // Arrange
            var connectionId = "test-connection-1";
            _connectionManager.RegisterConnection(connectionId, "key1", "model1", "provider1");
            var newProviderConnection = "provider-connection-1";

            // Act
            _connectionManager.UpdateConnectionProvider(connectionId, newProviderConnection);

            // Assert
            var connections = _connectionManager.GetActiveConnections();
            Assert.Single(connections);
            Assert.Equal(newProviderConnection, connections[0].ProviderConnectionId);
        }

        [Fact]
        public void GetConnectionInfo_Should_Return_Correct_Info()
        {
            // Arrange
            var connectionId = "test-connection-1";
            var virtualKey = "test-key-1";
            var model = "test-model";
            var provider = "openai";
            _connectionManager.RegisterConnection(connectionId, virtualKey, model, provider);

            // Act
            var info = _connectionManager.GetConnectionInfo(connectionId);

            // Assert
            Assert.NotNull(info);
            Assert.Equal(connectionId, info.ConnectionId);
            Assert.Equal(virtualKey, info.VirtualKey);
            Assert.Equal(model, info.Model);
            Assert.Equal(provider, info.Provider);
        }

        [Fact]
        public void GetConnectionInfo_Should_Return_Null_For_NonExistent_Connection()
        {
            // Act
            var info = _connectionManager.GetConnectionInfo("non-existent");

            // Assert
            Assert.Null(info);
        }

        [Fact]
        public void GetConnectionsByVirtualKey_Should_Return_All_Connections_For_Key()
        {
            // Arrange
            var virtualKey = "test-key-1";
            _connectionManager.RegisterConnection("conn1", virtualKey, "model1", "provider1");
            _connectionManager.RegisterConnection("conn2", virtualKey, "model2", "provider2");
            _connectionManager.RegisterConnection("conn3", "other-key", "model3", "provider3");

            // Act
            var connections = _connectionManager.GetConnectionsByVirtualKey(virtualKey);

            // Assert
            Assert.Equal(2, connections.Count);
            Assert.All(connections, c => Assert.Equal(virtualKey, c.VirtualKey));
        }

        [Fact]
        public void IncrementUsage_Should_Update_Usage_Stats()
        {
            // Arrange
            var connectionId = "test-connection-1";
            _connectionManager.RegisterConnection(connectionId, "key1", "model1", "provider1");

            // Act
            _connectionManager.IncrementUsage(connectionId, 100, 50, 1.5m);

            // Assert
            var info = _connectionManager.GetConnectionInfo(connectionId);
            Assert.NotNull(info);
            Assert.Equal(100, info.AudioBytesProcessed);
            Assert.Equal(50, info.TokensUsed);
            Assert.Equal(1.5m, info.EstimatedCost);
        }

        [Fact]
        public void Multiple_Increments_Should_Accumulate()
        {
            // Arrange
            var connectionId = "test-connection-1";
            _connectionManager.RegisterConnection(connectionId, "key1", "model1", "provider1");

            // Act
            _connectionManager.IncrementUsage(connectionId, 100, 50, 1.0m);
            _connectionManager.IncrementUsage(connectionId, 200, 100, 2.0m);

            // Assert
            var info = _connectionManager.GetConnectionInfo(connectionId);
            Assert.NotNull(info);
            Assert.Equal(300, info.AudioBytesProcessed);
            Assert.Equal(150, info.TokensUsed);
            Assert.Equal(3.0m, info.EstimatedCost);
        }

        [Fact]
        public async Task Concurrent_Operations_Should_Be_ThreadSafe()
        {
            // Arrange
            var registerTasks = new Task[50];
            var unregisterTasks = new Task[25];
            var connectionCount = 0;

            // Act - Register connections concurrently
            for (int i = 0; i < 50; i++)
            {
                var index = i;
                registerTasks[i] = Task.Run(() =>
                {
                    _connectionManager.RegisterConnection($"conn-{index}", $"key-{index % 10}", "model", "provider");
                    Interlocked.Increment(ref connectionCount);
                });
            }

            // Wait for all registrations to complete
            await Task.WhenAll(registerTasks);

            // Act - Unregister some connections concurrently
            for (int i = 0; i < 25; i++)
            {
                var index = i * 2; // Every even-numbered connection
                unregisterTasks[i] = Task.Run(() =>
                {
                    _connectionManager.UnregisterConnection($"conn-{index}");
                    Interlocked.Decrement(ref connectionCount);
                });
            }

            await Task.WhenAll(unregisterTasks);

            // Assert
            var connections = _connectionManager.GetActiveConnections();
            Assert.Equal(25, connections.Count); // 50 registered - 25 unregistered
        }

        [Fact]
        public void GetActiveConnections_Should_Return_All_Active_Connections()
        {
            // Arrange
            _connectionManager.RegisterConnection("conn1", "key1", "model1", "provider1");
            _connectionManager.RegisterConnection("conn2", "key2", "model2", "provider2");
            _connectionManager.RegisterConnection("conn3", "key3", "model3", "provider3");

            // Act
            var connections = _connectionManager.GetActiveConnections();

            // Assert
            Assert.Equal(3, connections.Count);
            Assert.Contains(connections, c => c.ConnectionId == "conn1");
            Assert.Contains(connections, c => c.ConnectionId == "conn2");
            Assert.Contains(connections, c => c.ConnectionId == "conn3");
        }

        [Fact]
        public void Connection_Should_Track_Timestamps()
        {
            // Arrange
            var before = DateTime.UtcNow;
            var connectionId = "test-connection-1";

            // Act
            _connectionManager.RegisterConnection(connectionId, "key1", "model1", "provider1");
            var after = DateTime.UtcNow;

            // Assert
            var info = _connectionManager.GetConnectionInfo(connectionId);
            Assert.NotNull(info);
            Assert.InRange(info.StartTime, before, after);
            Assert.InRange(info.LastActivity, before, after);
        }

        [Fact]
        public void IncrementUsage_Should_Update_LastActivity()
        {
            // Arrange
            var connectionId = "test-connection-1";
            _connectionManager.RegisterConnection(connectionId, "key1", "model1", "provider1");
            var initialInfo = _connectionManager.GetConnectionInfo(connectionId);
            Assert.NotNull(initialInfo);
            var initialLastActivity = initialInfo.LastActivity;

            // Wait a bit to ensure time difference
            Thread.Sleep(10);

            // Act
            _connectionManager.IncrementUsage(connectionId, 100, 50, 1.0m);

            // Assert
            var updatedInfo = _connectionManager.GetConnectionInfo(connectionId);
            Assert.NotNull(updatedInfo);
            Assert.True(updatedInfo.LastActivity > initialLastActivity);
        }
    }
}
