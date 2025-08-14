using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class ProviderModelsControllerTests : ControllerTestBase
    {
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _mockDbContextFactory;
        private readonly Mock<IModelListService> _mockModelListService;
        private readonly Mock<ILogger<ProviderModelsController>> _mockLogger;
        private readonly ProviderModelsController _controller;

        public ProviderModelsControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockDbContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            _mockModelListService = new Mock<IModelListService>();
            _mockLogger = CreateLogger<ProviderModelsController>();

            _controller = new ProviderModelsController(
                _mockDbContextFactory.Object,
                _mockModelListService.Object,
                _mockLogger.Object);

            _controller.ControllerContext = CreateControllerContext();
        }

        #region GetProviderModels Tests

        [Fact]
        public async Task GetProviderModels_WithValidProvider_ShouldReturnSortedModelList()
        {
            // Arrange
            var providerId = 1;
            var models = new List<string> { "gpt-4", "gpt-3.5-turbo", "ada", "text-embedding-3-large" };
            
            var mockDbContext = new InMemoryDbContext();
            mockDbContext.Providers.Add(new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                BaseUrl = "https://api.openai.com",
                ProviderKeyCredentials = new List<ProviderKeyCredential>
                {
                    new ProviderKeyCredential
                    {
                        ApiKey = "test-api-key",
                        IsPrimary = true,
                        IsEnabled = true
                    }
                }
            });
            await mockDbContext.SaveChangesAsync();

            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDbContext);

            _mockModelListService.Setup(x => x.GetModelsForProviderAsync(
                    It.IsAny<Provider>(),
                    It.IsAny<ProviderKeyCredential>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetProviderModels(providerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedModels = Assert.IsType<List<string>>(okResult.Value);
            Assert.Equal(4, returnedModels.Count);
            // Verify sorting
            Assert.Equal("ada", returnedModels[0]);
            Assert.Equal("gpt-3.5-turbo", returnedModels[1]);
            Assert.Equal("gpt-4", returnedModels[2]);
            Assert.Equal("text-embedding-3-large", returnedModels[3]);
        }

        [Fact]
        public async Task GetProviderModels_WithForceRefresh_ShouldPassParameterToService()
        {
            // Arrange
            var providerId = 2; // Anthropic provider ID
            var forceRefresh = true;
            var models = new List<string> { "claude-3-opus", "claude-3-sonnet" };
            
            var mockDbContext = new InMemoryDbContext();
            mockDbContext.Providers.Add(new Provider
            {
                Id = 2,
                ProviderType = ProviderType.Groq,
                ProviderKeyCredentials = new List<ProviderKeyCredential>
                {
                    new ProviderKeyCredential
                    {
                        ApiKey = "test-api-key",
                        IsPrimary = true,
                        IsEnabled = true
                    }
                }
            });
            await mockDbContext.SaveChangesAsync();

            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDbContext);

            _mockModelListService.Setup(x => x.GetModelsForProviderAsync(
                    It.IsAny<Provider>(),
                    It.IsAny<ProviderKeyCredential>(),
                    forceRefresh,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(models)
                .Verifiable();

            // Act
            var result = await _controller.GetProviderModels(providerId, forceRefresh);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockModelListService.Verify(x => x.GetModelsForProviderAsync(
                It.IsAny<Provider>(),
                It.IsAny<ProviderKeyCredential>(),
                forceRefresh,
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task GetProviderModels_WithNonExistentProvider_ShouldReturnNotFound()
        {
            // Arrange
            var providerId = 999; // Non-existent provider ID
            var mockDbContext = new InMemoryDbContext();

            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDbContext);

            // Act
            var result = await _controller.GetProviderModels(providerId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
            Assert.Equal($"Provider with ID {providerId} not found", errorResponse.error.ToString());
        }

        [Fact]
        public async Task GetProviderModels_WithMissingApiKey_ShouldReturnBadRequest()
        {
            // Arrange
            var providerId = 1;
            var mockDbContext = new InMemoryDbContext();
            mockDbContext.Providers.Add(new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                ProviderKeyCredentials = new List<ProviderKeyCredential>
                {
                    new ProviderKeyCredential
                    {
                        ApiKey = null, // Missing API key
                        IsPrimary = true,
                        IsEnabled = true
                    }
                }
            });
            await mockDbContext.SaveChangesAsync();

            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDbContext);

            // Act
            var result = await _controller.GetProviderModels(providerId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(badRequestResult.Value);
            Assert.Equal("API key is required to retrieve models", errorResponse.error.ToString());
        }

        [Fact]
        public async Task GetProviderModels_WithValidProviderId_ShouldWork()
        {
            // Arrange
            var providerId = 1; // OpenAI provider ID
            var models = new List<string> { "gpt-4" };
            
            var mockDbContext = new InMemoryDbContext();
            mockDbContext.Providers.Add(new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                ProviderKeyCredentials = new List<ProviderKeyCredential>
                {
                    new ProviderKeyCredential
                    {
                        ApiKey = "test-api-key",
                        IsPrimary = true,
                        IsEnabled = true
                    }
                }
            });
            await mockDbContext.SaveChangesAsync();

            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDbContext);

            _mockModelListService.Setup(x => x.GetModelsForProviderAsync(
                    It.IsAny<Provider>(),
                    It.IsAny<ProviderKeyCredential>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetProviderModels(providerId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedModels = Assert.IsType<List<string>>(okResult.Value);
            Assert.Single(returnedModels);
        }

        [Fact]
        public async Task GetProviderModels_WithServiceException_ShouldReturn500()
        {
            // Arrange
            var providerId = 1;
            var mockDbContext = new InMemoryDbContext();
            mockDbContext.Providers.Add(new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                ProviderKeyCredentials = new List<ProviderKeyCredential>
                {
                    new ProviderKeyCredential
                    {
                        ApiKey = "test-api-key",
                        IsPrimary = true,
                        IsEnabled = true
                    }
                }
            });
            await mockDbContext.SaveChangesAsync();

            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDbContext);

            _mockModelListService.Setup(x => x.GetModelsForProviderAsync(
                    It.IsAny<Provider>(),
                    It.IsAny<ProviderKeyCredential>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.GetProviderModels(providerId);

            // Assert
            var internalServerErrorResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, internalServerErrorResult.StatusCode);
            var errorResponse = Assert.IsType<ErrorResponseDto>(internalServerErrorResult.Value);
            Assert.Equal("Failed to retrieve models: Service error", errorResponse.error.ToString());
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullDbContextFactory_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ProviderModelsController(
                null,
                _mockModelListService.Object,
                _mockLogger.Object));
            Assert.Equal("dbContextFactory", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullModelListService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ProviderModelsController(
                _mockDbContextFactory.Object,
                null,
                _mockLogger.Object));
            Assert.Equal("modelListService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ProviderModelsController(
                _mockDbContextFactory.Object,
                _mockModelListService.Object,
                null));
            Assert.Equal("logger", ex.ParamName);
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldNotRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(ProviderModelsController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.Null(authorizeAttribute); // This controller doesn't require authorization
        }

        #endregion

        // Helper class for in-memory database context
        private class InMemoryDbContext : ConduitDbContext
        {
            public InMemoryDbContext() : base(CreateInMemoryOptions())
            {
            }

            private static DbContextOptions<ConduitDbContext> CreateInMemoryOptions()
            {
                return new DbContextOptionsBuilder<ConduitDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
            }
        }
    }
}