using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;

using FluentAssertions;

using ConduitLLM.Configuration.Messaging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Admin.Services
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Service")]
    public class AdminIpFilterServiceTests
    {
        private readonly Mock<IIpFilterRepository> _mockIpFilterRepo;
        private readonly Mock<IGlobalSettingRepository> _mockGlobalSettingRepo;
        private readonly Mock<IOptionsMonitor<IpFilterOptions>> _mockOptions;
        private readonly Mock<ILogger<AdminIpFilterService>> _mockLogger;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly AdminIpFilterService _service;
        private readonly ITestOutputHelper _output;

        public AdminIpFilterServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockIpFilterRepo = new Mock<IIpFilterRepository>();
            _mockGlobalSettingRepo = new Mock<IGlobalSettingRepository>();
            _mockOptions = new Mock<IOptionsMonitor<IpFilterOptions>>();
            _mockLogger = new Mock<ILogger<AdminIpFilterService>>();
            _mockEventBus = new Mock<IEventBus>();

            _mockOptions.Setup(o => o.CurrentValue).Returns(new IpFilterOptions
            {
                Enabled = false,
                DefaultAllow = true,
                BypassForAdminUi = true,
                ExcludedEndpoints = new List<string> { "/api/v1/health" }
            });

            _service = new AdminIpFilterService(
                _mockIpFilterRepo.Object,
                _mockGlobalSettingRepo.Object,
                _mockOptions.Object,
                _mockLogger.Object,
                _mockEventBus.Object);
        }

        #region CreateFilterAsync Tests

        [Fact]
        public async Task CreateFilterAsync_ValidFilter_LogsInformationAndReturnsSuccess()
        {
            // Arrange
            var createDto = new CreateIpFilterDto
            {
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "10.0.0.1",
                Description = "Test block",
                IsEnabled = true
            };

            var createdEntity = new IpFilterEntity
            {
                Id = 1,
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "10.0.0.1",
                Description = "Test block",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockIpFilterRepo.Setup(r => r.AddAsync(It.IsAny<IpFilterEntity>(), default))
                .ReturnsAsync(createdEntity);

            // Act
            var (success, error, filter) = await _service.CreateFilterAsync(createDto);

            // Assert
            success.Should().BeTrue();
            error.Should().BeNull();
            filter.Should().NotBeNull();
            filter!.Id.Should().Be(1);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("IP filter created")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateFilterAsync_InvalidIp_ReturnsFailure()
        {
            // Arrange
            var createDto = new CreateIpFilterDto
            {
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "not-an-ip",
                IsEnabled = true
            };

            // Act
            var (success, error, filter) = await _service.CreateFilterAsync(createDto);

            // Assert
            success.Should().BeFalse();
            error.Should().Contain("Invalid IP address");
            filter.Should().BeNull();
        }

        [Fact]
        public async Task CreateFilterAsync_RepositoryThrows_LogsErrorAndReturnsFailure()
        {
            // Arrange
            var createDto = new CreateIpFilterDto
            {
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "10.0.0.1",
                IsEnabled = true
            };

            _mockIpFilterRepo.Setup(r => r.AddAsync(It.IsAny<IpFilterEntity>(), default))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var (success, error, filter) = await _service.CreateFilterAsync(createDto);

            // Assert
            success.Should().BeFalse();
            error.Should().Be("An unexpected error occurred");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error creating IP filter")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region UpdateFilterAsync Tests

        [Fact]
        public async Task UpdateFilterAsync_WithChanges_LogsInformationAndReturnsSuccess()
        {
            // Arrange
            var existingEntity = new IpFilterEntity
            {
                Id = 1,
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "10.0.0.1",
                Description = "Old desc",
                IsEnabled = true
            };

            _mockIpFilterRepo.Setup(r => r.GetByIdAsync(1, default))
                .ReturnsAsync(existingEntity);
            _mockIpFilterRepo.Setup(r => r.UpdateAsync(It.IsAny<IpFilterEntity>(), default))
                .ReturnsAsync(true);

            var updateDto = new UpdateIpFilterDto
            {
                Id = 1,
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "10.0.0.1",
                Description = "New desc",
                IsEnabled = false
            };

            // Act
            var (success, error) = await _service.UpdateFilterAsync(updateDto);

            // Assert
            success.Should().BeTrue();
            error.Should().BeNull();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("IP filter updated")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateFilterAsync_NoChanges_SkipsUpdateAndDoesNotLogInfo()
        {
            // Arrange
            var existingEntity = new IpFilterEntity
            {
                Id = 1,
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "10.0.0.1",
                Description = "Same desc",
                IsEnabled = true
            };

            _mockIpFilterRepo.Setup(r => r.GetByIdAsync(1, default))
                .ReturnsAsync(existingEntity);

            var updateDto = new UpdateIpFilterDto
            {
                Id = 1,
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "10.0.0.1",
                Description = "Same desc",
                IsEnabled = true
            };

            // Act
            var (success, error) = await _service.UpdateFilterAsync(updateDto);

            // Assert
            success.Should().BeTrue();
            _mockIpFilterRepo.Verify(r => r.UpdateAsync(It.IsAny<IpFilterEntity>(), default), Times.Never);
        }

        [Fact]
        public async Task UpdateFilterAsync_NotFound_ReturnsFailure()
        {
            // Arrange
            _mockIpFilterRepo.Setup(r => r.GetByIdAsync(99, default))
                .ReturnsAsync((IpFilterEntity)null!);

            var updateDto = new UpdateIpFilterDto
            {
                Id = 99,
                IpAddressOrCidr = "10.0.0.1"
            };

            // Act
            var (success, error) = await _service.UpdateFilterAsync(updateDto);

            // Assert
            success.Should().BeFalse();
            error.Should().Contain("not found");
        }

        [Fact]
        public async Task UpdateFilterAsync_RepositoryFails_LogsWarningAndReturnsFailure()
        {
            // Arrange
            var existingEntity = new IpFilterEntity
            {
                Id = 1,
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "10.0.0.1",
                IsEnabled = true
            };

            _mockIpFilterRepo.Setup(r => r.GetByIdAsync(1, default))
                .ReturnsAsync(existingEntity);
            _mockIpFilterRepo.Setup(r => r.UpdateAsync(It.IsAny<IpFilterEntity>(), default))
                .ReturnsAsync(false);

            var updateDto = new UpdateIpFilterDto
            {
                Id = 1,
                FilterType = IpFilterConstants.WHITELIST, // Changed
                IpAddressOrCidr = "10.0.0.1",
                IsEnabled = true
            };

            // Act
            var (success, error) = await _service.UpdateFilterAsync(updateDto);

            // Assert
            success.Should().BeFalse();
            error.Should().Contain("Failed to update");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to update IP filter")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region DeleteFilterAsync Tests

        [Fact]
        public async Task DeleteFilterAsync_ExistingFilter_LogsInformationAndReturnsSuccess()
        {
            // Arrange
            var existingEntity = new IpFilterEntity
            {
                Id = 1,
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "10.0.0.1"
            };

            _mockIpFilterRepo.Setup(r => r.GetByIdAsync(1, default))
                .ReturnsAsync(existingEntity);
            _mockIpFilterRepo.Setup(r => r.DeleteAsync(1, default))
                .ReturnsAsync(true);

            // Act
            var (success, error) = await _service.DeleteFilterAsync(1);

            // Assert
            success.Should().BeTrue();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("IP filter deleted")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteFilterAsync_NotFound_ReturnsFailure()
        {
            // Arrange
            _mockIpFilterRepo.Setup(r => r.GetByIdAsync(99, default))
                .ReturnsAsync((IpFilterEntity)null!);

            // Act
            var (success, error) = await _service.DeleteFilterAsync(99);

            // Assert
            success.Should().BeFalse();
            error.Should().Contain("not found");
        }

        [Fact]
        public async Task DeleteFilterAsync_RepositoryFails_LogsWarningAndReturnsFailure()
        {
            // Arrange
            var existingEntity = new IpFilterEntity
            {
                Id = 1,
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "10.0.0.1"
            };

            _mockIpFilterRepo.Setup(r => r.GetByIdAsync(1, default))
                .ReturnsAsync(existingEntity);
            _mockIpFilterRepo.Setup(r => r.DeleteAsync(1, default))
                .ReturnsAsync(false);

            // Act
            var (success, error) = await _service.DeleteFilterAsync(1);

            // Assert
            success.Should().BeFalse();
            error.Should().Contain("Failed to delete");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to delete IP filter")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region UpdateIpFilterSettingsAsync Tests

        [Fact]
        public async Task UpdateIpFilterSettingsAsync_ValidSettings_LogsInformationAndReturnsSuccess()
        {
            // Arrange
            var settings = new IpFilterSettingsDto
            {
                IsEnabled = true,
                DefaultAllow = false,
                BypassForAdminUi = true,
                ExcludedEndpoints = new List<string> { "/health" }
            };

            // Act
            var (success, error) = await _service.UpdateIpFilterSettingsAsync(settings);

            // Assert
            success.Should().BeTrue();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("IP filter settings updated successfully")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            // Verify all settings were persisted
            _mockGlobalSettingRepo.Verify(r => r.UpsertAsync(
                "IpFilter:Enabled", "True", It.IsAny<string>()), Times.Once);
            _mockGlobalSettingRepo.Verify(r => r.UpsertAsync(
                "IpFilter:DefaultAllow", "False", It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region GetIpFilterSettingsAsync Tests

        [Fact]
        public async Task GetIpFilterSettingsAsync_NoDbSettings_FallsBackToOptions()
        {
            // Arrange - all GetByKeyAsync return null (no DB settings)
            _mockGlobalSettingRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), default))
                .ReturnsAsync((GlobalSetting)null!);

            // Act
            var settings = await _service.GetIpFilterSettingsAsync();

            // Assert
            settings.IsEnabled.Should().BeFalse(); // From options default
            settings.DefaultAllow.Should().BeTrue();
        }

        [Fact]
        public async Task GetIpFilterSettingsAsync_RepositoryThrows_ReturnsDefaults()
        {
            // Arrange
            _mockGlobalSettingRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), default))
                .ThrowsAsync(new Exception("DB error"));

            // Act
            var settings = await _service.GetIpFilterSettingsAsync();

            // Assert
            settings.IsEnabled.Should().BeFalse();
            settings.DefaultAllow.Should().BeTrue();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error getting IP filter settings")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion
    }
}
