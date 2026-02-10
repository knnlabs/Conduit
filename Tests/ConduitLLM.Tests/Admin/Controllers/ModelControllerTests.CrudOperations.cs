using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Models.Models;
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
    /// Unit tests for ModelController CRUD operations
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ModelControllerCrudOperationsTests
    {
        private readonly Mock<IModelRepository> _mockRepository;
        private readonly Mock<IAdminModelProviderMappingService> _mockMappingService;
        private readonly Mock<IProviderRepository> _mockProviderRepository;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<ModelController>> _mockLogger;
        private readonly ModelController _controller;

        public ModelControllerCrudOperationsTests()
        {
            _mockRepository = new Mock<IModelRepository>();
            _mockMappingService = new Mock<IAdminModelProviderMappingService>();
            _mockProviderRepository = new Mock<IProviderRepository>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<ModelController>>();
            _controller = new ModelController(_mockRepository.Object, _mockMappingService.Object, _mockProviderRepository.Object, _mockPublishEndpoint.Object, _mockLogger.Object);
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
                
                IsActive = true
            };

            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };

            var createdModel = new Model
            {
                Id = 1,
                Name = createDto.Name,
                ModelSeriesId = createDto.ModelSeriesId,
                Series = series,
                IsActive = createDto.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetByNameAsync(createDto.Name))
                .ReturnsAsync((Model?)null);
            _mockRepository.Setup(r => r.CreateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Model m, CancellationToken _) => {
                    m.Id = 1; // Simulate the database setting the ID
                    return m;
                });
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(createdModel);

            // Act
            var result = await _controller.CreateModel(createDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
            createdResult.ActionName.Should().Be(nameof(ModelController.GetModelById));
            createdResult.RouteValues!["id"].Should().Be(1);

            var dto = createdResult.Value.Should().BeOfType<ModelDto>().Subject;
            dto.Id.Should().Be(1);
            dto.Name.Should().Be("new-test-model");
            dto.IsActive.Should().BeTrue();

            _mockRepository.Verify(r => r.CreateModelAsync(It.Is<Model>(m => 
                m.Name == createDto.Name &&
                m.ModelSeriesId == createDto.ModelSeriesId &&
                m.IsActive == createDto.IsActive)), Times.Once);
        }

        [Fact]
        public async Task CreateModel_WithModelParameters_ShouldReturnCreatedWithParameters()
        {
            // Arrange
            var createDto = new CreateModelDto
            {
                Name = "model-with-params",
                ModelSeriesId = 1,
                
                ModelParameters = "{\"temperature\": {\"min\": 0, \"max\": 1.5}}",
                IsActive = true
            };

            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };

            var createdModel = new Model
            {
                Id = 1,
                Name = createDto.Name,
                ModelSeriesId = createDto.ModelSeriesId,
                Series = series,
                ModelParameters = createDto.ModelParameters,
                IsActive = createDto.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetByNameAsync(createDto.Name))
                .ReturnsAsync((Model?)null);
            _mockRepository.Setup(r => r.CreateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Model m, CancellationToken _) => {
                    m.Id = 1;
                    return m;
                });
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(createdModel);

            // Act
            var result = await _controller.CreateModel(createDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            var dto = createdResult.Value.Should().BeOfType<ModelDto>().Subject;
            dto.ModelParameters.Should().Be("{\"temperature\": {\"min\": 0, \"max\": 1.5}}");

            _mockRepository.Verify(r => r.CreateModelAsync(It.Is<Model>(m => 
                m.ModelParameters == createDto.ModelParameters)), Times.Once);
        }

        [Fact]
        public async Task CreateModel_WithNullData_ShouldReturnBadRequest()
        {
            // Arrange
            CreateModelDto createDto = null!;

            // Act
            var result = await _controller.CreateModel(createDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Model data is required");

            _mockRepository.Verify(r => r.CreateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateModel_WithEmptyName_ShouldReturnBadRequest()
        {
            // Arrange
            var createDto = new CreateModelDto
            {
                Name = "",
                ModelSeriesId = 1,
                
                IsActive = true
            };

            // Act
            var result = await _controller.CreateModel(createDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Model name is required");

            _mockRepository.Verify(r => r.CreateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateModel_WithDuplicateName_ShouldReturnConflict()
        {
            // Arrange
            var createDto = new CreateModelDto
            {
                Name = "existing-model",
                ModelSeriesId = 1,
                
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
            var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
            conflictResult.Value.Should().Be("A model with name 'existing-model' already exists");

            _mockRepository.Verify(r => r.CreateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateModel_WhenRepositoryThrows_ShouldReturn500()
        {
            // Arrange
            var createDto = new CreateModelDto
            {
                Name = "test-model",
                ModelSeriesId = 1,

                IsActive = true
            };

            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.CreateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act
            var result = await _controller.CreateModel(createDto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            var errorResponse = objectResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("internal_error");
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

            var existingModel = new Model
            {
                Id = modelId,
                Name = "old-model-name",
                ModelSeriesId = 1,
                Series = series,
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
                IsActive = updateDto.IsActive ?? existingModel.IsActive,
                CreatedAt = existingModel.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(existingModel);
            _mockRepository.Setup(r => r.UpdateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(updatedModel);

            // Act
            var result = await _controller.UpdateModel(modelId, updateDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = okResult.Value.Should().BeOfType<ModelDto>().Subject;
            dto.Id.Should().Be(modelId);
            dto.Name.Should().Be("updated-model-name");
            dto.IsActive.Should().BeFalse();

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(modelId), Times.Once);
            _mockRepository.Verify(r => r.UpdateModelAsync(It.Is<Model>(m => 
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
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().Be($"Model with ID {modelId} not found");

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(modelId), Times.Once);
            _mockRepository.Verify(r => r.UpdateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateModel_WithModelParameters_ShouldUpdateParameters()
        {
            // Arrange
            var modelId = 1;
            var updateDto = new UpdateModelDto
            {
                ModelParameters = "{\"temperature\": {\"min\": 0, \"max\": 2}}"
            };

            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };

            var existingModel = new Model
            {
                Id = modelId,
                Name = "test-model",
                ModelSeriesId = 1,
                Series = series,
                ModelParameters = null, // No existing parameters
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var updatedModel = new Model
            {
                Id = modelId,
                Name = existingModel.Name,
                ModelSeriesId = 1,
                Series = series,
                ModelParameters = updateDto.ModelParameters,
                IsActive = existingModel.IsActive,
                CreatedAt = existingModel.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(existingModel);
            _mockRepository.Setup(r => r.UpdateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(updatedModel);

            // Act
            var result = await _controller.UpdateModel(modelId, updateDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = okResult.Value.Should().BeOfType<ModelDto>().Subject;
            dto.ModelParameters.Should().Be("{\"temperature\": {\"min\": 0, \"max\": 2}}");

            _mockRepository.Verify(r => r.UpdateModelAsync(It.Is<Model>(m => 
                m.ModelParameters == updateDto.ModelParameters)), Times.Once);
        }

        [Fact]
        public async Task UpdateModel_WithEmptyModelParameters_ShouldClearParameters()
        {
            // Arrange
            var modelId = 1;
            var updateDto = new UpdateModelDto
            {
                ModelParameters = "" // Empty string to clear parameters
            };

            var author = new ModelAuthor { Id = 1, Name = "Test Author" };
            var series = new ModelSeries { Id = 1, Name = "Test Series", Author = author };

            var existingModel = new Model
            {
                Id = modelId,
                Name = "test-model",
                ModelSeriesId = 1,
                Series = series,
                ModelParameters = "{\"temperature\": {\"min\": 0, \"max\": 1}}", // Has existing parameters
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var updatedModel = new Model
            {
                Id = modelId,
                Name = existingModel.Name,
                ModelSeriesId = 1,
                Series = series,
                ModelParameters = null, // Parameters cleared
                IsActive = existingModel.IsActive,
                CreatedAt = existingModel.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(existingModel);
            _mockRepository.Setup(r => r.UpdateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(updatedModel);

            // Act
            var result = await _controller.UpdateModel(modelId, updateDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = okResult.Value.Should().BeOfType<ModelDto>().Subject;
            dto.ModelParameters.Should().BeNull();

            _mockRepository.Verify(r => r.UpdateModelAsync(It.Is<Model>(m => 
                m.ModelParameters == null)), Times.Once);
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
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Update data is required");

            _mockRepository.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<int>()), Times.Never);
            _mockRepository.Verify(r => r.UpdateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            var errorResponse = objectResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("internal_error");

            _mockRepository.Verify(r => r.UpdateModelAsync(It.IsAny<Model>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            var errorResponse = objectResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("internal_error");
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

            var existingModel = new Model
            {
                Id = modelId,
                Name = "test-model",
                ModelSeriesId = 1,
                Series = series,
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
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().Be($"Model with ID {modelId} not found");

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
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            var errorResponse = objectResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("internal_error");
        }

        #endregion
    }
}