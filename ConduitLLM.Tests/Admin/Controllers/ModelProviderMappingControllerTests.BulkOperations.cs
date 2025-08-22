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
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedProviders = Assert.IsAssignableFrom<IEnumerable<Provider>>(okResult.Value);
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
                    ModelId = 1,
                    ProviderId = 1, 
                    ProviderModelId = "gpt-4"
                },
                new() 
                { 
                    ModelAlias = "model2", 
                    ModelId = 2,
                    ProviderId = 2, 
                    ProviderModelId = "claude-3"
                    // SupportsVision = true 
                }
            };

            var created = new List<ModelProviderMapping>
            {
                new() { Id = 1, ModelAlias = "model1", ProviderId = 1, ProviderModelId = "gpt-4", ModelId = 1 },
                new() { Id = 2, ModelAlias = "model2", ProviderId = 2, ProviderModelId = "claude-3", ModelId = 2 }
            };
            var errors = new List<string>();

            _mockService.Setup(x => x.CreateBulkMappingsAsync(It.IsAny<IEnumerable<ModelProviderMapping>>()))
                .ReturnsAsync((created, errors));

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(m => m.ToDto()).ToList());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedResponse = Assert.IsType<BulkMappingResult>(okResult.Value);
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
                new() { ModelAlias = "model1", ProviderId = 1, ProviderModelId = "gpt-4", ModelId = 1 },
                new() { ModelAlias = "duplicate", ProviderId = 1, ProviderModelId = "gpt-4", ModelId = 1 },
                new() { ModelAlias = "model3", ProviderId = 1, ProviderModelId = "model", ModelId = 1 }
            };

            var created = new List<ModelProviderMapping>
            {
                new() { Id = 1, ModelAlias = "model1", ProviderId = 1, ModelId = 1 }
            };
            var errors = new List<string>
            {
                "Model ID already exists",
                "Provider not found"
            };

            _mockService.Setup(x => x.CreateBulkMappingsAsync(It.IsAny<IEnumerable<ModelProviderMapping>>()))
                .ReturnsAsync((created, errors));

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(m => m.ToDto()).ToList());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedResponse = Assert.IsType<BulkMappingResult>(okResult.Value);
            returnedResponse.SuccessCount.Should().Be(1);
            returnedResponse.FailureCount.Should().Be(2);
        }

        [Fact]
        public async Task BulkCreateMappings_WithEmptyRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>();

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(m => m.ToDto()).ToList());

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(badRequestResult.Value);
            errorResponse.error.ToString().Should().Be("No mappings provided");
        }

        [Fact]
        public async Task BulkCreateMappings_WithExistingModels_ShouldReturnErrors()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new() { ModelAlias = "existing", ProviderId = 1, ProviderModelId = "gpt-4-updated", ModelId = 1 }
            };

            var created = new List<ModelProviderMapping>();
            var errors = new List<string> { "Model 'existing' already exists" };

            _mockService.Setup(x => x.CreateBulkMappingsAsync(It.IsAny<IEnumerable<ModelProviderMapping>>()))
                .ReturnsAsync((created, errors));

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(m => m.ToDto()).ToList());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedResponse = Assert.IsType<BulkMappingResult>(okResult.Value);
            returnedResponse.Errors.Should().HaveCount(1);
            returnedResponse.Created.Should().BeEmpty();
        }

        #endregion
    }
}