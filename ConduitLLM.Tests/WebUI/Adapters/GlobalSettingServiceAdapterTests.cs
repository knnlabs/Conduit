using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.WebUI.Adapters
{
    public class GlobalSettingServiceAdapterTests
    {
        private readonly Mock<IAdminApiClient> _adminApiClientMock;
        private readonly Mock<ILogger<GlobalSettingServiceAdapter>> _loggerMock;
        private readonly GlobalSettingServiceAdapter _adapter;

        public GlobalSettingServiceAdapterTests()
        {
            _adminApiClientMock = new Mock<IAdminApiClient>();
            _loggerMock = new Mock<ILogger<GlobalSettingServiceAdapter>>();
            _adapter = new GlobalSettingServiceAdapter(_adminApiClientMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetAllGlobalSettingsAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedSettings = new List<GlobalSettingDto>
            {
                new GlobalSettingDto { Key = "Setting1", Value = "Value1" },
                new GlobalSettingDto { Key = "Setting2", Value = "Value2" }
            };

            _adminApiClientMock.Setup(c => c.GetAllGlobalSettingsAsync())
                .ReturnsAsync(expectedSettings);

            // Act
            var result = await _adapter.GetAllGlobalSettingsAsync();

            // Assert
            Assert.Same(expectedSettings, result);
            _adminApiClientMock.Verify(c => c.GetAllGlobalSettingsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetGlobalSettingByKeyAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedSetting = new GlobalSettingDto { Key = "TestKey", Value = "TestValue" };

            _adminApiClientMock.Setup(c => c.GetGlobalSettingByKeyAsync("TestKey"))
                .ReturnsAsync(expectedSetting);

            // Act
            var result = await _adapter.GetGlobalSettingByKeyAsync("TestKey");

            // Assert
            Assert.Same(expectedSetting, result);
            _adminApiClientMock.Verify(c => c.GetGlobalSettingByKeyAsync("TestKey"), Times.Once);
        }

        [Fact]
        public async Task UpsertGlobalSettingAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var setting = new GlobalSettingDto { Key = "TestKey", Value = "TestValue" };
            var expectedResult = new GlobalSettingDto { Key = "TestKey", Value = "TestValue", Id = 1 };

            _adminApiClientMock.Setup(c => c.UpsertGlobalSettingAsync(setting))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _adapter.UpsertGlobalSettingAsync(setting);

            // Assert
            Assert.Same(expectedResult, result);
            _adminApiClientMock.Verify(c => c.UpsertGlobalSettingAsync(setting), Times.Once);
        }

        [Fact]
        public async Task DeleteGlobalSettingAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.DeleteGlobalSettingAsync("TestKey"))
                .ReturnsAsync(true);

            // Act
            var result = await _adapter.DeleteGlobalSettingAsync("TestKey");

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.DeleteGlobalSettingAsync("TestKey"), Times.Once);
        }

        [Fact]
        public async Task GetSettingValueAsync_ReturnsValueFromGlobalSetting()
        {
            // Arrange
            var setting = new GlobalSettingDto { Key = "TestKey", Value = "TestValue" };

            _adminApiClientMock.Setup(c => c.GetGlobalSettingByKeyAsync("TestKey"))
                .ReturnsAsync(setting);

            // Act
            var result = await _adapter.GetSettingValueAsync("TestKey");

            // Assert
            Assert.Equal("TestValue", result);
            _adminApiClientMock.Verify(c => c.GetGlobalSettingByKeyAsync("TestKey"), Times.Once);
        }

        [Fact]
        public async Task GetSettingValueAsync_ReturnsNull_WhenSettingNotFound()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetGlobalSettingByKeyAsync("NonExistentKey"))
                .ReturnsAsync((GlobalSettingDto)null);

            // Act
            var result = await _adapter.GetSettingValueAsync("NonExistentKey");

            // Assert
            Assert.Null(result);
            _adminApiClientMock.Verify(c => c.GetGlobalSettingByKeyAsync("NonExistentKey"), Times.Once);
        }

        [Fact]
        public async Task SetSettingValueAsync_CreatesNewSetting_WhenSettingDoesNotExist()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetGlobalSettingByKeyAsync("NewKey"))
                .ReturnsAsync((GlobalSettingDto)null);

            _adminApiClientMock.Setup(c => c.UpsertGlobalSettingAsync(It.Is<GlobalSettingDto>(s => 
                s.Key == "NewKey" && s.Value == "NewValue")))
                .ReturnsAsync(new GlobalSettingDto { Key = "NewKey", Value = "NewValue", Id = 1 });

            // Act
            var result = await _adapter.SetSettingValueAsync("NewKey", "NewValue");

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.GetGlobalSettingByKeyAsync("NewKey"), Times.Once);
            _adminApiClientMock.Verify(c => c.UpsertGlobalSettingAsync(It.Is<GlobalSettingDto>(s => 
                s.Key == "NewKey" && s.Value == "NewValue")), Times.Once);
        }

        [Fact]
        public async Task SetSettingValueAsync_UpdatesExistingSetting_WhenSettingExists()
        {
            // Arrange
            var existingSetting = new GlobalSettingDto { Key = "ExistingKey", Value = "OldValue", Id = 1 };

            _adminApiClientMock.Setup(c => c.GetGlobalSettingByKeyAsync("ExistingKey"))
                .ReturnsAsync(existingSetting);

            _adminApiClientMock.Setup(c => c.UpsertGlobalSettingAsync(It.Is<GlobalSettingDto>(s => 
                s.Key == "ExistingKey" && s.Value == "NewValue" && s.Id == 1)))
                .ReturnsAsync(new GlobalSettingDto { Key = "ExistingKey", Value = "NewValue", Id = 1 });

            // Act
            var result = await _adapter.SetSettingValueAsync("ExistingKey", "NewValue");

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.GetGlobalSettingByKeyAsync("ExistingKey"), Times.Once);
            _adminApiClientMock.Verify(c => c.UpsertGlobalSettingAsync(It.Is<GlobalSettingDto>(s => 
                s.Key == "ExistingKey" && s.Value == "NewValue" && s.Id == 1)), Times.Once);
        }

        [Fact]
        public async Task SetSettingValueAsync_ReturnsFalse_WhenUpsertFails()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetGlobalSettingByKeyAsync("TestKey"))
                .ReturnsAsync((GlobalSettingDto)null);

            _adminApiClientMock.Setup(c => c.UpsertGlobalSettingAsync(It.IsAny<GlobalSettingDto>()))
                .ReturnsAsync((GlobalSettingDto)null);

            // Act
            var result = await _adapter.SetSettingValueAsync("TestKey", "TestValue");

            // Assert
            Assert.False(result);
            _adminApiClientMock.Verify(c => c.GetGlobalSettingByKeyAsync("TestKey"), Times.Once);
            _adminApiClientMock.Verify(c => c.UpsertGlobalSettingAsync(It.IsAny<GlobalSettingDto>()), Times.Once);
        }
    }
}