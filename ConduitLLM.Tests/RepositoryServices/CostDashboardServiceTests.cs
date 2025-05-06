using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Tests.TestHelpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class CostDashboardServiceTests
    {
        private readonly ILogger<CostDashboardService> _logger;

        public CostDashboardServiceTests()
        {
            _logger = NullLogger<CostDashboardService>.Instance;
        }

        [Fact]
        public async Task GetDashboardDataAsync_ShouldReturnDashboardData_WithCorrectDateRange()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            // Create test data with virtual keys first, then reference them in logs
            var virtualKeys = new List<VirtualKey>
            {
                new VirtualKey { Id = 101, KeyName = "Test Key 1" },
                new VirtualKey { Id = 102, KeyName = "Test Key 2" }
            };
            
            var logs = new List<RequestLog>
            {
                new RequestLog
                {
                    Id = 1,
                    VirtualKeyId = 101,
                    ModelName = "gpt-4",
                    InputTokens = 100,
                    OutputTokens = 50,
                    Cost = 0.01m,
                    Timestamp = new DateTime(2025, 1, 5, 12, 0, 0, DateTimeKind.Utc)
                },
                new RequestLog
                {
                    Id = 2,
                    VirtualKeyId = 102,
                    ModelName = "claude-v1",
                    InputTokens = 200,
                    OutputTokens = 100,
                    Cost = 0.02m,
                    Timestamp = new DateTime(2025, 1, 10, 14, 0, 0, DateTimeKind.Utc)
                },
                new RequestLog
                {
                    Id = 3,
                    VirtualKeyId = 101,
                    ModelName = "gpt-4",
                    InputTokens = 150,
                    OutputTokens = 75,
                    Cost = 0.015m,
                    Timestamp = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc)
                }
            };

            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    virtualKeys: virtualKeys,
                    requestLogs: logs);
            }
            
            // Create service with real factory
            var service = new CostDashboardService(factory, _logger);

            // Act
            var result = await service.GetDashboardDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(startDate, result.StartDate);
            Assert.Equal(endDate, result.EndDate);
            Assert.Equal(3, result.TotalRequests);
            Assert.Equal(0.045m, result.TotalCost);
            Assert.Equal(450, result.TotalInputTokens);
            Assert.Equal(225, result.TotalOutputTokens);

            // Check cost trends - should have entries for each day in range
            Assert.Equal(31, result.CostTrends.Count); // 31 days in January
            
            // Verify days with data
            var jan5Data = result.CostTrends.FirstOrDefault(d => d.Date.Date == new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc).Date);
            Assert.NotNull(jan5Data);
            Assert.Equal(0.01m, jan5Data.Cost);
            Assert.Equal(1, jan5Data.Requests);

            var jan10Data = result.CostTrends.FirstOrDefault(d => d.Date.Date == new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc).Date);
            Assert.NotNull(jan10Data);
            Assert.Equal(0.02m, jan10Data.Cost);
            Assert.Equal(1, jan10Data.Requests);

            // Verify days with zero data have been filled in
            var jan2Data = result.CostTrends.FirstOrDefault(d => d.Date.Date == new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc).Date);
            Assert.NotNull(jan2Data);
            Assert.Equal(0m, jan2Data.Cost);
            Assert.Equal(0, jan2Data.Requests);

            // Check model data
            Assert.Equal(2, result.CostByModel.Count);
            var gpt4Data = result.CostByModel.FirstOrDefault(m => m.Model == "gpt-4");
            Assert.NotNull(gpt4Data);
            Assert.Equal(0.025m, gpt4Data.Cost);
            Assert.Equal(2, gpt4Data.Requests);

            // Check virtual key data
            Assert.Equal(2, result.CostByVirtualKey.Count);
            var key1Data = result.CostByVirtualKey.FirstOrDefault(k => k.KeyId == 101);
            Assert.NotNull(key1Data);
            Assert.Equal("Test Key 1", key1Data.KeyName);
            Assert.Equal(0.025m, key1Data.Cost);
            Assert.Equal(2, key1Data.Requests);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ShouldFilterByVirtualKeyId()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            int virtualKeyId = 101;

            // Create test data with virtual keys first
            var virtualKeys = new List<VirtualKey>
            {
                new VirtualKey { Id = 101, KeyName = "Test Key 1" },
                new VirtualKey { Id = 102, KeyName = "Test Key 2" }
            };
            
            var logs = new List<RequestLog>
            {
                new RequestLog
                {
                    Id = 1,
                    VirtualKeyId = 101,
                    ModelName = "gpt-4",
                    InputTokens = 100,
                    OutputTokens = 50,
                    Cost = 0.01m,
                    Timestamp = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc)
                },
                new RequestLog
                {
                    Id = 2,
                    VirtualKeyId = 102,
                    ModelName = "claude-v1",
                    InputTokens = 200,
                    OutputTokens = 100,
                    Cost = 0.02m,
                    Timestamp = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc)
                },
                new RequestLog
                {
                    Id = 3,
                    VirtualKeyId = 101,
                    ModelName = "gpt-4",
                    InputTokens = 150,
                    OutputTokens = 75,
                    Cost = 0.015m,
                    Timestamp = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc)
                }
            };

            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    virtualKeys: virtualKeys,
                    requestLogs: logs);
            }
            
            // Create service with real factory
            var service = new CostDashboardService(factory, _logger);

            // Act
            var result = await service.GetDashboardDataAsync(startDate, endDate, virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalRequests); // Only logs with virtualKeyId = 101
            Assert.Equal(0.025m, result.TotalCost);
            Assert.Equal(250, result.TotalInputTokens);
            Assert.Equal(125, result.TotalOutputTokens);

            // Check cost by model
            Assert.Single(result.CostByModel);
            Assert.Equal("gpt-4", result.CostByModel[0].Model);
            Assert.Equal(2, result.CostByModel[0].Requests);

            // Check virtual key data
            Assert.Single(result.CostByVirtualKey);
            Assert.Equal(101, result.CostByVirtualKey[0].KeyId);
            Assert.Equal("Test Key 1", result.CostByVirtualKey[0].KeyName);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ShouldFilterByModelName()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            string modelName = "gpt-4";

            // Create test data with virtual keys first
            var virtualKeys = new List<VirtualKey>
            {
                new VirtualKey { Id = 101, KeyName = "Test Key 1" },
                new VirtualKey { Id = 102, KeyName = "Test Key 2" }
            };
            
            var logs = new List<RequestLog>
            {
                new RequestLog
                {
                    Id = 1,
                    VirtualKeyId = 101,
                    ModelName = "gpt-4",
                    InputTokens = 100,
                    OutputTokens = 50,
                    Cost = 0.01m,
                    Timestamp = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc)
                },
                new RequestLog
                {
                    Id = 2,
                    VirtualKeyId = 102,
                    ModelName = "claude-v1",
                    InputTokens = 200,
                    OutputTokens = 100,
                    Cost = 0.02m,
                    Timestamp = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc)
                },
                new RequestLog
                {
                    Id = 3,
                    VirtualKeyId = 101,
                    ModelName = "gpt-4",
                    InputTokens = 150,
                    OutputTokens = 75,
                    Cost = 0.015m,
                    Timestamp = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc)
                }
            };

            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    virtualKeys: virtualKeys,
                    requestLogs: logs);
            }
            
            // Create service with real factory
            var service = new CostDashboardService(factory, _logger);

            // Act
            var result = await service.GetDashboardDataAsync(startDate, endDate, null, modelName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalRequests); // Only logs with modelName = "gpt-4"
            Assert.Equal(0.025m, result.TotalCost);
            Assert.Equal(250, result.TotalInputTokens);
            Assert.Equal(125, result.TotalOutputTokens);

            // Check cost by model
            Assert.Single(result.CostByModel);
            Assert.Equal("gpt-4", result.CostByModel[0].Model);
            Assert.Equal(2, result.CostByModel[0].Requests);

            // Check virtual key data
            Assert.Single(result.CostByVirtualKey);
            Assert.Equal(101, result.CostByVirtualKey[0].KeyId);
            Assert.Equal("Test Key 1", result.CostByVirtualKey[0].KeyName);
        }

        [Fact]
        public async Task GetAvailableModelsAsync_ShouldReturnDistinctModels()
        {
            // Arrange
            var logs = new List<RequestLog>
            {
                new RequestLog { ModelName = "gpt-4" },
                new RequestLog { ModelName = "claude-v1" },
                new RequestLog { ModelName = "gpt-4" },
                new RequestLog { ModelName = "gemini-pro" }
            };

            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, requestLogs: logs);
            }
            
            // Create service with real factory
            var service = new CostDashboardService(factory, _logger);

            // Act
            var result = await service.GetAvailableModelsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains("gpt-4", result);
            Assert.Contains("claude-v1", result);
            Assert.Contains("gemini-pro", result);
        }

        [Fact]
        public async Task GetDetailedCostDataAsync_ShouldReturnGroupedDetailedData()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            // Create test data with virtual keys first
            var virtualKeys = new List<VirtualKey>
            {
                new VirtualKey { Id = 101, KeyName = "Test Key 1" },
                new VirtualKey { Id = 102, KeyName = "Test Key 2" }
            };
            
            var logs = new List<RequestLog>
            {
                new RequestLog
                {
                    Id = 1,
                    VirtualKeyId = 101,
                    ModelName = "gpt-4",
                    InputTokens = 100,
                    OutputTokens = 50,
                    Cost = 0.01m,
                    Timestamp = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc)
                },
                new RequestLog
                {
                    Id = 2,
                    VirtualKeyId = 102,
                    ModelName = "claude-v1",
                    InputTokens = 200,
                    OutputTokens = 100,
                    Cost = 0.02m,
                    Timestamp = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc)
                },
                new RequestLog
                {
                    Id = 3,
                    VirtualKeyId = 101,
                    ModelName = "gpt-4",
                    InputTokens = 150,
                    OutputTokens = 75,
                    Cost = 0.015m,
                    Timestamp = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc)
                }
            };

            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    virtualKeys: virtualKeys,
                    requestLogs: logs);
            }
            
            // Create service with real factory
            var service = new CostDashboardService(factory, _logger);

            // Act
            var result = await service.GetDetailedCostDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count); // 2 groups: (2025-01-05, gpt-4, 101) and (2025-01-05, claude-v1, 102)

            // Check gpt-4 group
            var gpt4Group = result.FirstOrDefault(d => d.Model == "gpt-4" && d.KeyName == "Test Key 1");
            Assert.NotNull(gpt4Group);
            Assert.Equal(new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc), gpt4Group.Date);
            Assert.Equal(2, gpt4Group.Requests);
            Assert.Equal(250, gpt4Group.InputTokens);
            Assert.Equal(125, gpt4Group.OutputTokens);
            Assert.Equal(0.025m, gpt4Group.Cost);

            // Check claude-v1 group
            var claudeGroup = result.FirstOrDefault(d => d.Model == "claude-v1" && d.KeyName == "Test Key 2");
            Assert.NotNull(claudeGroup);
            Assert.Equal(new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc), claudeGroup.Date);
            Assert.Equal(1, claudeGroup.Requests);
            Assert.Equal(200, claudeGroup.InputTokens);
            Assert.Equal(100, claudeGroup.OutputTokens);
            Assert.Equal(0.02m, claudeGroup.Cost);
        }
    }
}