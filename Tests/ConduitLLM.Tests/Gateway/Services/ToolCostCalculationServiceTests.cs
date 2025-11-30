using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Gateway.Services;

namespace ConduitLLM.Tests.Http.Services
{
    public class ToolCostCalculationServiceTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly Mock<ILogger<ToolCostCalculationService>> _loggerMock;
        private readonly ToolCostCalculationService _service;

        public ToolCostCalculationServiceTests()
        {
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            _context = new ConduitDbContext(options);
            _loggerMock = new Mock<ILogger<ToolCostCalculationService>>();
            _service = new ToolCostCalculationService(_context, _loggerMock.Object);
        }

        public void Dispose()
        {
            _context?.Dispose();
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
            Assert.Equal(toolCount * costPerUnit, result);
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
            Assert.Equal((3 * 0.03m) + (2 * 0.05m), result); // 0.09 + 0.10 = 0.19
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithMissingToolConfig_ReturnsZero()
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
            var result = await _service.CalculateToolCostsAsync(toolUsage, ProviderType.Groq);

            // Assert
            Assert.Equal(0m, result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No cost configuration found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithInactiveToolConfig_ReturnsZero()
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
            var result = await _service.CalculateToolCostsAsync(toolUsage, providerType);

            // Assert
            Assert.Equal(0m, result);
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
            Assert.Equal(durationHours * costPerHour, result);
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
            Assert.Equal(durationMinutes * costPerMinute, result);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithNullToolUsage_ReturnsZero()
        {
            // Act
            var result = await _service.CalculateToolCostsAsync(null!, ProviderType.Groq);

            // Assert
            Assert.Equal(0m, result);
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
            Assert.Equal(0m, result);
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
        public async Task CalculateToolCostsAsync_HandlesExceptionGracefully()
        {
            // Arrange
            var toolUsage = new ToolUsageData
            {
                Tools = new List<ToolUsageItem>
                {
                    new ToolUsageItem { ToolName = "test_tool", Count = 1 }
                }
            };

            // Force an exception by disposing the context
            _context.Dispose();

            // Act
            var result = await _service.CalculateToolCostsAsync(toolUsage, ProviderType.Groq);

            // Assert
            Assert.Equal(0m, result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to calculate tool costs")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CalculateToolCostsAsync_WithNullCostPerUnit_ReturnsZero()
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
            var result = await _service.CalculateToolCostsAsync(toolUsage, providerType);

            // Assert
            Assert.Equal(0m, result);
        }
    }
}