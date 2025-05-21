using ConduitLLM.WebUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Services.Adapters;

// Remove the test extensions to avoid ambiguity
// using ConduitLLM.Tests.WebUI.Extensions;
// Only import the WebUI extensions
using ConduitLLM.WebUI.Extensions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using Xunit;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class GlobalSettingServiceTests
    {
        private readonly Mock<IAdminApiClient> _mockAdminApiClient;
        private readonly Mock<ILogger<GlobalSettingServiceAdapter>> _mockLogger;
        private readonly GlobalSettingServiceAdapter _service;

        public GlobalSettingServiceTests()
        {
            _mockAdminApiClient = new Mock<IAdminApiClient>();
            _mockLogger = new Mock<ILogger<GlobalSettingServiceAdapter>>();
            _service = new GlobalSettingServiceAdapter(_mockAdminApiClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetSettingAsync_ShouldReturnValue_WhenSettingExists()
        {
            // Arrange
            string key = "TestKey";
            string expectedValue = "TestValue";
            
            // Use explicit type to avoid ambiguity
            _mockAdminApiClient.Setup(api => api.GetGlobalSettingByKeyAsync(key))
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = key, Value = expectedValue }));
            
            // Act
            var result = await _service.GetSettingAsync(key);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public async Task GetSettingAsync_ShouldReturnNull_WhenSettingDoesNotExist()
        {
            // Arrange
            string key = "NonExistentKey";
            
            // Use explicit call to GetGlobalSettingByKeyAsync instead of extension method
            _mockAdminApiClient.Setup(api => api.GetGlobalSettingByKeyAsync(key))
                .Returns(Task.FromResult<GlobalSettingDto?>(null));
            
            // Act
            var result = await _service.GetSettingAsync(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetSettingAsync_ShouldCreateNewSetting_WhenKeyDoesNotExist()
        {
            // Arrange
            string key = "NewKey";
            string value = "NewValue";
            
            // Use UpsertGlobalSettingAsync with a return type of GlobalSettingDto
            _mockAdminApiClient.Setup(api => api.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == key && dto.Value == value)))
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = key, Value = value }));
            
            // Act
            await _service.SetSettingAsync(key, value);
            
            // Verify that the correct API method was called
            _mockAdminApiClient.Verify(api => api.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == key && dto.Value == value)), Times.Once);
        }
        
        [Fact]
        public async Task SetSettingAsync_ShouldUpdateExistingSetting_WhenKeyExists()
        {
            // Arrange
            string key = "ExistingKey";
            string newValue = "UpdatedValue";
            
            // Setup API to return existing setting then accept update
            _mockAdminApiClient.Setup(api => api.GetGlobalSettingByKeyAsync(key))
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = key, Value = "OriginalValue" }));
                
            _mockAdminApiClient.Setup(api => api.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == key && dto.Value == newValue)))
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = key, Value = newValue }));
            
            // Act
            await _service.SetSettingAsync(key, newValue);
            
            // Verify API was called with correct parameters
            _mockAdminApiClient.Verify(api => api.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == key && dto.Value == newValue)), Times.Once);
        }

        [Fact]
        public async Task GetMasterKeyHashAsync_ShouldReturnHash()
        {
            // Arrange
            string expectedHash = "abc123hash";
            
            _mockAdminApiClient.Setup(api => api.GetGlobalSettingByKeyAsync("MasterKeyHash"))
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = "MasterKeyHash", Value = expectedHash }));
            
            // Act
            var result = await _service.GetMasterKeyHashAsync();

            // Assert
            Assert.Equal(expectedHash, result);
        }

        [Fact]
        public async Task GetMasterKeyHashAlgorithmAsync_ShouldReturnAlgorithm()
        {
            // Arrange
            string expectedAlgorithm = "SHA512";
            
            _mockAdminApiClient.Setup(api => api.GetGlobalSettingByKeyAsync("MasterKeyHashAlgorithm"))
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = "MasterKeyHashAlgorithm", Value = expectedAlgorithm }));
            
            // Act
            var result = await _service.GetMasterKeyHashAlgorithmAsync();

            // Assert
            Assert.Equal(expectedAlgorithm, result);
        }

        [Fact]
        public async Task GetMasterKeyHashAlgorithmAsync_ShouldReturnDefaultSHA256_WhenNotSet()
        {
            // Arrange
            _mockAdminApiClient.Setup(api => api.GetGlobalSettingByKeyAsync("MasterKeyHashAlgorithm"))
                .Returns(Task.FromResult<GlobalSettingDto?>(null));
            
            // Act
            var result = await _service.GetMasterKeyHashAlgorithmAsync();

            // Assert
            Assert.Equal("SHA256", result);
        }

        [Fact]
        public async Task SetMasterKeyAsync_ShouldHashAndSaveKey()
        {
            // Arrange
            string masterKey = "SecureMasterKey123";
            
            _mockAdminApiClient.Setup(api => api.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == "MasterKeyHash" && !string.IsNullOrEmpty(dto.Value))))
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = "MasterKeyHash", Value = "hash-value" }));
                
            _mockAdminApiClient.Setup(api => api.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == "MasterKeyHashAlgorithm" && dto.Value == "SHA256")))
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = "MasterKeyHashAlgorithm", Value = "SHA256" }));
            
            // Act
            await _service.SetMasterKeyAsync(masterKey);

            // Assert - verify that both settings were saved via API
            _mockAdminApiClient.Verify(api => api.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == "MasterKeyHash" && !string.IsNullOrEmpty(dto.Value))), 
                Times.Once);
                
            _mockAdminApiClient.Verify(api => api.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == "MasterKeyHashAlgorithm" && dto.Value == "SHA256")), 
                Times.Once);
        }

        [Fact]
        public async Task SetMasterKeyAsync_ShouldThrowException_WhenKeyIsEmpty()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.SetMasterKeyAsync(""));
            await Assert.ThrowsAsync<ArgumentException>(() => _service.SetMasterKeyAsync(null!));
        }
        
        [Fact]
        public async Task SetMasterKeyAsync_UsesCorrectHashAlgorithm()
        {
            // Arrange
            string masterKey = "SecureMasterKey123";
            
            // Calculate expected hash with SHA256
            string expectedHash;
            using (var sha256 = SHA256.Create())
            {
                var keyBytes = Encoding.UTF8.GetBytes(masterKey);
                var hashBytes = sha256.ComputeHash(keyBytes);
                expectedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            
            // Capture the actual hash value sent to the API
            string capturedHashValue = null;
            
            _mockAdminApiClient.Setup(api => api.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == "MasterKeyHash")))
                .Callback<GlobalSettingDto>(dto => capturedHashValue = dto.Value)
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = "MasterKeyHash", Value = "hash-value" }));
                
            _mockAdminApiClient.Setup(api => api.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == "MasterKeyHashAlgorithm")))
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = "MasterKeyHashAlgorithm", Value = "SHA256" }));
            
            // Act
            await _service.SetMasterKeyAsync(masterKey);

            // Assert - Verify the hash matches what we expect
            Assert.NotNull(capturedHashValue);
            Assert.Equal(expectedHash, capturedHashValue);
        }
    }
}