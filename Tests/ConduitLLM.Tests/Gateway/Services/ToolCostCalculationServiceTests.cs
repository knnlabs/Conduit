using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Tests.TestInfrastructure;

namespace ConduitLLM.Tests.Http.Services
{
    public class ToolCostCalculationServiceTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly Mock<ILogger<ToolCostCalculationService>> _loggerMock;
        private readonly ToolCostCalculationService _service;
        private readonly DbContextOptions<ConduitDbContext> _options;
        private readonly SqliteTestDatabase _database;

        public ToolCostCalculationServiceTests()
        {
            _database = new SqliteTestDatabase();
            _options = _database.Options;
            _context = _database.CreateContext();
            _loggerMock = new Mock<ILogger<ToolCostCalculationService>>();
            var factoryMock = new Mock<IDbContextFactory<ConduitDbContext>>();
            factoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _database.CreateContext());
            _service = new ToolCostCalculationService(factoryMock.Object, _loggerMock.Object);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _database.Dispose();
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithValidToolConfig_ReturnsCorrectCost()
        {
            // Arrange
            var providerType = ProviderType.Groq;
            var toolName = "code_interpreter";
            var costPerUnit = 0.03m;
            var toolCount = 5;

            _context.ProviderTools.Add(new ProviderTool
            {
                Provider = providerType,
                ToolName = toolName,
                CostPerUnit = costPerUnit,
                BillingUnit = "requests",
                IsActive = true
            });
            await _context.SaveChangesAsync();

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = toolName, Count = toolCount }
                }
            };

            // Act
            var result = await _service.CalculateToolCostsAsync(toolUsage, providerType);

            // Assert
            Assert.False(result.Failed);
            Assert.False(result.HasUnconfiguredTools);
            Assert.Equal(toolCount * costPerUnit, result.TotalCost);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithMultipleTools_ReturnsCombinedCost()
        {
            // Arrange
            var providerType = ProviderType.Groq;

            _context.ProviderTools.AddRange(
                new ProviderTool
                {
                    Provider = providerType,
                    ToolName = "code_interpreter",
                    CostPerUnit = 0.03m,
                    BillingUnit = "requests",
                    IsActive = true
                },
                new ProviderTool
                {
                    Provider = providerType,
                    ToolName = "browser_search",
                    CostPerUnit = 0.05m,
                    BillingUnit = "requests",
                    IsActive = true
                }
            );
            await _context.SaveChangesAsync();

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = "code_interpreter", Count = 3 },
                    new ToolUsageItem { ToolName = "browser_search", Count = 2 }
                }
            };

            // Act
            var result = await _service.CalculateToolCostsAsync(toolUsage, providerType);

            // Assert
            Assert.Equal((3 * 0.03m) + (2 * 0.05m), result.TotalCost); // 0.09 + 0.10 = 0.19
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithMissingToolConfig_FailsClosed()
        {
            // Arrange
            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = "nonexistent_tool", Count = 5 }
                }
            };

            // Act
            var exception = await Assert.ThrowsAsync<ToolCostCalculationException>(() =>
                _service.CalculateToolCostsAsync(toolUsage, ProviderType.Groq));
            Assert.Contains("nonexistent_tool", exception.Message);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithMixedConfiguredAndUnconfigured_FailsClosed()
        {
            // Arrange
            var providerType = ProviderType.Groq;

            _context.ProviderTools.Add(new ProviderTool
            {
                Provider = providerType,
                ToolName = "code_interpreter",
                CostPerUnit = 0.03m,
                BillingUnit = "requests",
                IsActive = true
            });
            await _context.SaveChangesAsync();

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = "code_interpreter", Count = 2 },
                    new ToolUsageItem { ToolName = "unknown_tool", Count = 3 }
                }
            };

            // Act
            var exception = await Assert.ThrowsAsync<ToolCostCalculationException>(() =>
                _service.CalculateToolCostsAsync(toolUsage, providerType));
            Assert.Contains("unknown_tool", exception.Message);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithInactiveToolConfig_FailsClosed()
        {
            // Arrange
            var providerType = ProviderType.Groq;
            var toolName = "code_interpreter";

            _context.ProviderTools.Add(new ProviderTool
            {
                Provider = providerType,
                ToolName = toolName,
                CostPerUnit = 0.03m,
                BillingUnit = "requests",
                IsActive = false // Inactive tool
            });
            await _context.SaveChangesAsync();

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = toolName, Count = 5 }
                }
            };

            // Act
            await Assert.ThrowsAsync<ToolCostCalculationException>(() =>
                _service.CalculateToolCostsAsync(toolUsage, providerType));
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithHoursBillingUnit_UsesDuration()
        {
            // Arrange
            var providerType = ProviderType.Groq;
            var toolName = "long_running_task";
            var costPerHour = 1.50m;
            var durationHours = 2.5m;

            _context.ProviderTools.Add(new ProviderTool
            {
                Provider = providerType,
                ToolName = toolName,
                CostPerUnit = costPerHour,
                BillingUnit = "hours",
                IsActive = true
            });
            await _context.SaveChangesAsync();

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = toolName, Count = 1, Duration = durationHours }
                }
            };

            // Act
            var result = await _service.CalculateToolCostsAsync(toolUsage, providerType);

            // Assert
            Assert.Equal(durationHours * costPerHour, result.TotalCost);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithMinutesBillingUnit_UsesDuration()
        {
            // Arrange
            var providerType = ProviderType.Groq;
            var toolName = "processing_task";
            var costPerMinute = 0.10m;
            var durationMinutes = 15m;

            _context.ProviderTools.Add(new ProviderTool
            {
                Provider = providerType,
                ToolName = toolName,
                CostPerUnit = costPerMinute,
                BillingUnit = "minutes",
                IsActive = true
            });
            await _context.SaveChangesAsync();

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = toolName, Count = 1, Duration = durationMinutes }
                }
            };

            // Act
            var result = await _service.CalculateToolCostsAsync(toolUsage, providerType);

            // Assert
            Assert.Equal(durationMinutes * costPerMinute, result.TotalCost);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithDurationSeconds_ConvertsToHours()
        {
            // Arrange
            var providerType = ProviderType.Groq;
            var toolName = "code_interpreter";
            var costPerHour = 1.00m;
            var durationSeconds = 7200m; // 2 hours

            _context.ProviderTools.Add(new ProviderTool
            {
                Provider = providerType,
                ToolName = toolName,
                CostPerUnit = costPerHour,
                BillingUnit = "hours",
                IsActive = true
            });
            await _context.SaveChangesAsync();

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = toolName, Count = 1, DurationSeconds = durationSeconds }
                }
            };

            // Act
            var result = await _service.CalculateToolCostsAsync(toolUsage, providerType);

            // Assert - 7200 seconds = 2 hours × $1.00 = $2.00
            Assert.Equal(2.00m, result.TotalCost);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithDurationSeconds_ConvertsToMinutes()
        {
            // Arrange
            var providerType = ProviderType.Groq;
            var toolName = "code_interpreter";
            var costPerMinute = 0.10m;
            var durationSeconds = 300m; // 5 minutes

            _context.ProviderTools.Add(new ProviderTool
            {
                Provider = providerType,
                ToolName = toolName,
                CostPerUnit = costPerMinute,
                BillingUnit = "minutes",
                IsActive = true
            });
            await _context.SaveChangesAsync();

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = toolName, Count = 1, DurationSeconds = durationSeconds }
                }
            };

            // Act
            var result = await _service.CalculateToolCostsAsync(toolUsage, providerType);

            // Assert - 300 seconds = 5 minutes × $0.10 = $0.50
            Assert.Equal(0.50m, result.TotalCost);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_DurationSeconds_TakesPriorityOverDuration()
        {
            // Arrange
            var providerType = ProviderType.Groq;
            var toolName = "code_interpreter";

            _context.ProviderTools.Add(new ProviderTool
            {
                Provider = providerType,
                ToolName = toolName,
                CostPerUnit = 1.00m,
                BillingUnit = "hours",
                IsActive = true
            });
            await _context.SaveChangesAsync();

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem
                    {
                        ToolName = toolName,
                        Count = 1,
                        Duration = 99m, // Should be ignored
                        DurationSeconds = 3600m // 1 hour — should take priority
                    }
                }
            };

            // Act
            var result = await _service.CalculateToolCostsAsync(toolUsage, providerType);

            // Assert - DurationSeconds (3600s = 1hr) takes priority over Duration (99)
            Assert.Equal(1.00m, result.TotalCost);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithNullToolUsage_ReturnsZero()
        {
            // Act
            var result = await _service.CalculateToolCostsAsync(null!, ProviderType.Groq);

            // Assert
            Assert.Equal(0m, result.TotalCost);
            Assert.False(result.HasUnconfiguredTools);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithEmptyToolsList_ReturnsZero()
        {
            // Arrange
            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>()
            };

            // Act
            var result = await _service.CalculateToolCostsAsync(toolUsage, ProviderType.Groq);

            // Assert
            Assert.Equal(0m, result.TotalCost);
        }

        [Fact]
        public void SerializeToolUsage_WithValidData_ReturnsJsonString()
        {
            // Arrange
            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = "code_interpreter", Count = 3 },
                    new ToolUsageItem { ToolName = "browser_search", Count = 1 }
                }
            };

            // Act
            var result = _service.SerializeToolUsage(toolUsage);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("code_interpreter", result);
            Assert.Contains("browser_search", result);

            // Verify it's valid JSON
            var deserialized = JsonSerializer.Deserialize<ToolUsageData>(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Tools.Count);
        }

        [Fact]
        public void SerializeToolUsage_WithNullData_ReturnsEmptyJson()
        {
            // Act
            var result = _service.SerializeToolUsage(null!);

            // Assert
            Assert.Equal("{}", result);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_OnDbFailure_PropagatesFailure()
        {
            // Arrange
            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = "test_tool", Count = 1 }
                }
            };

            // Force an exception
            var failingFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            failingFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database unavailable"));

            var failingService = new ToolCostCalculationService(failingFactory.Object, _loggerMock.Object);

            // Act
            await Assert.ThrowsAsync<ToolCostCalculationException>(() =>
                failingService.CalculateToolCostsAsync(toolUsage, ProviderType.Groq));
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithNullCostPerUnit_FailsClosed()
        {
            // Arrange
            var providerType = ProviderType.Groq;
            var toolName = "free_tool";

            _context.ProviderTools.Add(new ProviderTool
            {
                Provider = providerType,
                ToolName = toolName,
                CostPerUnit = null, // No cost configured
                BillingUnit = "requests",
                IsActive = true
            });
            await _context.SaveChangesAsync();

            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = toolName, Count = 5 }
                }
            };

            // Act
            await Assert.ThrowsAsync<ToolCostCalculationException>(() =>
                _service.CalculateToolCostsAsync(toolUsage, providerType));
        }
    }
}
