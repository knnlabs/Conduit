using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Reliability and failure handling tests for distributed cache statistics
    /// </summary>
    public partial class DistributedCacheStatisticsIntegrationTests
    {
        [Fact]
        public async Task InstanceFailure_RemainingInstances_ContinueWorking()
        {
            // Arrange
            var instance1 = CreateCollector("instance-1");
            var instance2 = CreateCollector("instance-2");
            var instance3 = CreateCollector("instance-3");

            await instance1.RegisterInstanceAsync();
            await instance2.RegisterInstanceAsync();
            await instance3.RegisterInstanceAsync();

            var region = CacheRegion.VirtualKeys;

            // Act - Record some operations on all instances
            var operation = new CacheOperation
            {
                Region = region,
                OperationType = CacheOperationType.Hit,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            };

            await instance1.RecordOperationAsync(operation);
            await instance2.RecordOperationAsync(operation);
            await instance3.RecordOperationAsync(operation);

            // Simulate instance2 failure
            await instance2.UnregisterInstanceAsync();
            instance2.Dispose();

            // Continue operations on remaining instances
            await instance1.RecordOperationAsync(operation);
            await instance3.RecordOperationAsync(operation);

            // Assert
            var activeInstances = (await instance1.GetActiveInstancesAsync()).ToList();
            activeInstances.Should().HaveCount(2);
            activeInstances.Should().Contain("instance-1");
            activeInstances.Should().Contain("instance-3");
            activeInstances.Should().NotContain("instance-2");

            var stats = await instance1.GetAggregatedStatisticsAsync(region);
            stats.HitCount.Should().Be(5); // 3 initial + 2 after failure
        }

        [Fact]
        public async Task InstanceRegistration_Heartbeat_TracksActiveInstances()
        {
            // Arrange
            var instance1 = CreateCollector("instance-1");
            var instance2 = CreateCollector("instance-2");
            
            // Act - Register only instance1 and instance2
            await instance1.RegisterInstanceAsync();
            await instance2.RegisterInstanceAsync();
            
            // Manually add instance-3 to the set without registering (so no heartbeat timer)
            lock (_lockObj)
            {
                _instanceSet.Add("instance-3");
                // Set heartbeat times - instance-3 will have an expired heartbeat
                _redisStorage[$"conduit:cache:heartbeat:instance-1"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                _redisStorage[$"conduit:cache:heartbeat:instance-2"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds().ToString();
                _redisStorage[$"conduit:cache:heartbeat:instance-3"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds().ToString(); // Expired
            }

            // Assert
            var activeInstances = (await instance1.GetActiveInstancesAsync()).ToList();
            activeInstances.Should().HaveCount(2);
            activeInstances.Should().Contain("instance-1");
            activeInstances.Should().Contain("instance-2");
            activeInstances.Should().NotContain("instance-3"); // Expired heartbeat
            
            // Clean up
            instance1.Dispose();
            instance2.Dispose();
        }

        [Fact]
        public async Task RedisConnectionFailure_GracefulDegradation()
        {
            // Arrange
            var instance = CreateCollector("instance-1");
            await instance.RegisterInstanceAsync();

            var region = CacheRegion.ProviderResponses;

            // Act - Simulate Redis failure during operation
            var callCount = 0;
            _mockDatabase.Setup(db => db.HashIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .Returns((RedisKey key, RedisValue field, long value, CommandFlags flags) =>
                {
                    callCount++;
                    if (callCount > 3)
                        throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed");
                    
                    return Task.FromResult(1L);
                });

            // Record multiple operations to trigger failure
            var exceptions = new List<Exception>();
            for (int i = 0; i < 5; i++)
            {
                var operation = new CacheOperation
                {
                    Region = region,
                    OperationType = CacheOperationType.Hit,
                    Success = true,
                    Duration = TimeSpan.FromMilliseconds(10),
                    DataSizeBytes = 100 // Add data size to trigger more HashIncrement calls
                };

                try
                {
                    await instance.RecordOperationAsync(operation);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            // Assert - Some operations should fail but not crash the system
            // Each operation with DataSizeBytes makes 4 HashIncrement calls (2 for Hit, 2 for DataBytes)
            // We make 5 operations, so we expect at least some calls before failure
            callCount.Should().BeGreaterThan(3);
            // RedisCacheStatisticsCollector handles errors gracefully, so no exceptions should bubble up
            exceptions.Should().BeEmpty("Redis failures should be handled gracefully without throwing exceptions");
        }
    }
}