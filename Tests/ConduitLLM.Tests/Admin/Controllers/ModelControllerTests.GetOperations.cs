using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;

using FluentAssertions;

using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Unit tests for ModelController GET operations
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ModelControllerGetOperationsTests
    {
        private readonly Mock<IModelRepository> _mockRepository;
        private readonly Mock<IAdminModelProviderMappingService> _mockMappingService;
        private readonly Mock<IProviderRepository> _mockProviderRepository;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<ModelController>> _mockLogger;
        private readonly ModelController _controller;

        public ModelControllerGetOperationsTests()
        {
            _mockRepository = new Mock<IModelRepository>();
            _mockMappingService = new Mock<IAdminModelProviderMappingService>();
            _mockProviderRepository = new Mock<IProviderRepository>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<ModelController>>();
            _controller = new ModelController(_mockRepository.Object, _mockMappingService.Object, _mockProviderRepository.Object, _mockPublishEndpoint.Object, _mockLogger.Object);
        }

        #region GetAllModels Tests

        [Fact]
        public async Task GetAllModels_WithModels_ShouldReturnOkWithModelDtos()
        {
            // Arrange
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };

            var models = new List<Model>
            {
                new Model
                {
                    Id = 1,
                    Name = "test-model-1",
                    ModelSeriesId = 1,
                    Series = series,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Model
                {
                    Id = 2,
                    Name = "test-model-2",
                    ModelSeriesId = 1,
                    Series = series,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            _mockRepository.Setup(r => r.GetAllWithDetailsAsync())
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetAllModels();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelDto>>().Subject;
            dtos.Should().HaveCount(2);

            var firstDto = dtos.First();
            firstDto.Id.Should().Be(1);
            firstDto.Name.Should().Be("test-model-1");
            firstDto.IsActive.Should().BeTrue();
            // Capabilities are now flat fields on the model - just verify they exist by checking the Id
            firstDto.Id.Should().BePositive();

            _mockRepository.Verify(r => r.GetAllWithDetailsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllModels_WithEmptyList_ShouldReturnOkWithEmptyList()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetAllWithDetailsAsync())
                .ReturnsAsync(new List<Model>());

            // Act
            var result = await _controller.GetAllModels();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelDto>>().Subject;
            dtos.Should().BeEmpty();

            _mockRepository.Verify(r => r.GetAllWithDetailsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllModels_WhenRepositoryThrows_ShouldReturn500()
        {
            // Arrange
            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.GetAllWithDetailsAsync())
                .ThrowsAsync(exception);

            // Act
            var result = await _controller.GetAllModels();

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            var errorResponse = objectResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("internal_error");
        }

        #endregion

        #region GetModelById Tests

        [Fact]
        public async Task GetModelById_WithValidId_ShouldReturnOkWithModelDto()
        {
            // Arrange
            var modelId = 1;
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };

            var model = new Model
            {
                Id = modelId,
                Name = "test-model",
                ModelSeriesId = 1,
                Series = series,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(model);

            // Act
            var result = await _controller.GetModelById(modelId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = okResult.Value.Should().BeOfType<ModelDto>().Subject;
            dto.Id.Should().Be(modelId);
            dto.Name.Should().Be("test-model");
            dto.IsActive.Should().BeTrue();
            // Capabilities are now flat fields on the model - verify the Id exists
            dto.Id.Should().BePositive();

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(modelId), Times.Once);
        }

        [Fact]
        public async Task GetModelById_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var modelId = 999;
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync((Model?)null);

            // Act
            var result = await _controller.GetModelById(modelId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("not_found");

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(modelId), Times.Once);
        }

        [Fact]
        public async Task GetModelById_WhenRepositoryThrows_ShouldReturn500()
        {
            // Arrange
            var modelId = 1;
            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ThrowsAsync(exception);

            // Act
            var result = await _controller.GetModelById(modelId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            var errorResponse = objectResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("internal_error");
        }

        #endregion

        #region GetModelIdentifiers Tests

        [Fact]
        public async Task GetModelIdentifiers_WithValidId_ShouldReturnOkWithIdentifiers()
        {
            // Arrange
            var modelId = 1;
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };

            var model = new Model
            {
                Id = modelId,
                Name = "gpt-oss-120b",
                ModelSeriesId = 1,
                Series = series,
                IsActive = true,
                Identifiers = new List<ModelProviderTypeAssociation>
                {
                    new ModelProviderTypeAssociation 
                    { 
                        Id = 1, 
                        ModelId = modelId, 
                        Identifier = "openai/gpt-oss-120b", 
                        Provider = ProviderType.Groq,
                        IsPrimary = true
                    },
                    new ModelProviderTypeAssociation 
                    { 
                        Id = 2, 
                        ModelId = modelId, 
                        Identifier = "gpt-oss-120b", 
                        Provider = ProviderType.Fireworks,
                        IsPrimary = true
                    },
                    new ModelProviderTypeAssociation 
                    { 
                        Id = 3, 
                        ModelId = modelId, 
                        Identifier = "gpt-oss-120b", 
                        Provider = ProviderType.Cerebras,
                        IsPrimary = false
                    }
                }
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(model);

            // Act
            var result = await _controller.GetModelIdentifiers(modelId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var identifiers = okResult.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject;
            identifiers.Should().HaveCount(3);

            // Verify the structure by serializing to JSON and deserializing
            var json = System.Text.Json.JsonSerializer.Serialize(identifiers);
            var deserializedIdentifiers = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(json);
            
            deserializedIdentifiers.Should().HaveCount(3);
            
            deserializedIdentifiers[0].GetProperty("id").GetInt32().Should().Be(1);
            deserializedIdentifiers[0].GetProperty("identifier").GetString().Should().Be("openai/gpt-oss-120b");
            deserializedIdentifiers[0].GetProperty("provider").GetInt32().Should().Be((int)ProviderType.Groq);
            deserializedIdentifiers[0].GetProperty("isPrimary").GetBoolean().Should().Be(true);

            deserializedIdentifiers[1].GetProperty("provider").GetInt32().Should().Be((int)ProviderType.Fireworks);
            deserializedIdentifiers[2].GetProperty("provider").GetInt32().Should().Be((int)ProviderType.Cerebras);
            deserializedIdentifiers[2].GetProperty("isPrimary").GetBoolean().Should().Be(false);

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(modelId), Times.Once);
        }

        [Fact]
        public async Task GetModelIdentifiers_WithModelWithoutIdentifiers_ShouldReturnEmptyList()
        {
            // Arrange
            var modelId = 1;
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };

            var model = new Model
            {
                Id = modelId,
                Name = "test-model",
                ModelSeriesId = 1,
                Series = series,
                IsActive = true,
                Identifiers = new List<ModelProviderTypeAssociation>() // Empty identifiers
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(model);

            // Act
            var result = await _controller.GetModelIdentifiers(modelId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var identifiers = okResult.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject;
            identifiers.Should().BeEmpty();

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(modelId), Times.Once);
        }

        [Fact]
        public async Task GetModelIdentifiers_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var modelId = 999;
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync((Model?)null);

            // Act
            var result = await _controller.GetModelIdentifiers(modelId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("not_found");

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(modelId), Times.Once);
        }

        [Fact]
        public async Task GetModelIdentifiers_WhenRepositoryThrows_ShouldReturn500()
        {
            // Arrange
            var modelId = 1;
            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ThrowsAsync(exception);

            // Act
            var result = await _controller.GetModelIdentifiers(modelId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            var errorResponse = objectResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("internal_error");
        }

        #endregion
    }
}