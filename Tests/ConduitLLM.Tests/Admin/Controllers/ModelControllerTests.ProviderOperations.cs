using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;

using FluentAssertions;

using ConduitLLM.Configuration.Messaging;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Unit tests for ModelController provider-related operations
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ModelControllerProviderOperationsTests
    {
        private readonly Mock<IModelRepository> _mockRepository;
        private readonly Mock<IAdminModelProviderMappingService> _mockMappingService;
        private readonly Mock<IProviderRepository> _mockProviderRepository;
        private readonly Mock<IEventBus> _mockPublishEndpoint;
        private readonly Mock<ILogger<ModelController>> _mockLogger;
        private readonly ModelController _controller;

        public ModelControllerProviderOperationsTests()
        {
            _mockRepository = new Mock<IModelRepository>();
            _mockMappingService = new Mock<IAdminModelProviderMappingService>();
            _mockProviderRepository = new Mock<IProviderRepository>();
            _mockPublishEndpoint = new Mock<IEventBus>();
            _mockLogger = new Mock<ILogger<ModelController>>();
            _controller = new ModelController(_mockRepository.Object, _mockMappingService.Object, _mockProviderRepository.Object, _mockPublishEndpoint.Object, _mockLogger.Object);
        }

        #region GetModelsByProvider Tests

        [Fact]
        public async Task GetModelsByProvider_WithValidProvider_ShouldReturnOkWithModels()
        {
            // Arrange
            var provider = "groq";
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };
            // Remove unnecessary variable declaration

            var models = new List<Model>
            {
                new Model
                {
                    Id = 1,
                    Name = "llama-3.1-8b",
                    ModelSeriesId = 1,
                    Series = series,
                    IsActive = true,
                    SupportsChat = true,
                    MaxInputTokens = 4096,
                    MaxOutputTokens = 2048,
                    TokenizerType = TokenizerType.Cl100KBase,
                    Identifiers = new List<ModelProviderTypeAssociation>
                    {
                        new ModelProviderTypeAssociation 
                        { 
                            Id = 1, 
                            ModelId = 1, 
                            Identifier = "llama-3.1-8b-instant", 
                            Provider = ProviderType.Groq,
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
                    IsActive = true,
                    SupportsChat = true,
                    SupportsStreaming = true,
                    MaxInputTokens = 32768,
                    MaxOutputTokens = 8192,
                    TokenizerType = TokenizerType.Cl100KBase,
                    Identifiers = new List<ModelProviderTypeAssociation>
                    {
                        new ModelProviderTypeAssociation 
                        { 
                            Id = 2, 
                            ModelId = 2, 
                            Identifier = "mixtral-8x7b-32768", 
                            Provider = ProviderType.Groq,
                            IsPrimary = true
                        }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetByProviderAsync(ProviderType.Groq))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelWithProviderIdDto>>().Subject;
            dtos.Should().HaveCount(2);

            var firstDto = dtos.First();
            firstDto.Id.Should().Be(1);
            firstDto.Name.Should().Be("llama-3.1-8b");
            firstDto.ProviderModelId.Should().Be("llama-3.1-8b-instant");
            firstDto.SupportsChat.Should().BeTrue();

            var secondDto = dtos.Last();
            secondDto.Id.Should().Be(2);
            secondDto.Name.Should().Be("mixtral-8x7b");
            secondDto.ProviderModelId.Should().Be("mixtral-8x7b-32768");

            _mockRepository.Verify(r => r.GetByProviderAsync(ProviderType.Groq), Times.Once);
        }

        [Fact]
        public async Task GetModelsByProvider_WithEmptyProvider_ShouldReturnBadRequest()
        {
            // Arrange
            var provider = "";

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Provider name is required");

            _mockRepository.Verify(r => r.GetByProviderAsync(It.IsAny<ProviderType>()), Times.Never);
        }

        [Fact]
        public async Task GetModelsByProvider_WithNullProvider_ShouldReturnBadRequest()
        {
            // Arrange
            string provider = null!;

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Provider name is required");

            _mockRepository.Verify(r => r.GetByProviderAsync(It.IsAny<ProviderType>()), Times.Never);
        }

        [Fact]
        public async Task GetModelsByProvider_WithWhitespaceProvider_ShouldReturnBadRequest()
        {
            // Arrange
            var provider = "   ";

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Provider name is required");

            _mockRepository.Verify(r => r.GetByProviderAsync(It.IsAny<ProviderType>()), Times.Never);
        }

        [Fact]
        public async Task GetModelsByProvider_WithInvalidProvider_ShouldReturnBadRequest()
        {
            // Arrange
            var provider = "nonexistent";

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badResult = result as BadRequestObjectResult;
            badResult!.Value.Should().NotBeNull();
            badResult.Value.ToString().Should().Contain("Invalid provider");
            badResult.Value.ToString().Should().Contain("nonexistent");

            _mockRepository.Verify(r => r.GetByProviderAsync(It.IsAny<ProviderType>()), Times.Never);
        }

        [Fact]
        public async Task GetModelsByProvider_WithModelHavingProviderIdentifier_ShouldReturnCorrectIdentifier()
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
                    IsActive = true,
                    Identifiers = new List<ModelProviderTypeAssociation>
                    {
                        // Identifier for groq provider
                        new ModelProviderTypeAssociation
                        {
                            Id = 1,
                            ModelId = 1,
                            Identifier = "test-model-groq",
                            Provider = ProviderType.Groq,
                            IsPrimary = true
                        }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetByProviderAsync(ProviderType.Groq))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelWithProviderIdDto>>().Subject;
            var dto = dtos.First();

            // Should use the groq-specific identifier
            dto.ProviderModelId.Should().Be("test-model-groq");
        }

        [Fact]
        public async Task GetModelsByProvider_WithCaseInsensitiveProviderMatch_ShouldReturnCorrectIdentifier()
        {
            // Arrange
            var provider = "GROQ"; // Uppercase
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
                    IsActive = true,
                    Identifiers = new List<ModelProviderTypeAssociation>
                    {
                        new ModelProviderTypeAssociation 
                        { 
                            Id = 1, 
                            ModelId = 1, 
                            Identifier = "test-model-groq", 
                            Provider = ProviderType.Groq, // Lowercase in DB
                            IsPrimary = true
                        }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetByProviderAsync(ProviderType.Groq))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelWithProviderIdDto>>().Subject;
            var dto = dtos.First();

            // Should match case-insensitively
            dto.ProviderModelId.Should().Be("test-model-groq");
        }

        [Fact]
        public async Task GetModelsByProvider_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var provider = "groq";
            var exception = new Exception("Database connection failed");

            _mockRepository.Setup(r => r.GetByProviderAsync(ProviderType.Groq))
                .ThrowsAsync(exception);

            // Act & Assert — error→HTTP mapping now happens in AdminExceptionMiddleware
            var act = async () => await _controller.GetModelsByProvider(provider);
            await act.Should().ThrowAsync<Exception>();
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
                     // Null capabilities
                    IsActive = true,
                    Identifiers = new List<ModelProviderTypeAssociation>
                    {
                        new ModelProviderTypeAssociation 
                        { 
                            Id = 1, 
                            ModelId = 1, 
                            Identifier = "test-model", 
                            Provider = ProviderType.Groq,
                            IsPrimary = true
                        }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetByProviderAsync(ProviderType.Groq))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetModelsByProvider(provider);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelWithProviderIdDto>>().Subject;
            var dto = dtos.First();

            // After consolidation, capability fields have default values
            dto.SupportsChat.Should().BeFalse();
            dto.SupportsVision.Should().BeFalse();
            dto.SupportsStreaming.Should().BeFalse();
            dto.MaxInputTokens.Should().Be(null);
            dto.MaxOutputTokens.Should().Be(null);
        }

        #endregion
    }
}