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
        private readonly Mock<IEventBus> _mockPublishEndpoint;
        private readonly Mock<ILogger<ModelController>> _mockLogger;
        private readonly ModelController _controller;

        public ModelControllerGetOperationsTests()
        {
            _mockRepository = new Mock<IModelRepository>();
            _mockMappingService = new Mock<IAdminModelProviderMappingService>();
            _mockProviderRepository = new Mock<IProviderRepository>();
            _mockPublishEndpoint = new Mock<IEventBus>();
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

            _mockRepository.Setup(r => r.GetPaginatedWithFilterAsync(null, null, null, null, null))
                .ReturnsAsync((models, models.Count));

            // Act — no pagination params returns flat array
            var result = await _controller.GetAllModels();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelDto>>().Subject;
            dtos.Should().HaveCount(2);

            var firstDto = dtos.First();
            firstDto.Id.Should().Be(1);
            firstDto.Name.Should().Be("test-model-1");
            firstDto.IsActive.Should().BeTrue();
            firstDto.Id.Should().BePositive();

            _mockRepository.Verify(r => r.GetPaginatedWithFilterAsync(null, null, null, null, null), Times.Once);
        }

        [Fact]
        public async Task GetAllModels_WithEmptyList_ShouldReturnOkWithEmptyList()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetPaginatedWithFilterAsync(null, null, null, null, null))
                .ReturnsAsync((new List<Model>(), 0));

            // Act
            var result = await _controller.GetAllModels();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelDto>>().Subject;
            dtos.Should().BeEmpty();

            _mockRepository.Verify(r => r.GetPaginatedWithFilterAsync(null, null, null, null, null), Times.Once);
        }

        [Fact]
        public async Task GetAllModels_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.GetPaginatedWithFilterAsync(null, null, null, null, null))
                .ThrowsAsync(exception);

            // Act & Assert — error→HTTP mapping now happens in AdminExceptionMiddleware
            var act = async () => await _controller.GetAllModels();
            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task GetAllModels_WithFilters_ShouldReturnFlatFilteredList()
        {
            var models = new List<Model>
            {
                new() { Id = 7, Name = "vision-model", ModelSeriesId = 1 }
            };
            _mockRepository
                .Setup(r => r.GetPaginatedWithFilterAsync(null, null, "vision", "vision", true))
                .ReturnsAsync((models, 1));

            var result = await _controller.GetAllModels("vision", "vision", true);

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dtos = okResult.Value.Should().BeOfType<List<ModelDto>>().Subject;
            dtos.Should().ContainSingle().Which.Name.Should().Be("vision-model");
            _mockRepository.Verify(
                r => r.GetPaginatedWithFilterAsync(null, null, "vision", "vision", true),
                Times.Once);
        }

        [Fact]
        public async Task GetPagedModels_WithFilters_ShouldReturnTypedPagedResult()
        {
            var models = new List<Model>
            {
                new() { Id = 7, Name = "vision-model", ModelSeriesId = 1 }
            };
            _mockRepository
                .Setup(r => r.GetPaginatedWithFilterAsync(2, 25, "vision", "vision", true))
                .ReturnsAsync((models, 51));

            var result = await _controller.GetPagedModels(2, 25, "vision", "vision", true);

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var page = okResult.Value.Should().BeOfType<PagedResult<ModelDto>>().Subject;
            page.Items.Should().ContainSingle().Which.Name.Should().Be("vision-model");
            page.TotalCount.Should().Be(51);
            page.CurrentPage.Should().Be(2);
            page.PageSize.Should().Be(25);
            page.TotalPages.Should().Be(3);
        }

        [Theory]
        [InlineData(0, 500, 1, 100)]
        [InlineData(-10, 0, 1, 50)]
        public async Task GetPagedModels_ShouldClampPagination(
            int requestedPage,
            int requestedPageSize,
            int expectedPage,
            int expectedPageSize)
        {
            _mockRepository
                .Setup(r => r.GetPaginatedWithFilterAsync(expectedPage, expectedPageSize, null, null, null))
                .ReturnsAsync((new List<Model>(), 0));

            var result = await _controller.GetPagedModels(requestedPage, requestedPageSize);

            var page = result.Should().BeOfType<OkObjectResult>().Subject.Value
                .Should().BeOfType<PagedResult<ModelDto>>().Subject;
            page.CurrentPage.Should().Be(expectedPage);
            page.PageSize.Should().Be(expectedPageSize);
            page.TotalPages.Should().Be(0);
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
        public async Task GetModelById_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var modelId = 1;
            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ThrowsAsync(exception);

            // Act & Assert — error→HTTP mapping now happens in AdminExceptionMiddleware
            var act = async () => await _controller.GetModelById(modelId);
            await act.Should().ThrowAsync<Exception>();
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
            var identifiers = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelIdentifierDto>>().Subject;
            identifiers.Should().HaveCount(3);

            var identifierDtos = identifiers.ToList();
            identifierDtos[0].Id.Should().Be(1);
            identifierDtos[0].Identifier.Should().Be("openai/gpt-oss-120b");
            identifierDtos[0].Provider.Should().Be((int)ProviderType.Groq);
            identifierDtos[0].IsPrimary.Should().BeTrue();
            identifierDtos[1].Provider.Should().Be((int)ProviderType.Fireworks);
            identifierDtos[2].Provider.Should().Be((int)ProviderType.Cerebras);
            identifierDtos[2].IsPrimary.Should().BeFalse();

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
            var identifiers = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelIdentifierDto>>().Subject;
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
        public async Task GetModelIdentifiers_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var modelId = 1;
            var exception = new Exception("Database connection failed");
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ThrowsAsync(exception);

            // Act & Assert — error→HTTP mapping now happens in AdminExceptionMiddleware
            var act = async () => await _controller.GetModelIdentifiers(modelId);
            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task GetAvailableProviders_ShouldReturnTypedAssociationsWithNumericProvider()
        {
            var modelId = 7;
            var model = new Model
            {
                Id = modelId,
                Name = "typed-model",
                ModelSeriesId = 1,
                Identifiers = new List<ModelProviderTypeAssociation>
                {
                    new()
                    {
                        Id = 11,
                        ModelId = modelId,
                        Identifier = "typed-model/provider",
                        Provider = ProviderType.Groq,
                        ProviderVariation = "fast",
                        IsPrimary = true
                    },
                    new()
                    {
                        Id = 12,
                        ModelId = modelId,
                        Identifier = "unassigned",
                        Provider = null
                    }
                }
            };
            var providers = new List<Provider>
            {
                new() { Id = 21, ProviderName = "Groq Production", ProviderType = ProviderType.Groq, IsEnabled = true },
                new() { Id = 22, ProviderName = "Groq Disabled", ProviderType = ProviderType.Groq, IsEnabled = false }
            };
            _mockRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId)).ReturnsAsync(model);
            _mockProviderRepository
                .Setup(r => r.GetPaginatedAsync(1, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync((providers, providers.Count));

            var result = await _controller.GetAvailableProviders(modelId);

            var associations = result.Should().BeOfType<OkObjectResult>().Subject.Value
                .Should().BeOfType<List<ModelProviderAvailabilityDto>>().Subject;
            var association = associations.Should().ContainSingle().Subject;
            association.AssociationId.Should().Be(11);
            association.Provider.Should().Be((int)ProviderType.Groq);
            association.ProviderVariation.Should().Be("fast");
            association.AvailableProviders.Should().ContainSingle().Which.Should().BeEquivalentTo(
                new AvailableProviderDto
                {
                    ProviderId = 21,
                    ProviderName = "Groq Production",
                    ProviderType = nameof(ProviderType.Groq)
                });
        }

        #endregion
    }
}
