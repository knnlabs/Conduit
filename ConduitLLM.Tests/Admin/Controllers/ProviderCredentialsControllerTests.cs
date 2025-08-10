using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static ConduitLLM.Admin.Controllers.ProviderCredentialsController;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Unit tests for the ProviderCredentialsController class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ProviderCredentialsControllerTests
    {
        private readonly Mock<IProviderRepository> _mockProviderRepository;
        private readonly Mock<IProviderKeyCredentialRepository> _mockKeyRepository;
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<ILLMClient> _mockClient;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<ProviderCredentialsController>> _mockLogger;
        private readonly ProviderCredentialsController _controller;
        private readonly ITestOutputHelper _output;

        public ProviderCredentialsControllerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockProviderRepository = new Mock<IProviderRepository>();
            _mockKeyRepository = new Mock<IProviderKeyCredentialRepository>();
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockClient = new Mock<ILLMClient>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<ProviderCredentialsController>>();
            
            _controller = new ProviderCredentialsController(
                _mockProviderRepository.Object,
                _mockKeyRepository.Object,
                _mockClientFactory.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullProviderRepository_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProviderCredentialsController(null!, _mockKeyRepository.Object, _mockClientFactory.Object, _mockPublishEndpoint.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullClientFactory_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProviderCredentialsController(_mockProviderRepository.Object, _mockKeyRepository.Object, null!, _mockPublishEndpoint.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProviderCredentialsController(_mockProviderRepository.Object, _mockKeyRepository.Object, _mockClientFactory.Object, _mockPublishEndpoint.Object, null!));
        }

        #endregion

        #region TestProviderConnectionWithCredentials Tests

        [Fact]
        public async Task TestProviderConnectionWithCredentials_WithValidCredentials_ShouldReturnSuccess()
        {
            // Arrange
            var testRequest = new TestProviderRequest
            {
                ProviderType = ProviderType.OpenAI,
                ApiKey = "valid-api-key",
                BaseUrl = "https://api.openai.com/v1"
            };

            var mockModels = new List<string> { "gpt-4", "gpt-3.5-turbo" };
            _mockClientFactory.Setup(x => x.CreateTestClient(It.IsAny<Provider>(), It.IsAny<ProviderKeyCredential>()))
                .Returns(_mockClient.Object);
            _mockClient.Setup(x => x.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockModels);

            // Act
            var result = await _controller.TestProviderConnectionWithCredentials(testRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value!;
            
            // Use reflection to access anonymous object properties
            var successProperty = response.GetType().GetProperty("Success");
            var messageProperty = response.GetType().GetProperty("Message");
            var modelCountProperty = response.GetType().GetProperty("ModelCount");
            
            Assert.NotNull(successProperty);
            Assert.NotNull(messageProperty);
            Assert.NotNull(modelCountProperty);
            
            Assert.True((bool)successProperty.GetValue(response)!);
            Assert.Contains("Connection successful", (string)messageProperty.GetValue(response)!);
            Assert.Equal(2, (int)modelCountProperty.GetValue(response)!);
        }

        [Fact]
        public async Task TestProviderConnectionWithCredentials_WithInvalidApiKey_ShouldReturnFailure()
        {
            // Arrange
            var testRequest = new TestProviderRequest
            {
                ProviderType = ProviderType.OpenAI,
                ApiKey = "invalid-api-key",
                BaseUrl = "https://api.openai.com/v1"
            };

            _mockClientFactory.Setup(x => x.CreateTestClient(It.IsAny<Provider>(), It.IsAny<ProviderKeyCredential>()))
                .Returns(_mockClient.Object);
            _mockClient.Setup(x => x.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new LLMCommunicationException("Invalid API key provided"));

            // Act
            var result = await _controller.TestProviderConnectionWithCredentials(testRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value!;
            
            // Use reflection to access anonymous object properties
            var successProperty = response.GetType().GetProperty("Success");
            var messageProperty = response.GetType().GetProperty("Message");
            var modelCountProperty = response.GetType().GetProperty("ModelCount");
            
            Assert.NotNull(successProperty);
            Assert.NotNull(messageProperty);
            Assert.NotNull(modelCountProperty);
            
            Assert.False((bool)successProperty.GetValue(response)!);
            Assert.Contains("Invalid API key", (string)messageProperty.GetValue(response)!);
            Assert.Equal(0, (int)modelCountProperty.GetValue(response)!);
        }

        [Fact]
        public async Task TestProviderConnectionWithCredentials_WithUnauthorizedError_ShouldReturnFailure()
        {
            // Arrange
            var testRequest = new TestProviderRequest
            {
                ProviderType = ProviderType.OpenAI,
                ApiKey = "badkey",
                BaseUrl = "https://api.openai.com/v1"
            };

            _mockClientFactory.Setup(x => x.CreateTestClient(It.IsAny<Provider>(), It.IsAny<ProviderKeyCredential>()))
                .Returns(_mockClient.Object);
            _mockClient.Setup(x => x.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new LLMCommunicationException("HTTP 401: Unauthorized - Invalid API key provided"));

            // Act
            var result = await _controller.TestProviderConnectionWithCredentials(testRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value!;
            
            // Use reflection to access anonymous object properties
            var successProperty = response.GetType().GetProperty("Success");
            var messageProperty = response.GetType().GetProperty("Message");
            var modelCountProperty = response.GetType().GetProperty("ModelCount");
            
            Assert.NotNull(successProperty);
            Assert.NotNull(messageProperty);
            Assert.NotNull(modelCountProperty);
            
            Assert.False((bool)successProperty.GetValue(response)!);
            Assert.Contains("Invalid API key", (string)messageProperty.GetValue(response)!);
            Assert.Equal(0, (int)modelCountProperty.GetValue(response)!);
        }

        [Fact]
        public async Task TestProviderConnectionWithCredentials_DoesNotReturnFallbackModels()
        {
            // Arrange - This test verifies the fix for the original issue
            var testRequest = new TestProviderRequest
            {
                ProviderType = ProviderType.OpenAI,
                ApiKey = "fake-key-should-fail",
                BaseUrl = "https://api.openai.com/v1"
            };

            _mockClientFactory.Setup(x => x.CreateTestClient(It.IsAny<Provider>(), It.IsAny<ProviderKeyCredential>()))
                .Returns(_mockClient.Object);
            
            // Mock the authentication failure that should occur with invalid key
            _mockClient.Setup(x => x.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new LLMCommunicationException("Authentication failed - invalid API key"));

            // Act
            var result = await _controller.TestProviderConnectionWithCredentials(testRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value!;
            
            // Use reflection to access anonymous object properties
            var successProperty = response.GetType().GetProperty("Success");
            var messageProperty = response.GetType().GetProperty("Message");
            var modelCountProperty = response.GetType().GetProperty("ModelCount");
            
            Assert.NotNull(successProperty);
            Assert.NotNull(messageProperty);
            Assert.NotNull(modelCountProperty);
            
            // Verify the connection test properly fails (no fallback models returned)
            Assert.False((bool)successProperty.GetValue(response)!);
            Assert.Contains("Authentication failed", (string)messageProperty.GetValue(response)!);
            Assert.Equal(0, (int)modelCountProperty.GetValue(response)!);
            
            // Verify that ListModelsAsync was called (not bypassed by fallback)
            _mockClient.Verify(x => x.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TestProviderConnectionWithCredentials_WithEmptyApiKey_ShouldReturnInternalServerError()
        {
            // Arrange
            var testRequest = new TestProviderRequest
            {
                ProviderType = ProviderType.OpenAI,
                ApiKey = "", // Empty API key
                BaseUrl = "https://api.openai.com/v1"
            };

            // Mock the client factory to throw when creating client with empty key
            _mockClientFactory.Setup(x => x.CreateTestClient(It.IsAny<Provider>(), It.IsAny<ProviderKeyCredential>()))
                .Throws(new ArgumentException("API key is required for testing credentials"));

            // Act
            var result = await _controller.TestProviderConnectionWithCredentials(testRequest);

            // Assert - Client factory exceptions result in 500 Internal Server Error
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
            Assert.Equal("An unexpected error occurred.", statusResult.Value);
        }

        [Fact]
        public async Task TestProviderConnectionWithCredentials_WithNullApiKey_ShouldReturnInternalServerError()
        {
            // Arrange
            var testRequest = new TestProviderRequest
            {
                ProviderType = ProviderType.OpenAI,
                ApiKey = null, // Null API key
                BaseUrl = "https://api.openai.com/v1"
            };

            // Mock the client factory to throw when creating client with null key
            _mockClientFactory.Setup(x => x.CreateTestClient(It.IsAny<Provider>(), It.IsAny<ProviderKeyCredential>()))
                .Throws(new ArgumentException("API key is required for testing credentials"));

            // Act
            var result = await _controller.TestProviderConnectionWithCredentials(testRequest);

            // Assert - Client factory exceptions result in 500 Internal Server Error
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
            Assert.Equal("An unexpected error occurred.", statusResult.Value);
        }

        [Fact]
        public async Task TestProviderConnectionWithCredentials_WithGenericException_ShouldReturnFailure()
        {
            // Arrange
            var testRequest = new TestProviderRequest
            {
                ProviderType = ProviderType.OpenAI,
                ApiKey = "test-key",
                BaseUrl = "https://api.openai.com/v1"
            };

            _mockClientFactory.Setup(x => x.CreateTestClient(It.IsAny<Provider>(), It.IsAny<ProviderKeyCredential>()))
                .Returns(_mockClient.Object);
            _mockClient.Setup(x => x.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network timeout"));

            // Act
            var result = await _controller.TestProviderConnectionWithCredentials(testRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value!;
            
            // Use reflection to access anonymous object properties
            var successProperty = response.GetType().GetProperty("Success");
            var messageProperty = response.GetType().GetProperty("Message");
            var modelCountProperty = response.GetType().GetProperty("ModelCount");
            
            Assert.NotNull(successProperty);
            Assert.NotNull(messageProperty);
            Assert.NotNull(modelCountProperty);
            
            Assert.False((bool)successProperty.GetValue(response)!);
            Assert.Contains("Network timeout", (string)messageProperty.GetValue(response)!);
            Assert.Equal(0, (int)modelCountProperty.GetValue(response)!);
        }

        #endregion
    }
}