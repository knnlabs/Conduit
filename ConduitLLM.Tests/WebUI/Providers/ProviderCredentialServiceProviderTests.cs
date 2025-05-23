using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.WebUI.Providers
{
    public class AdminProviderCredentialTests
    {
        private readonly Mock<IAdminApiClient> _mockAdminApiClient;
        private readonly Mock<ILogger<AdminApiClient>> _loggerMock;
        private readonly IProviderCredentialService _service;

        public AdminProviderCredentialTests()
        {
            // Create a mock that implements both interfaces
            _mockAdminApiClient = new Mock<IAdminApiClient>();
            Mock<IProviderCredentialService> mockService = _mockAdminApiClient.As<IProviderCredentialService>();
            _loggerMock = new Mock<ILogger<AdminApiClient>>();
            
            // Use the mock as the service
            _service = mockService.Object;
        }

        [Fact]
        public async Task GetAllAsync_ReturnsCredentials_WhenApiReturnsData()
        {
            // Arrange
            var credentials = new List<ProviderCredentialDto>
            {
                new ProviderCredentialDto { Id = 1, ProviderName = "openai", ApiKey = "sk-***", BaseUrl = "https://api.openai.com" },
                new ProviderCredentialDto { Id = 2, ProviderName = "anthropic", ApiKey = "sk-***", BaseUrl = "https://api.anthropic.com" }
            };

            _mockAdminApiClient.Setup(c => c.GetAllProviderCredentialsAsync())
                .ReturnsAsync(credentials);

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count);
            Assert.Equal(1, resultList[0].Id);
            Assert.Equal("openai", resultList[0].ProviderName);
            Assert.Equal(2, resultList[1].Id);
            Assert.Equal("anthropic", resultList[1].ProviderName);
            
            _mockAdminApiClient.Verify(c => c.GetAllProviderCredentialsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsCredential_WhenCredentialExists()
        {
            // Arrange
            int id = 1;
            var credential = new ProviderCredentialDto 
            { 
                Id = id, 
                ProviderName = "openai", 
                ApiKey = "sk-***", 
                BaseUrl = "https://api.openai.com" 
            };

            _mockAdminApiClient.Setup(c => c.GetProviderCredentialByIdAsync(id))
                .ReturnsAsync(credential);

            // Act
            var result = await _service.GetByIdAsync(id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
            Assert.Equal("openai", result.ProviderName);
            Assert.Equal("sk-***", result.ApiKey);
            Assert.Equal("https://api.openai.com", result.BaseUrl);
            
            _mockAdminApiClient.Verify(c => c.GetProviderCredentialByIdAsync(id), Times.Once);
        }

        [Fact]
        public async Task GetByProviderNameAsync_ReturnsCredential_WhenCredentialExists()
        {
            // Arrange
            string providerName = "openai";
            var credential = new ProviderCredentialDto 
            { 
                Id = 1, 
                ProviderName = providerName, 
                ApiKey = "sk-***", 
                BaseUrl = "https://api.openai.com" 
            };

            _mockAdminApiClient.Setup(c => c.GetProviderCredentialByNameAsync(providerName))
                .ReturnsAsync(credential);

            // Act
            var result = await _service.GetByProviderNameAsync(providerName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal(providerName, result.ProviderName);
            Assert.Equal("sk-***", result.ApiKey);
            Assert.Equal("https://api.openai.com", result.BaseUrl);
            
            _mockAdminApiClient.Verify(c => c.GetProviderCredentialByNameAsync(providerName), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ReturnsNewCredential_WhenCreationSucceeds()
        {
            // Arrange
            var createDto = new CreateProviderCredentialDto
            {
                ProviderName = "openai",
                ApiKey = "sk-newkey",
                BaseUrl = "https://api.openai.com",
                OrganizationId = "org-123"
            };
            
            var createdCredential = new ProviderCredentialDto
            {
                Id = 1,
                ProviderName = "openai",
                ApiKey = "sk-***",
                BaseUrl = "https://api.openai.com",
                OrganizationId = "org-123"
            };

            _mockAdminApiClient.Setup(c => c.CreateProviderCredentialAsync(createDto))
                .ReturnsAsync(createdCredential);

            // Act
            var result = await _service.CreateAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("openai", result.ProviderName);
            Assert.Equal("sk-***", result.ApiKey); // API returns masked key
            Assert.Equal("https://api.openai.com", result.BaseUrl);
            Assert.Equal("org-123", result.OrganizationId);
            
            _mockAdminApiClient.Verify(c => c.CreateProviderCredentialAsync(createDto), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ReturnsUpdatedCredential_WhenUpdateSucceeds()
        {
            // Arrange
            int id = 1;
            var updateDto = new UpdateProviderCredentialDto
            {
                ApiKey = "sk-updatedkey",
                BaseUrl = "https://api.openai.com/v2",
                OrganizationId = "org-456"
            };
            
            var updatedCredential = new ProviderCredentialDto
            {
                Id = id,
                ProviderName = "openai",
                ApiKey = "sk-***",
                BaseUrl = "https://api.openai.com/v2",
                OrganizationId = "org-456"
            };

            _mockAdminApiClient.Setup(c => c.UpdateProviderCredentialAsync(id, updateDto))
                .ReturnsAsync(updatedCredential);

            // Act
            var result = await _service.UpdateAsync(id, updateDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
            Assert.Equal("openai", result.ProviderName);
            Assert.Equal("sk-***", result.ApiKey); // API returns masked key
            Assert.Equal("https://api.openai.com/v2", result.BaseUrl);
            Assert.Equal("org-456", result.OrganizationId);
            
            _mockAdminApiClient.Verify(c => c.UpdateProviderCredentialAsync(id, updateDto), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ReturnsTrue_WhenDeletionSucceeds()
        {
            // Arrange
            int id = 1;

            _mockAdminApiClient.Setup(c => c.DeleteProviderCredentialAsync(id))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteAsync(id);

            // Assert
            Assert.True(result);
            
            _mockAdminApiClient.Verify(c => c.DeleteProviderCredentialAsync(id), Times.Once);
        }

        [Fact]
        public async Task TestConnectionAsync_ReturnsSuccessResult_WhenConnectionSucceeds()
        {
            // Arrange
            string providerName = "openai";
            var testResult = new ProviderConnectionTestResultDto
            {
                Success = true,
                Message = "Connection successful",
                Models = new List<string> { "gpt-4", "gpt-3.5-turbo" }
            };

            _mockAdminApiClient.Setup(c => c.TestProviderConnectionAsync(providerName))
                .ReturnsAsync(testResult);

            // Act
            var result = await _service.TestConnectionAsync(providerName);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("Connection successful", result.Message);
            Assert.Equal(2, result.Models.Count);
            Assert.Contains("gpt-4", result.Models);
            Assert.Contains("gpt-3.5-turbo", result.Models);
            
            _mockAdminApiClient.Verify(c => c.TestProviderConnectionAsync(providerName), Times.Once);
        }

        [Fact]
        public async Task TestConnectionAsync_ReturnsFailureResult_WhenConnectionFails()
        {
            // Arrange
            string providerName = "openai";
            var testResult = new ProviderConnectionTestResultDto
            {
                Success = false,
                Message = "Invalid API key",
                Models = new List<string>()
            };

            _mockAdminApiClient.Setup(c => c.TestProviderConnectionAsync(providerName))
                .ReturnsAsync(testResult);

            // Act
            var result = await _service.TestConnectionAsync(providerName);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Invalid API key", result.Message);
            Assert.Empty(result.Models);
            
            _mockAdminApiClient.Verify(c => c.TestProviderConnectionAsync(providerName), Times.Once);
        }
    }
}