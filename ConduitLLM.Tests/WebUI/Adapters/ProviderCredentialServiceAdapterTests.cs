using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Tests.WebUI.Adapters
{
    public class ProviderCredentialServiceAdapterTests
    {
        private readonly Mock<IAdminApiClient> _adminApiClientMock;
        private readonly Mock<ILogger<ProviderCredentialServiceAdapter>> _loggerMock;
        private readonly ProviderCredentialServiceAdapter _adapter;

        public ProviderCredentialServiceAdapterTests()
        {
            _adminApiClientMock = new Mock<IAdminApiClient>();
            _loggerMock = new Mock<ILogger<ProviderCredentialServiceAdapter>>();
            _adapter = new ProviderCredentialServiceAdapter(_adminApiClientMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetAllProviderCredentialsAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedCredentials = new List<ProviderCredentialDto>
            {
                new ProviderCredentialDto { Id = 1, ProviderName = "Provider1", ApiKey = "[MASKED]" },
                new ProviderCredentialDto { Id = 2, ProviderName = "Provider2", ApiKey = "[MASKED]" }
            };

            _adminApiClientMock.Setup(c => c.GetAllProviderCredentialsAsync())
                .ReturnsAsync(expectedCredentials);

            // Act
            var result = await _adapter.GetAllProviderCredentialsAsync();

            // Assert
            Assert.Equal(expectedCredentials.Count, result.Count());
            _adminApiClientMock.Verify(c => c.GetAllProviderCredentialsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetProviderCredentialByIdAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedCredential = new ProviderCredentialDto { Id = 1, ProviderName = "TestProvider", ApiKey = "[MASKED]" };

            _adminApiClientMock.Setup(c => c.GetProviderCredentialByIdAsync(1))
                .ReturnsAsync(expectedCredential);

            // Act
            var result = await _adapter.GetProviderCredentialByIdAsync(1);

            // Assert
            Assert.Same(expectedCredential, result);
            _adminApiClientMock.Verify(c => c.GetProviderCredentialByIdAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetProviderCredentialByNameAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedCredential = new ProviderCredentialDto { Id = 1, ProviderName = "TestProvider", ApiKey = "[MASKED]" };

            _adminApiClientMock.Setup(c => c.GetProviderCredentialByNameAsync("TestProvider"))
                .ReturnsAsync(expectedCredential);

            // Act
            var result = await _adapter.GetProviderCredentialByNameAsync("TestProvider");

            // Assert
            Assert.Same(expectedCredential, result);
            _adminApiClientMock.Verify(c => c.GetProviderCredentialByNameAsync("TestProvider"), Times.Once);
        }

        [Fact]
        public async Task CreateProviderCredentialAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var createDto = new CreateProviderCredentialDto { ProviderName = "NewProvider", ApiKey = "api-key-123" };
            var expectedCredential = new ProviderCredentialDto { Id = 3, ProviderName = "NewProvider", ApiKey = "[MASKED]" };

            _adminApiClientMock.Setup(c => c.CreateProviderCredentialAsync(createDto))
                .ReturnsAsync(expectedCredential);

            // Act
            var result = await _adapter.CreateProviderCredentialAsync(createDto);

            // Assert
            Assert.Same(expectedCredential, result);
            _adminApiClientMock.Verify(c => c.CreateProviderCredentialAsync(createDto), Times.Once);
        }

        [Fact]
        public async Task UpdateProviderCredentialAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var updateDto = new UpdateProviderCredentialDto { ApiKey = "new-api-key" };
            var expectedCredential = new ProviderCredentialDto { Id = 1, ProviderName = "TestProvider", ApiKey = "[MASKED]" };

            _adminApiClientMock.Setup(c => c.UpdateProviderCredentialAsync(1, updateDto))
                .ReturnsAsync(expectedCredential);

            // Act
            var result = await _adapter.UpdateProviderCredentialAsync(1, updateDto);

            // Assert
            Assert.Same(expectedCredential, result);
            _adminApiClientMock.Verify(c => c.UpdateProviderCredentialAsync(1, updateDto), Times.Once);
        }

        [Fact]
        public async Task DeleteProviderCredentialAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.DeleteProviderCredentialAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _adapter.DeleteProviderCredentialAsync(1);

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.DeleteProviderCredentialAsync(1), Times.Once);
        }

        [Fact]
        public async Task TestProviderConnectionAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedResult = new ProviderConnectionTestResultDto
            {
                Success = true,
                Message = "Connection successful",
                ProviderName = "TestProvider"
            };

            _adminApiClientMock.Setup(c => c.TestProviderConnectionAsync("TestProvider"))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _adapter.TestProviderConnectionAsync("TestProvider");

            // Assert
            Assert.Same(expectedResult, result);
            _adminApiClientMock.Verify(c => c.TestProviderConnectionAsync("TestProvider"), Times.Once);
        }

        [Fact]
        public async Task GetCredentialsForProviderAsync_ReturnsCredentialsInDictionary()
        {
            // Arrange
            var credential = new ProviderCredentialDto
            {
                Id = 1,
                ProviderName = "TestProvider",
                ApiKey = "key123",
                ApiBase = "https://api.test.com",
                OrgId = "org-123",
                ProjectId = "project-123",
                Region = "us-east-1",
                EndpointUrl = "https://endpoint.test.com",
                DeploymentName = "deployment1"
            };

            _adminApiClientMock.Setup(c => c.GetProviderCredentialByNameAsync("TestProvider"))
                .ReturnsAsync(credential);

            // Act
            var result = await _adapter.GetCredentialsForProviderAsync("TestProvider");

            // Assert
            Assert.Equal(7, result.Count);
            Assert.Equal("key123", result["api_key"]);
            Assert.Equal("https://api.test.com", result["api_base"]);
            Assert.Equal("org-123", result["organization_id"]);
            Assert.Equal("project-123", result["project_id"]);
            Assert.Equal("us-east-1", result["region"]);
            Assert.Equal("https://endpoint.test.com", result["endpoint_url"]);
            Assert.Equal("deployment1", result["deployment_name"]);
            _adminApiClientMock.Verify(c => c.GetProviderCredentialByNameAsync("TestProvider"), Times.Once);
        }

        [Fact]
        public async Task GetCredentialsForProviderAsync_ReturnsEmptyDictionary_WhenNoCredentialFound()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetProviderCredentialByNameAsync("NonExistentProvider"))
                .ReturnsAsync((ProviderCredentialDto)null);

            // Act
            var result = await _adapter.GetCredentialsForProviderAsync("NonExistentProvider");

            // Assert
            Assert.Empty(result);
            _adminApiClientMock.Verify(c => c.GetProviderCredentialByNameAsync("NonExistentProvider"), Times.Once);
        }

        [Fact]
        public async Task GetCredentialsForProviderAsync_SkipsNullOrEmptyProperties()
        {
            // Arrange
            var credential = new ProviderCredentialDto
            {
                Id = 1,
                ProviderName = "TestProvider",
                ApiKey = "key123",
                // Other properties are null or empty
            };

            _adminApiClientMock.Setup(c => c.GetProviderCredentialByNameAsync("TestProvider"))
                .ReturnsAsync(credential);

            // Act
            var result = await _adapter.GetCredentialsForProviderAsync("TestProvider");

            // Assert
            Assert.Single(result);
            Assert.Equal("key123", result["api_key"]);
            _adminApiClientMock.Verify(c => c.GetProviderCredentialByNameAsync("TestProvider"), Times.Once);
        }

        [Fact]
        public async Task GetCredentialsForProviderAsync_HandlesExceptions()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetProviderCredentialByNameAsync("TestProvider"))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _adapter.GetCredentialsForProviderAsync("TestProvider");

            // Assert
            Assert.Empty(result);
            _adminApiClientMock.Verify(c => c.GetProviderCredentialByNameAsync("TestProvider"), Times.Once);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error getting credentials for provider")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}