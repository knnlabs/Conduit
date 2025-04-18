using System;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class RequestLogServiceTests
    {
        private DbContextOptions<ConfigurationDbContext> GetDbOptions()
        {
            return new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }
        
        private ConfigurationDbContext CreateTestContext(DbContextOptions<ConfigurationDbContext> options)
        {
            var context = new ConfigurationDbContext(options);
            context.IsTestEnvironment = true;
            return context;
        }

        [Fact]
        public async Task LogRequest_WithValidData_CreatesLogEntry()
        {
            // Arrange
            var options = GetDbOptions();
            var keyUuid = Guid.NewGuid().ToString();
            var currentTime = DateTime.UtcNow;
            
            // Add a virtual key first
            using (var context = CreateTestContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey
                {
                    Id = 1,
                    KeyName = "Test Key",
                    KeyHash = keyUuid,
                    IsEnabled = true
                });
                await context.SaveChangesAsync();
            }
            
            // Act
            using (var context = CreateTestContext(options))
            {
                var logService = new RequestLogService(context);
                
                await logService.LogRequestAsync(new LogRequestDto
                {
                    VirtualKeyId = 1,
                    ModelName = "gpt-4",
                    RequestType = "chat",
                    InputTokens = 10,
                    OutputTokens = 5,
                    Cost = 0.0003m,
                    ResponseTimeMs = 500,
                    UserId = "test-user",
                    ClientIp = "127.0.0.1",
                    RequestPath = "/api/chat",
                    StatusCode = 200
                });
            }
            
            // Assert
            using (var context = CreateTestContext(options))
            {
                var log = context.RequestLogs.Include(l => l.VirtualKey).FirstOrDefault();
                
                Assert.NotNull(log);
                Assert.Equal(1, log.VirtualKeyId);
                Assert.Equal(keyUuid, log.VirtualKey?.KeyHash);
                Assert.Equal("gpt-4", log.ModelName);
                Assert.Equal("chat", log.RequestType);
                Assert.Equal(10, log.InputTokens);
                Assert.Equal(5, log.OutputTokens);
                Assert.Equal(15, log.InputTokens + log.OutputTokens);
                Assert.Equal(0.0003m, log.Cost);
            }
        }
        
        [Fact]
        public async Task GetUsageStatistics_ReturnsCorrectAggregatedData()
        {
            // Arrange
            var options = GetDbOptions();
            
            // Setup test data
            using (var context = CreateTestContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey { Id = 1, KeyName = "Test Key", KeyHash = "vk_test123", IsEnabled = true });
                await context.SaveChangesAsync();
                
                var now = DateTime.UtcNow;
                context.RequestLogs.AddRange(
                    new RequestLog { VirtualKeyId = 1, ModelName = "gpt-4", RequestType = "chat", InputTokens = 100, OutputTokens = 50, Cost = 0.006m, ResponseTimeMs = 1000, Timestamp = now.AddDays(-1) },
                    new RequestLog { VirtualKeyId = 1, ModelName = "gpt-4", RequestType = "chat", InputTokens = 200, OutputTokens = 100, Cost = 0.012m, ResponseTimeMs = 1500, Timestamp = now.AddHours(-12) },
                    new RequestLog { VirtualKeyId = 1, ModelName = "gpt-3.5-turbo", RequestType = "chat", InputTokens = 300, OutputTokens = 150, Cost = 0.003m, ResponseTimeMs = 800, Timestamp = now.AddHours(-2) }
                );
                
                await context.SaveChangesAsync();
            }
            
            // Act
            using (var context = CreateTestContext(options))
            {
                var service = new RequestLogService(context);
                var stats = await service.GetUsageStatisticsAsync(1, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
                
                // Assert
                Assert.Equal(3, stats.TotalRequests);
                Assert.Equal(0.021m, stats.TotalCost);
                Assert.Equal(600, stats.TotalInputTokens);
                Assert.Equal(300, stats.TotalOutputTokens);
                Assert.Equal(900, stats.TotalTokens); // Input + Output
                Assert.Equal(1100, stats.AverageResponseTimeMs); // (1000 + 1500 + 800) / 3
                
                // Check model breakdown
                Assert.Equal(2, stats.ModelUsage["gpt-4"].RequestCount);
                Assert.Equal(0.018m, stats.ModelUsage["gpt-4"].Cost);
                Assert.Equal(1, stats.ModelUsage["gpt-3.5-turbo"].RequestCount);
                Assert.Equal(0.003m, stats.ModelUsage["gpt-3.5-turbo"].Cost);
            }
        }
    }
}
