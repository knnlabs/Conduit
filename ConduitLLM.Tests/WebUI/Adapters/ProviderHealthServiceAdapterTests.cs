using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.WebUI.Adapters
{
    public class ProviderHealthServiceAdapterTests
    {
        private readonly Mock<IAdminApiClient> _adminApiClientMock;
        private readonly Mock<ILogger<ProviderHealthServiceAdapter>> _loggerMock;
        private readonly ProviderHealthServiceAdapter _adapter;

        public ProviderHealthServiceAdapterTests()
        {
            _adminApiClientMock = new Mock<IAdminApiClient>();
            _loggerMock = new Mock<ILogger<ProviderHealthServiceAdapter>>();
            _adapter = new ProviderHealthServiceAdapter(_adminApiClientMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetAllConfigurationsAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedConfigs = new List<ConfigDTOs.ProviderHealthConfigurationDto>
            {
                new ConfigDTOs.ProviderHealthConfigurationDto { Id = 1, ProviderName = "Provider1" },
                new ConfigDTOs.ProviderHealthConfigurationDto { Id = 2, ProviderName = "Provider2" }
            };

            _adminApiClientMock.Setup(c => c.GetAllProviderHealthConfigurationsAsync())
                .ReturnsAsync(expectedConfigs);

            // Act
            var result = await _adapter.GetAllConfigurationsAsync();

            // Assert
            Assert.Same(expectedConfigs, result);
            _adminApiClientMock.Verify(c => c.GetAllProviderHealthConfigurationsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetConfigurationByProviderNameAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedConfig = new ConfigDTOs.ProviderHealthConfigurationDto { Id = 1, ProviderName = "TestProvider" };

            _adminApiClientMock.Setup(c => c.GetProviderHealthConfigurationByNameAsync("TestProvider"))
                .ReturnsAsync(expectedConfig);

            // Act
            var result = await _adapter.GetConfigurationByProviderNameAsync("TestProvider");

            // Assert
            Assert.Same(expectedConfig, result);
            _adminApiClientMock.Verify(c => c.GetProviderHealthConfigurationByNameAsync("TestProvider"), Times.Once);
        }

        [Fact]
        public async Task CreateConfigurationAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var createDto = new ConfigDTOs.CreateProviderHealthConfigurationDto { ProviderName = "NewProvider" };
            var expectedConfig = new ConfigDTOs.ProviderHealthConfigurationDto { Id = 1, ProviderName = "NewProvider" };

            _adminApiClientMock.Setup(c => c.CreateProviderHealthConfigurationAsync(createDto))
                .ReturnsAsync(expectedConfig);

            // Act
            var result = await _adapter.CreateConfigurationAsync(createDto);

            // Assert
            Assert.Same(expectedConfig, result);
            _adminApiClientMock.Verify(c => c.CreateProviderHealthConfigurationAsync(createDto), Times.Once);
        }

        [Fact]
        public async Task UpdateConfigurationAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var updateDto = new ConfigDTOs.UpdateProviderHealthConfigurationDto { MonitoringEnabled = false };
            var expectedConfig = new ConfigDTOs.ProviderHealthConfigurationDto { Id = 1, ProviderName = "TestProvider", MonitoringEnabled = false };

            _adminApiClientMock.Setup(c => c.UpdateProviderHealthConfigurationAsync("TestProvider", updateDto))
                .ReturnsAsync(expectedConfig);

            // Act
            var result = await _adapter.UpdateConfigurationAsync("TestProvider", updateDto);

            // Assert
            Assert.Same(expectedConfig, result);
            _adminApiClientMock.Verify(c => c.UpdateProviderHealthConfigurationAsync("TestProvider", updateDto), Times.Once);
        }

        [Fact]
        public async Task DeleteConfigurationAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.DeleteProviderHealthConfigurationAsync("TestProvider"))
                .ReturnsAsync(true);

            // Act
            var result = await _adapter.DeleteConfigurationAsync("TestProvider");

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.DeleteProviderHealthConfigurationAsync("TestProvider"), Times.Once);
        }

        [Fact]
        public async Task GetRecordsAsync_DelegatesToAdminApiClient_WithProviderName()
        {
            // Arrange
            var expectedRecords = new List<ConfigDTOs.ProviderHealthRecordDto>
            {
                new ConfigDTOs.ProviderHealthRecordDto { Id = 1, ProviderName = "TestProvider" }
            };

            _adminApiClientMock.Setup(c => c.GetProviderHealthRecordsAsync("TestProvider"))
                .ReturnsAsync(expectedRecords);

            // Act
            var result = await _adapter.GetRecordsAsync("TestProvider");

            // Assert
            Assert.Same(expectedRecords, result);
            _adminApiClientMock.Verify(c => c.GetProviderHealthRecordsAsync("TestProvider"), Times.Once);
        }

        [Fact]
        public async Task GetRecordsAsync_DelegatesToAdminApiClient_WithoutProviderName()
        {
            // Arrange
            var expectedRecords = new List<ConfigDTOs.ProviderHealthRecordDto>
            {
                new ConfigDTOs.ProviderHealthRecordDto { Id = 1, ProviderName = "Provider1" },
                new ConfigDTOs.ProviderHealthRecordDto { Id = 2, ProviderName = "Provider2" }
            };

            _adminApiClientMock.Setup(c => c.GetProviderHealthRecordsAsync(null))
                .ReturnsAsync(expectedRecords);

            // Act
            var result = await _adapter.GetRecordsAsync();

            // Assert
            Assert.Same(expectedRecords, result);
            _adminApiClientMock.Verify(c => c.GetProviderHealthRecordsAsync(null), Times.Once);
        }

        [Fact]
        public async Task GetHealthSummaryAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedSummaries = new List<ConfigDTOs.ProviderHealthSummaryDto>
            {
                new ConfigDTOs.ProviderHealthSummaryDto { ProviderName = "Provider1", Status = ConfigEntities.ProviderHealthRecord.StatusType.Online },
                new ConfigDTOs.ProviderHealthSummaryDto { ProviderName = "Provider2", Status = ConfigEntities.ProviderHealthRecord.StatusType.Offline }
            };

            _adminApiClientMock.Setup(c => c.GetProviderHealthSummaryAsync())
                .ReturnsAsync(expectedSummaries);

            // Act
            var result = await _adapter.GetHealthSummaryAsync();

            // Assert
            Assert.Same(expectedSummaries, result);
            _adminApiClientMock.Verify(c => c.GetProviderHealthSummaryAsync(), Times.Once);
        }
    }
}