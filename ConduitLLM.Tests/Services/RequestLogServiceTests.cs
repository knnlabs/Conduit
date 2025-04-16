using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class RequestLogServiceTests
    {
        private DbContextOptions<VirtualKeyDbContext> GetDbOptions()
        {
            return new DbContextOptionsBuilder<VirtualKeyDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task LogRequest_WithValidData_CreatesLogEntry()
        {
            // Arrange
            var options = GetDbOptions();
            
            // Create a virtual key first
            using (var context = new VirtualKeyDbContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey 
                { 
                    Id = 1, 
                    KeyName = "Test Key", 
                    KeyHash = "vk_test123", 
                    IsEnabled = true,
                    MaxBudget = 10.0m,
                    CurrentSpend = 0.0m,
                    BudgetDuration = "monthly"
                });
                await context.SaveChangesAsync();
            }
            
            // Test the request logging
            using (var context = new VirtualKeyDbContext(options))
            {
                var service = new RequestLogService(context);
                var logRequest = new LogRequestDto
                {
                    VirtualKeyId = 1,
                    ModelName = "gpt-4-turbo",
                    RequestType = "chat",
                    InputTokens = 150,
                    OutputTokens = 75,
                    Cost = 0.0035m,
                    ResponseTimeMs = 850,
                    UserId = "test-user",
                    ClientIp = "127.0.0.1",
                    RequestPath = "/api/chat",
                    StatusCode = 200
                };
                
                // Act
                await service.LogRequestAsync(logRequest);
            }
            
            // Assert
            using (var context = new VirtualKeyDbContext(options))
            {
                // Use Include to explicitly load the navigation property
                var log = await context.RequestLogs
                    .Include(r => r.VirtualKey)
                    .FirstOrDefaultAsync();
                
                Assert.NotNull(log);
                Assert.Equal(1, log.VirtualKeyId);
                Assert.Equal("gpt-4-turbo", log.ModelName);
                Assert.Equal("chat", log.RequestType);
                Assert.Equal(150, log.InputTokens);
                Assert.Equal(75, log.OutputTokens);
                Assert.Equal(0.0035m, log.Cost);
                Assert.InRange(log.ResponseTimeMs, 849, 851);
                Assert.Equal("test-user", log.UserId);
                Assert.Equal("127.0.0.1", log.ClientIp);
                Assert.Equal("/api/chat", log.RequestPath);
                Assert.Equal(200, log.StatusCode);
                
                // Verify relationship navigation works
                Assert.NotNull(log.VirtualKey);
                Assert.Equal("Test Key", log.VirtualKey.KeyName);
            }
        }
        
        [Fact]
        public async Task GetUsageStatistics_ReturnsCorrectAggregatedData()
        {
            // Arrange
            var options = GetDbOptions();
            
            // Setup test data
            using (var context = new VirtualKeyDbContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey { Id = 1, KeyName = "Test Key", KeyHash = "vk_test123", IsEnabled = true });
                await context.SaveChangesAsync();
                
                var now = DateTime.UtcNow;
                context.RequestLogs.AddRange(
                    new RequestLog { VirtualKeyId = 1, ModelName = "gpt-4", RequestType = "chat", InputTokens = 100, OutputTokens = 50, Cost = 0.002m, ResponseTimeMs = 1000, Timestamp = now.AddDays(-1) },
                    new RequestLog { VirtualKeyId = 1, ModelName = "gpt-4", RequestType = "chat", InputTokens = 200, OutputTokens = 100, Cost = 0.004m, ResponseTimeMs = 1500, Timestamp = now.AddHours(-12) },
                    new RequestLog { VirtualKeyId = 1, ModelName = "gpt-3.5-turbo", RequestType = "chat", InputTokens = 300, OutputTokens = 150, Cost = 0.001m, ResponseTimeMs = 800, Timestamp = now.AddHours(-2) }
                );
                
                await context.SaveChangesAsync();
            }
            
            // Act
            using (var context = new VirtualKeyDbContext(options))
            {
                var service = new RequestLogService(context);
                var stats = await service.GetUsageStatisticsAsync(1, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
                
                // Assert
                Assert.Equal(3, stats.TotalRequests);
                Assert.Equal(0.007m, stats.TotalCost);
                Assert.Equal(600, stats.TotalInputTokens);
                Assert.Equal(300, stats.TotalOutputTokens);
                Assert.Equal(900, stats.TotalTokens); // Input + Output
                Assert.Equal(1100, stats.AverageResponseTimeMs); // (1000 + 1500 + 800) / 3
                
                // Check model breakdown
                Assert.Equal(2, stats.ModelUsage.Count);
                Assert.Equal(2, stats.ModelUsage["gpt-4"].RequestCount);
                Assert.Equal(0.006m, stats.ModelUsage["gpt-4"].Cost);
                Assert.Equal(1, stats.ModelUsage["gpt-3.5-turbo"].RequestCount);
                Assert.Equal(0.001m, stats.ModelUsage["gpt-3.5-turbo"].Cost);
            }
        }
    }
}
