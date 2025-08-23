using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MassTransit;
using Moq;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Models;
using ConduitLLM.Configuration.Services;

namespace ConduitLLM.Tests.Configuration.Services
{
    public class CacheConfigurationServiceTests : IDisposable
    {
        private readonly ConduitDbContext _dbContext;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<CacheConfigurationService>> _mockLogger;
        private readonly CacheConfigurationService _service;

        public CacheConfigurationServiceTests()
        {
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new ConduitDbContext(options);
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<CacheConfigurationService>>();

            _service = new CacheConfigurationService(
                _dbContext,
                _mockPublishEndpoint.Object,
                _mockConfiguration.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GetConfigurationAsync_ExistingConfiguration_ReturnsConfig()
        {
            // Arrange
            var entity = new CacheConfiguration
            {
                Region = CacheRegions.VirtualKeys,
                Enabled = true,
                DefaultTtlSeconds = 1800,
                Priority = 100,
                IsActive = true
            };
            _dbContext.CacheConfigurations.Add(entity);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.GetConfigurationAsync(CacheRegions.VirtualKeys);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(CacheRegions.VirtualKeys, result.Region);
            Assert.True(result.Enabled);
            Assert.Equal(TimeSpan.FromSeconds(1800), result.DefaultTTL);
            Assert.Equal(100, result.Priority);
        }

        [Fact]
        public async Task GetConfigurationAsync_NonExistentRegion_ReturnsNull()
        {
            // Act
            var result = await _service.GetConfigurationAsync(CacheRegions.ModelMetadata);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetConfigurationAsync_LoadsFromConfiguration_WhenNotInDatabase()
        {
            // Arrange - use ConfigurationBuilder to create a real configuration section
            var configData = new Dictionary<string, string>
            {
                [$"Cache:Regions:{CacheRegions.RateLimits}:Enabled"] = "true",
                [$"Cache:Regions:{CacheRegions.RateLimits}:Priority"] = "75",
                [$"Cache:Regions:{CacheRegions.RateLimits}:DefaultTtlSeconds"] = "900"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            _mockConfiguration.Setup(x => x.GetSection($"Cache:Regions:{CacheRegions.RateLimits}"))
                .Returns(configuration.GetSection($"Cache:Regions:{CacheRegions.RateLimits}"));

            // Act
            var result = await _service.GetConfigurationAsync(CacheRegions.RateLimits);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(CacheRegions.RateLimits, result.Region);
            Assert.True(result.Enabled);
            Assert.Equal(75, result.Priority);
            Assert.Equal(TimeSpan.FromSeconds(900), result.DefaultTTL);
        }

        [Fact]
        public async Task CreateConfigurationAsync_ValidConfig_CreatesSuccessfully()
        {
            // Arrange
            var config = new CacheRegionConfig
            {
                Region = CacheRegions.ModelCosts,
                Enabled = true,
                DefaultTTL = TimeSpan.FromMinutes(60),
                Priority = 50
            };

            // Act
            var result = await _service.CreateConfigurationAsync(
                CacheRegions.ModelCosts,
                config,
                "test-user");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(CacheRegions.ModelCosts, result.Region);
            Assert.True(result.Enabled);

            var savedEntity = await _dbContext.CacheConfigurations
                .FirstOrDefaultAsync(c => c.Region == CacheRegions.ModelCosts);
            Assert.NotNull(savedEntity);
            Assert.True(savedEntity.IsActive);
            Assert.Equal("test-user", savedEntity.CreatedBy);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<CacheConfigurationChangedEvent>(e => 
                    e.Region == CacheRegions.ModelCosts && 
                    e.Action == "Created"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateConfigurationAsync_ExistingActiveConfig_ThrowsException()
        {
            // Arrange
            var entity = new CacheConfiguration
            {
                Region = CacheRegions.AuthTokens,
                IsActive = true
            };
            _dbContext.CacheConfigurations.Add(entity);
            await _dbContext.SaveChangesAsync();

            var config = new CacheRegionConfig
            {
                Region = CacheRegions.AuthTokens,
                Enabled = true
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateConfigurationAsync(CacheRegions.AuthTokens, config, "test-user"));
        }

        [Fact]
        public async Task UpdateConfigurationAsync_ValidConfig_UpdatesSuccessfully()
        {
            // Arrange
            var entity = new CacheConfiguration
            {
                Region = CacheRegions.ProviderHealth,
                Enabled = true,
                DefaultTtlSeconds = 300,
                IsActive = true
            };
            _dbContext.CacheConfigurations.Add(entity);
            await _dbContext.SaveChangesAsync();

            var newConfig = new CacheRegionConfig
            {
                Region = CacheRegions.ProviderHealth,
                Enabled = false,
                DefaultTTL = TimeSpan.FromMinutes(10)
            };

            // Act
            var result = await _service.UpdateConfigurationAsync(
                CacheRegions.ProviderHealth,
                newConfig,
                "test-user",
                "Disabling cache for maintenance");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Enabled);
            Assert.Equal(TimeSpan.FromMinutes(10), result.DefaultTTL);

            var audit = await _dbContext.CacheConfigurationAudits
                .FirstOrDefaultAsync(a => a.Region == CacheRegions.ProviderHealth);
            Assert.NotNull(audit);
            Assert.Equal("Updated", audit.Action);
            Assert.Equal("test-user", audit.ChangedBy);
            Assert.Equal("Disabling cache for maintenance", audit.Reason);
            Assert.True(audit.Success);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<CacheConfigurationChangedEvent>(e => 
                    e.Region == CacheRegions.ProviderHealth && 
                    e.Action == "Updated"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteConfigurationAsync_ExistingConfig_SoftDeletesSuccessfully()
        {
            // Arrange
            var entity = new CacheConfiguration
            {
                Region = CacheRegions.IpFilters,
                IsActive = true
            };
            _dbContext.CacheConfigurations.Add(entity);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.DeleteConfigurationAsync(
                CacheRegions.IpFilters,
                "test-user",
                "No longer needed");

            // Assert
            Assert.True(result);

            var deletedEntity = await _dbContext.CacheConfigurations
                .FirstOrDefaultAsync(c => c.Region == CacheRegions.IpFilters);
            Assert.NotNull(deletedEntity);
            Assert.False(deletedEntity.IsActive);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<CacheConfigurationChangedEvent>(e => 
                    e.Region == CacheRegions.IpFilters && 
                    e.Action == "Deleted"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ValidateConfigurationAsync_ValidConfig_ReturnsValid()
        {
            // Arrange
            var config = new CacheRegionConfig
            {
                DefaultTTL = TimeSpan.FromMinutes(5),
                MaxTTL = TimeSpan.FromMinutes(30),
                MaxEntries = 1000,
                Priority = 50
            };

            // Act
            var result = await _service.ValidateConfigurationAsync(config);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ValidateConfigurationAsync_InvalidConfig_ReturnsErrors()
        {
            // Arrange
            var config = new CacheRegionConfig
            {
                DefaultTTL = TimeSpan.FromMinutes(-5),
                MaxTTL = TimeSpan.FromMinutes(10),
                MaxEntries = -100,
                Priority = 150
            };

            // Act
            var result = await _service.ValidateConfigurationAsync(config);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("DefaultTTL cannot be negative", result.Errors);
            Assert.Contains("MaxEntries must be greater than 0", result.Errors);
            Assert.Contains("Priority must be between 0 and 100", result.Errors);
        }

        [Fact]
        public async Task GetAuditHistoryAsync_ReturnsAuditEntries()
        {
            // Arrange
            var audits = new[]
            {
                new CacheConfigurationAudit
                {
                    Region = CacheRegions.GlobalSettings,
                    Action = "Created",
                    ChangedBy = "user1",
                    ChangedAt = DateTime.UtcNow.AddHours(-2)
                },
                new CacheConfigurationAudit
                {
                    Region = CacheRegions.GlobalSettings,
                    Action = "Updated",
                    ChangedBy = "user2",
                    ChangedAt = DateTime.UtcNow.AddHours(-1)
                }
            };
            _dbContext.CacheConfigurationAudits.AddRange(audits);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.GetAuditHistoryAsync(CacheRegions.GlobalSettings);

            // Assert
            var auditList = result.ToList();
            Assert.Equal(2, auditList.Count);
            Assert.Equal("Updated", auditList[0].Action); // Most recent first
            Assert.Equal("Created", auditList[1].Action);
        }

        [Fact]
        public async Task RollbackConfigurationAsync_ValidAudit_RollsBackSuccessfully()
        {
            // Arrange
            var oldConfig = new CacheRegionConfig
            {
                Region = CacheRegions.AsyncTasks,
                Enabled = true,
                DefaultTTL = TimeSpan.FromMinutes(15)
            };

            var audit = new CacheConfigurationAudit
            {
                Region = CacheRegions.AsyncTasks,
                Action = "Updated",
                OldConfigJson = System.Text.Json.JsonSerializer.Serialize(oldConfig),
                ChangedBy = "user1"
            };
            _dbContext.CacheConfigurationAudits.Add(audit);

            var currentEntity = new CacheConfiguration
            {
                Region = CacheRegions.AsyncTasks,
                Enabled = false,
                IsActive = true
            };
            _dbContext.CacheConfigurations.Add(currentEntity);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.RollbackConfigurationAsync(
                CacheRegions.AsyncTasks,
                audit.Id,
                "rollback-user");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Enabled);
            Assert.Equal(TimeSpan.FromMinutes(15), result.DefaultTTL);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<CacheConfigurationChangedEvent>(e => 
                    e.Region == CacheRegions.AsyncTasks && 
                    e.Action == "RolledBack" &&
                    e.IsRollback == true),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ApplyEnvironmentConfigurationsAsync_AppliesEnvironmentVariables()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_CACHE_EMBEDDINGS_ENABLED", "false");
            Environment.SetEnvironmentVariable("CONDUIT_CACHE_EMBEDDINGS_TTL", "7200");

            try
            {
                // Act
                await _service.ApplyEnvironmentConfigurationsAsync();

                // Assert
                var config = await _dbContext.CacheConfigurations
                    .FirstOrDefaultAsync(c => c.Region == CacheRegions.Embeddings);
                
                Assert.NotNull(config);
                Assert.False(config.Enabled);
                Assert.Equal(7200, config.DefaultTtlSeconds);
                Assert.Equal("System", config.CreatedBy);
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("CONDUIT_CACHE_EMBEDDINGS_ENABLED", null);
                Environment.SetEnvironmentVariable("CONDUIT_CACHE_EMBEDDINGS_TTL", null);
            }
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
        }
    }
}