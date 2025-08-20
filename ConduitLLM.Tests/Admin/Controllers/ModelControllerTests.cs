using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Unit tests for ModelController
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ModelControllerTests
    {
        private readonly Mock<IModelRepository> _mockRepository;
        private readonly Mock<ILogger<ModelController>> _mockLogger;
        private readonly ModelController _controller;

        public ModelControllerTests()
        {
            _mockRepository = new Mock<IModelRepository>();
            _mockLogger = new Mock<ILogger<ModelController>>();
            _controller = new ModelController(_mockRepository.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelController(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelController(_mockRepository.Object, null!));
        }

        #endregion

        #region GetModelsByProvider Tests

        [Fact]
        public async Task GetModelsByProvider_WithValidProvider_ShouldReturnOkWithModels()
        {
            // Arrange
            var provider = "groq";
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };
            var capabilities = new ModelCapabilities 
            { 
                Id = 1, 
                SupportsChat = true,
                MaxTokens = 4096 
            };

            var models = new List<Model>
            {
                new Model
                {
                    Id = 1,
                    Name = "llama-3.1-8b",
                    ModelSeriesId = 1,
                    Series = series,
                    ModelCapabilitiesId = 1,
                    Capabilities = capabilities,
                    IsActive = true,
                    Identifiers = new List<ModelIdentifier>
                    {
                        new ModelIdentifier 
                        { 
                            Id = 1, 
                            ModelId = 1, 
                            Identifier = "llama-3.1-8b-instant", 
                            Provider = "groq",
                            IsPrimary = true
                        }
                    }
                },
                new Model
                {
                    Id = 2,
                    Name = "mixtral-8x7b",
                    ModelSeriesId = 1,
                    Series = series,
                    ModelCapabilitiesId = 1,
                    Capabilities = capabilities,
                    IsActive = true,
                    Identifiers = new List<ModelIdentifier>
                    {
                        new ModelIdentifier 
                        { 
                            Id = 2, 
                            ModelId = 2, 
                            Identifier = "mixtral-8x7b-32768", 
                            Provider = "groq",
                            IsPrimary = true
                        }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetByProviderAsync(provider))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            var dtos = okResult!.Value as IEnumerable<ModelWithProviderIdDto>;
            dtos.Should().NotBeNull();
            dtos.Should().HaveCount(2);

            var firstDto = dtos!.First();
            firstDto.Id.Should().Be(1);
            firstDto.Name.Should().Be("llama-3.1-8b");
            firstDto.ProviderModelId.Should().Be("llama-3.1-8b-instant");
            firstDto.Capabilities.Should().NotBeNull();

            var secondDto = dtos!.Last();
            secondDto.Id.Should().Be(2);
            secondDto.Name.Should().Be("mixtral-8x7b");
            secondDto.ProviderModelId.Should().Be("mixtral-8x7b-32768");

            _mockRepository.Verify(r => r.GetByProviderAsync(provider), Times.Once);
        }

        [Fact]
        public async Task GetModelsByProvider_WithEmptyProvider_ShouldReturnBadRequest()
        {
            // Arrange
            var provider = "";

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().Be("Provider name is required");

            _mockRepository.Verify(r => r.GetByProviderAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetModelsByProvider_WithNullProvider_ShouldReturnBadRequest()
        {
            // Arrange
            string provider = null!;

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().Be("Provider name is required");

            _mockRepository.Verify(r => r.GetByProviderAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetModelsByProvider_WithWhitespaceProvider_ShouldReturnBadRequest()
        {
            // Arrange
            var provider = "   ";

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().Be("Provider name is required");

            _mockRepository.Verify(r => r.GetByProviderAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetModelsByProvider_WithNoModels_ShouldReturnOkWithEmptyList()
        {
            // Arrange
            var provider = "nonexistent";
            _mockRepository.Setup(r => r.GetByProviderAsync(provider))
                .ReturnsAsync(new List<Model>());

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            var dtos = okResult!.Value as IEnumerable<ModelWithProviderIdDto>;
            dtos.Should().NotBeNull();
            dtos.Should().BeEmpty();

            _mockRepository.Verify(r => r.GetByProviderAsync(provider), Times.Once);
        }

        [Fact]
        public async Task GetModelsByProvider_WithModelMissingIdentifier_ShouldUseFallbackName()
        {
            // Arrange
            var provider = "groq";
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };
            var capabilities = new ModelCapabilities { Id = 1, SupportsChat = true };

            var models = new List<Model>
            {
                new Model
                {
                    Id = 1,
                    Name = "test-model",
                    ModelSeriesId = 1,
                    Series = series,
                    ModelCapabilitiesId = 1,
                    Capabilities = capabilities,
                    IsActive = true,
                    Identifiers = new List<ModelIdentifier>
                    {
                        // Identifier for different provider
                        new ModelIdentifier 
                        { 
                            Id = 1, 
                            ModelId = 1, 
                            Identifier = "test-model-openai", 
                            Provider = "openai",
                            IsPrimary = true
                        }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetByProviderAsync(provider))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var dtos = okResult!.Value as IEnumerable<ModelWithProviderIdDto>;
            var dto = dtos!.First();
            
            // Should fallback to model name when no matching provider identifier
            dto.ProviderModelId.Should().Be("test-model");
        }

        [Fact]
        public async Task GetModelsByProvider_WithCaseInsensitiveProviderMatch_ShouldReturnCorrectIdentifier()
        {
            // Arrange
            var provider = "GROQ"; // Uppercase
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };
            var capabilities = new ModelCapabilities { Id = 1, SupportsChat = true };

            var models = new List<Model>
            {
                new Model
                {
                    Id = 1,
                    Name = "test-model",
                    ModelSeriesId = 1,
                    Series = series,
                    ModelCapabilitiesId = 1,
                    Capabilities = capabilities,
                    IsActive = true,
                    Identifiers = new List<ModelIdentifier>
                    {
                        new ModelIdentifier 
                        { 
                            Id = 1, 
                            ModelId = 1, 
                            Identifier = "test-model-groq", 
                            Provider = "groq", // Lowercase in DB
                            IsPrimary = true
                        }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetByProviderAsync(provider))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var dtos = okResult!.Value as IEnumerable<ModelWithProviderIdDto>;
            var dto = dtos!.First();
            
            // Should match case-insensitively
            dto.ProviderModelId.Should().Be("test-model-groq");
        }

        [Fact]
        public async Task GetModelsByProvider_WhenRepositoryThrows_ShouldReturn500()
        {
            // Arrange
            var provider = "groq";
            var exception = new Exception("Database connection failed");
            
            _mockRepository.Setup(r => r.GetByProviderAsync(provider))
                .ThrowsAsync(exception);

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().Be("An error occurred while retrieving models");

            // Verify logging occurred
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting models for provider")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetModelsByProvider_WithNullCapabilities_ShouldHandleGracefully()
        {
            // Arrange
            var provider = "groq";
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };

            var models = new List<Model>
            {
                new Model
                {
                    Id = 1,
                    Name = "test-model",
                    ModelSeriesId = 1,
                    Series = series,
                    ModelCapabilitiesId = 1,
                    Capabilities = null, // Null capabilities
                    IsActive = true,
                    Identifiers = new List<ModelIdentifier>
                    {
                        new ModelIdentifier 
                        { 
                            Id = 1, 
                            ModelId = 1, 
                            Identifier = "test-model", 
                            Provider = "groq",
                            IsPrimary = true
                        }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetByProviderAsync(provider))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var dtos = okResult!.Value as IEnumerable<ModelWithProviderIdDto>;
            var dto = dtos!.First();
            
            dto.Capabilities.Should().BeNull();
        }

        #endregion
    }
}