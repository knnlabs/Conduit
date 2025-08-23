using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using FluentAssertions;

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
        private readonly Mock<ILogger<ModelController>> _mockLogger;
        private readonly ModelController _controller;

        public ModelControllerGetOperationsTests()
        {
            _mockRepository = new Mock<IModelRepository>();
            _mockLogger = new Mock<ILogger<ModelController>>();
            _controller = new ModelController(_mockRepository.Object, _mockLogger.Object);
        }

        #region GetAllModels Tests

        [Fact]
        public async Task GetAllModels_WithModels_ShouldReturnOkWithModelDtos()
        {
            // Arrange
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };
            var capabilities = new ModelCapabilities { Id = 1, SupportsChat = true, MaxTokens = 4096 };

            var models = new List<Model>
            {
                new Model
                {
                    Id = 1,
                    Name = "test-model-1",
                    ModelSeriesId = 1,
                    Series = series,
                    ModelCapabilitiesId = 1,
                    Capabilities = capabilities,
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
                    ModelCapabilitiesId = 1,
                    Capabilities = capabilities,
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
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            
            var dtos = okResult!.Value as IEnumerable<ModelDto>;
            dtos.Should().NotBeNull();
            dtos.Should().HaveCount(2);

            var firstDto = dtos!.First();
            firstDto.Id.Should().Be(1);
            firstDto.Name.Should().Be("test-model-1");
            firstDto.IsActive.Should().BeTrue();
            firstDto.Capabilities.Should().NotBeNull();

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
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            
            var dtos = okResult!.Value as IEnumerable<ModelDto>;
            dtos.Should().NotBeNull();
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
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().Be("An error occurred while retrieving models");

            // Verify logging occurred
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting all models")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
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
            var capabilities = new ModelCapabilities { Id = 1, SupportsChat = true, MaxTokens = 4096 };

            var model = new Model
            {
                Id = modelId,
                Name = "test-model",
                ModelSeriesId = 1,
                Series = series,
                ModelCapabilitiesId = 1,
                Capabilities = capabilities,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(model);

            // Act
            var result = await _controller.GetModelById(modelId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            
            var dto = okResult!.Value as ModelDto;
            dto.Should().NotBeNull();
            dto!.Id.Should().Be(modelId);
            dto.Name.Should().Be("test-model");
            dto.IsActive.Should().BeTrue();
            dto.Capabilities.Should().NotBeNull();

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
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().Be($"Model with ID {modelId} not found");

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
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().Be("An error occurred while retrieving the model");

            // Verify logging occurred
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting model with ID")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
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
            var capabilities = new ModelCapabilities { Id = 1, SupportsChat = true };

            var model = new Model
            {
                Id = modelId,
                Name = "gpt-oss-120b",
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
                        ModelId = modelId, 
                        Identifier = "openai/gpt-oss-120b", 
                        Provider = "groq",
                        IsPrimary = true
                    },
                    new ModelIdentifier 
                    { 
                        Id = 2, 
                        ModelId = modelId, 
                        Identifier = "gpt-oss-120b", 
                        Provider = "fireworks",
                        IsPrimary = true
                    },
                    new ModelIdentifier 
                    { 
                        Id = 3, 
                        ModelId = modelId, 
                        Identifier = "gpt-oss-120b", 
                        Provider = "cerebras",
                        IsPrimary = false
                    }
                }
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(model);

            // Act
            var result = await _controller.GetModelIdentifiers(modelId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            
            var identifiers = okResult!.Value as IEnumerable<object>;
            identifiers.Should().NotBeNull();
            identifiers.Should().HaveCount(3);

            // Verify the structure by serializing to JSON and deserializing
            var json = System.Text.Json.JsonSerializer.Serialize(identifiers);
            var deserializedIdentifiers = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(json);
            
            deserializedIdentifiers.Should().HaveCount(3);
            
            deserializedIdentifiers[0].GetProperty("id").GetInt32().Should().Be(1);
            deserializedIdentifiers[0].GetProperty("identifier").GetString().Should().Be("openai/gpt-oss-120b");
            deserializedIdentifiers[0].GetProperty("provider").GetString().Should().Be("groq");
            deserializedIdentifiers[0].GetProperty("isPrimary").GetBoolean().Should().Be(true);

            deserializedIdentifiers[1].GetProperty("provider").GetString().Should().Be("fireworks");
            deserializedIdentifiers[2].GetProperty("provider").GetString().Should().Be("cerebras");
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
            var capabilities = new ModelCapabilities { Id = 1, SupportsChat = true };

            var model = new Model
            {
                Id = modelId,
                Name = "test-model",
                ModelSeriesId = 1,
                Series = series,
                ModelCapabilitiesId = 1,
                Capabilities = capabilities,
                IsActive = true,
                Identifiers = new List<ModelIdentifier>() // Empty identifiers
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(model);

            // Act
            var result = await _controller.GetModelIdentifiers(modelId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            
            var identifiers = okResult!.Value as IEnumerable<object>;
            identifiers.Should().NotBeNull();
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
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().Be($"Model with ID {modelId} not found");

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
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().Be("An error occurred while retrieving model identifiers");

            // Verify logging occurred
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting identifiers for model with ID")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}