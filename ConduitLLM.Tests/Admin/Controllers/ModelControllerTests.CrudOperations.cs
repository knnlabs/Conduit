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
    /// Unit tests for ModelController CRUD operations
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ModelControllerCrudOperationsTests
    {
        private readonly Mock<IModelRepository> _mockRepository;
        private readonly Mock<ILogger<ModelController>> _mockLogger;
        private readonly ModelController _controller;

        public ModelControllerCrudOperationsTests()
        {
            _mockRepository = new Mock<IModelRepository>();
            _mockLogger = new Mock<ILogger<ModelController>>();
            _controller = new ModelController(_mockRepository.Object, _mockLogger.Object);
        }

        #region CreateModel Tests

        [Fact]
        public async Task CreateModel_WithValidData_ShouldReturnCreatedWithModelDto()
        {
            // Arrange
            var createDto = new CreateModelDto
            {
                Name = "new-test-model",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                IsActive = true
            };

            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };
            var capabilities = new ModelCapabilities { Id = 1, SupportsChat = true, MaxTokens = 4096 };

            var createdModel = new Model
            {
                Id = 1,
                Name = createDto.Name,
                ModelSeriesId = createDto.ModelSeriesId,
                Series = series,
                ModelCapabilitiesId = createDto.ModelCapabilitiesId,
                Capabilities = capabilities,
                IsActive = createDto.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetByNameAsync(createDto.Name))
                .ReturnsAsync((Model?)null);
            _mockRepository.Setup(r => r.CreateAsync(It.IsAny<Model>()))
                .ReturnsAsync((Model m) => {
                    m.Id = 1; // Simulate the database setting the ID
                    return m;
                });
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(createdModel);

            // Act
            var result = await _controller.CreateModel(createDto);

            // Assert
            result.Should().BeOfType<CreatedAtActionResult>();
            var createdResult = result as CreatedAtActionResult;
            createdResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
            createdResult.ActionName.Should().Be(nameof(ModelController.GetModelById));
            createdResult.RouteValues!["id"].Should().Be(1);

            var dto = createdResult.Value as ModelDto;
            dto.Should().NotBeNull();
            dto!.Id.Should().Be(1);
            dto.Name.Should().Be("new-test-model");
            dto.IsActive.Should().BeTrue();

            _mockRepository.Verify(r => r.CreateAsync(It.Is<Model>(m => 
                m.Name == createDto.Name &&
                m.ModelSeriesId == createDto.ModelSeriesId &&
                m.ModelCapabilitiesId == createDto.ModelCapabilitiesId &&
                m.IsActive == createDto.IsActive)), Times.Once);
        }

        [Fact]
        public async Task CreateModel_WithNullData_ShouldReturnBadRequest()
        {
            // Arrange
            CreateModelDto createDto = null!;

            // Act
            var result = await _controller.CreateModel(createDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().Be("Model data is required");

            _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Model>()), Times.Never);
        }

        [Fact]
        public async Task CreateModel_WithEmptyName_ShouldReturnBadRequest()
        {
            // Arrange
            var createDto = new CreateModelDto
            {
                Name = "",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                IsActive = true
            };

            // Act
            var result = await _controller.CreateModel(createDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().Be("Model name is required");

            _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Model>()), Times.Never);
        }

        [Fact]
        public async Task CreateModel_WithDuplicateName_ShouldReturnConflict()
        {
            // Arrange
            var createDto = new CreateModelDto
            {
                Name = "existing-model",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                IsActive = true
            };

            var existingModel = new Model
            {
                Id = 99,
                Name = "existing-model"
            };

            _mockRepository.Setup(r => r.GetByNameAsync(createDto.Name))
                .ReturnsAsync(existingModel);

            // Act
            var result = await _controller.CreateModel(createDto);

            // Assert
            result.Should().BeOfType<ConflictObjectResult>();
            var conflictResult = result as ConflictObjectResult;
            conflictResult!.Value.Should().Be("A model with name 'existing-model' already exists");

            _mockRepository.Verify(r => r.CreateAsync(It.IsAny<Model>()), Times.Never);
        }

        [Fact]
        public async Task CreateModel_WhenRepositoryThrows_ShouldReturn500()
        {
            // Arrange
            var createDto = new CreateModelDto
            {
                Name = "test-model",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                IsActive = true
            };

            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.CreateAsync(It.IsAny<Model>()))
                .ThrowsAsync(exception);

            // Act
            var result = await _controller.CreateModel(createDto);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().Be("An error occurred while creating the model");

            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error creating model")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region UpdateModel Tests

        [Fact]
        public async Task UpdateModel_WithValidData_ShouldReturnOkWithUpdatedModel()
        {
            // Arrange
            var modelId = 1;
            var updateDto = new UpdateModelDto
            {
                Name = "updated-model-name",
                IsActive = false
            };

            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };
            var capabilities = new ModelCapabilities { Id = 1, SupportsChat = true, MaxTokens = 4096 };

            var existingModel = new Model
            {
                Id = modelId,
                Name = "old-model-name",
                ModelSeriesId = 1,
                Series = series,
                ModelCapabilitiesId = 1,
                Capabilities = capabilities,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var updatedModel = new Model
            {
                Id = modelId,
                Name = updateDto.Name,
                ModelSeriesId = 1,
                Series = series,
                ModelCapabilitiesId = 1,
                Capabilities = capabilities,
                IsActive = updateDto.IsActive ?? existingModel.IsActive,
                CreatedAt = existingModel.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(existingModel);
            _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Model>()))
                .ReturnsAsync(updatedModel);

            // Act
            var result = await _controller.UpdateModel(modelId, updateDto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            
            var dto = okResult!.Value as ModelDto;
            dto.Should().NotBeNull();
            dto!.Id.Should().Be(modelId);
            dto.Name.Should().Be("updated-model-name");
            dto.IsActive.Should().BeFalse();

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(modelId), Times.Once);
            _mockRepository.Verify(r => r.UpdateAsync(It.Is<Model>(m => 
                m.Id == modelId &&
                m.Name == updateDto.Name &&
                m.IsActive == updateDto.IsActive)), Times.Once);
        }

        [Fact]
        public async Task UpdateModel_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var modelId = 999;
            var updateDto = new UpdateModelDto
            {
                Name = "updated-name",
                IsActive = false
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync((Model?)null);

            // Act
            var result = await _controller.UpdateModel(modelId, updateDto);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().Be($"Model with ID {modelId} not found");

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(modelId), Times.Once);
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Model>()), Times.Never);
        }

        [Fact]
        public async Task UpdateModel_WithNullData_ShouldReturnBadRequest()
        {
            // Arrange
            var modelId = 1;
            UpdateModelDto updateDto = null!;

            // Act
            var result = await _controller.UpdateModel(modelId, updateDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().Be("Update data is required");

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<int>()), Times.Never);
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Model>()), Times.Never);
        }

        [Fact]
        public async Task UpdateModel_WhenGetByIdFails_ShouldReturn500()
        {
            // Arrange
            var modelId = 1;
            var updateDto = new UpdateModelDto
            {
                Name = "updated-name",
                IsActive = false
            };

            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ThrowsAsync(exception);

            // Act
            var result = await _controller.UpdateModel(modelId, updateDto);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().Be("An error occurred while updating the model");

            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Model>()), Times.Never);
        }

        [Fact]
        public async Task UpdateModel_WhenRepositoryThrows_ShouldReturn500()
        {
            // Arrange
            var modelId = 1;
            var updateDto = new UpdateModelDto
            {
                Name = "updated-name",
                IsActive = false
            };

            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ThrowsAsync(exception);

            // Act
            var result = await _controller.UpdateModel(modelId, updateDto);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().Be("An error occurred while updating the model");

            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error updating model with ID")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region DeleteModel Tests

        [Fact]
        public async Task DeleteModel_WithValidId_ShouldReturnNoContent()
        {
            // Arrange
            var modelId = 1;
            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };
            var capabilities = new ModelCapabilities { Id = 1, SupportsChat = true };

            var existingModel = new Model
            {
                Id = modelId,
                Name = "test-model",
                ModelSeriesId = 1,
                Series = series,
                ModelCapabilitiesId = 1,
                Capabilities = capabilities,
                IsActive = true
            };

            _mockRepository.Setup(r => r.GetByIdAsync(modelId))
                .ReturnsAsync(existingModel);
            _mockRepository.Setup(r => r.HasMappingReferencesAsync(modelId))
                .ReturnsAsync(false);
            _mockRepository.Setup(r => r.DeleteAsync(modelId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteModel(modelId);

            // Assert
            result.Should().BeOfType<NoContentResult>();

            _mockRepository.Verify(r => r.GetByIdAsync(modelId), Times.Once);
            _mockRepository.Verify(r => r.HasMappingReferencesAsync(modelId), Times.Once);
            _mockRepository.Verify(r => r.DeleteAsync(modelId), Times.Once);
        }

        [Fact]
        public async Task DeleteModel_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var modelId = 999;
            _mockRepository.Setup(r => r.GetByIdAsync(modelId))
                .ReturnsAsync((Model?)null);

            // Act
            var result = await _controller.DeleteModel(modelId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().Be($"Model with ID {modelId} not found");

            _mockRepository.Verify(r => r.GetByIdAsync(modelId), Times.Once);
            _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteModel_WhenRepositoryThrows_ShouldReturn500()
        {
            // Arrange
            var modelId = 1;
            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.GetByIdAsync(modelId))
                .ThrowsAsync(exception);

            // Act
            var result = await _controller.DeleteModel(modelId);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            objectResult.Value.Should().Be("An error occurred while deleting the model");

            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error deleting model with ID")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}