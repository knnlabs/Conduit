using ConduitLLM.Admin.Controllers;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Bulk operations and provider tests for ModelProviderMappingControllerTests
    /// </summary>
    public partial class ModelProviderMappingControllerTests
    {
        #region GetProvidersAsync Tests

        [Fact]
        public async Task GetProviders_ShouldReturnProviderList()
        {
            // Arrange
            var providers = new List<Provider>
            {
                new() { Id = 1, ProviderType = ProviderType.OpenAI },
                new() { Id = 2, ProviderType = ProviderType.Groq },
                new() { Id = 3, ProviderType = ProviderType.MiniMax }
            };

            _mockService.Setup(x => x.GetProvidersAsync())
                .ReturnsAsync(providers);

            // Act
            var result = await _controller.GetProviders();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProviders = okResult.Value.Should().BeAssignableTo<IEnumerable<Provider>>().Subject;
            returnedProviders.Should().HaveCount(3);
        }

        #endregion

        #region BulkCreateMappings Tests

        [Fact]
        public async Task BulkCreateMappings_WithValidMappings_ShouldReturnSuccess()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new() 
                { 
                    ModelAlias = "model1", 
                    ModelProviderTypeAssociationId = 1,
                    ProviderId = 1, 
                    ProviderModelId = "gpt-4"
                },
                new() 
                { 
                    ModelAlias = "model2", 
                    ModelProviderTypeAssociationId = 2,
                    ProviderId = 2, 
                    ProviderModelId = "claude-3"
                }
            };

            var created = new List<ModelProviderMapping>
            {
                new() { Id = 1, ModelAlias = "model1", ProviderId = 1, ProviderModelId = "gpt-4", ModelProviderTypeAssociationId = 1 },
                new() { Id = 2, ModelAlias = "model2", ProviderId = 2, ProviderModelId = "claude-3", ModelProviderTypeAssociationId = 2 }
            };
            var errors = new List<string>();

            _mockService.Setup(x => x.CreateBulkMappingsAsync(It.IsAny<IEnumerable<ModelProviderMapping>>()))
                .ReturnsAsync((created, errors));

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(ToCreateRequest).ToList());

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedResponse = okResult.Value.Should().BeOfType<BulkMappingResult>().Subject;
            returnedResponse.TotalProcessed.Should().Be(2);
            returnedResponse.Created.Should().HaveCount(2);
            returnedResponse.SuccessCount.Should().Be(2);
        }

        [Fact]
        public async Task BulkCreateMappings_WithSomeFailures_ShouldReturnPartialSuccess()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new() { ModelAlias = "model1", ProviderId = 1, ProviderModelId = "gpt-4", ModelProviderTypeAssociationId = 1 },
                new() { ModelAlias = "duplicate", ProviderId = 1, ProviderModelId = "gpt-4", ModelProviderTypeAssociationId = 1 },
                new() { ModelAlias = "model3", ProviderId = 1, ProviderModelId = "model", ModelProviderTypeAssociationId = 1 }
            };

            var created = new List<ModelProviderMapping>
            {
                new() { Id = 1, ModelAlias = "model1", ProviderId = 1, ModelProviderTypeAssociationId = 1 }
            };
            var errors = new List<string>
            {
                "Model Provider Type Association ID already exists",
                "Provider not found"
            };

            _mockService.Setup(x => x.CreateBulkMappingsAsync(It.IsAny<IEnumerable<ModelProviderMapping>>()))
                .ReturnsAsync((created, errors));

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(ToCreateRequest).ToList());

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedResponse = okResult.Value.Should().BeOfType<BulkMappingResult>().Subject;
            returnedResponse.SuccessCount.Should().Be(1);
            returnedResponse.FailureCount.Should().Be(2);
        }

        [Fact]
        public async Task BulkCreateMappings_WithEmptyRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>();

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(ToCreateRequest).ToList());

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.error.ToString().Should().Be("No mappings provided");
        }

        [Fact]
        public async Task BulkCreateMappings_WithExistingModels_ShouldReturnErrors()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new() { ModelAlias = "existing", ProviderId = 1, ProviderModelId = "gpt-4-updated", ModelProviderTypeAssociationId = 1 }
            };

            var created = new List<ModelProviderMapping>();
            var errors = new List<string> { "Model 'existing' already exists" };

            _mockService.Setup(x => x.CreateBulkMappingsAsync(It.IsAny<IEnumerable<ModelProviderMapping>>()))
                .ReturnsAsync((created, errors));

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(ToCreateRequest).ToList());

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedResponse = okResult.Value.Should().BeOfType<BulkMappingResult>().Subject;
            returnedResponse.Errors.Should().HaveCount(1);
            returnedResponse.Created.Should().BeEmpty();
        }

        #endregion
    }
}
