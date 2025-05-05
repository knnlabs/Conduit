using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Controllers
{
    /// <summary>
    /// Unit tests for the ModelProviderMappingController.
    /// </summary>
    public class ModelProviderMappingControllerTests
    {
        private readonly Mock<IModelProviderMappingService> _mockMappingService;
        private readonly Mock<IProviderCredentialRepository> _mockCredentialRepository;
        private readonly Mock<ILogger<ModelProviderMappingController>> _mockLogger;
        private readonly ModelProviderMappingController _controller;

        public ModelProviderMappingControllerTests()
        {
            _mockMappingService = new Mock<IModelProviderMappingService>();
            _mockCredentialRepository = new Mock<IProviderCredentialRepository>();
            _mockLogger = new Mock<ILogger<ModelProviderMappingController>>();
            
            _controller = new ModelProviderMappingController(
                _mockMappingService.Object,
                _mockCredentialRepository.Object,
                _mockLogger.Object
            );
        }

        /// <summary>
        /// Test that GetAllMappings returns OK with mappings list.
        /// </summary>
        [Fact]
        public async Task GetAllMappings_ReturnsOkWithMappings()
        {
            // Arrange
            var mappings = new List<ConduitLLM.Configuration.ModelProviderMapping>
            {
                new ConduitLLM.Configuration.ModelProviderMapping { ModelAlias = "gpt-4", ProviderName = "openai", ProviderModelId = "gpt-4-turbo-preview" },
                new ConduitLLM.Configuration.ModelProviderMapping { ModelAlias = "claude-3", ProviderName = "anthropic", ProviderModelId = "claude-3-opus-20240229" }
            };
            
            _mockMappingService.Setup(s => s.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            // Act
            var result = await _controller.GetAllMappings();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedMappings = Assert.IsAssignableFrom<List<ConduitLLM.Configuration.ModelProviderMapping>>(okResult.Value);
            Assert.Equal(2, returnedMappings.Count);
        }

        /// <summary>
        /// Test that GetAllMappings returns 500 when service throws exception.
        /// </summary>
        [Fact]
        public async Task GetAllMappings_Returns500WhenExceptionOccurs()
        {
            // Arrange
            _mockMappingService.Setup(s => s.GetAllMappingsAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.GetAllMappings();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        /// <summary>
        /// Test that GetMappingById returns OK with mapping.
        /// </summary>
        [Fact]
        public async Task GetMappingById_ReturnsOkWithMapping()
        {
            // Arrange
            int id = 1;
            var mapping = new ConduitLLM.Configuration.ModelProviderMapping 
            { 
                ModelAlias = "gpt-4", 
                ProviderName = "openai", 
                ProviderModelId = "gpt-4-turbo-preview" 
            };
            
            _mockMappingService.Setup(s => s.GetMappingByIdAsync(id))
                .ReturnsAsync(mapping);

            // Act
            var result = await _controller.GetMappingById(id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedMapping = Assert.IsType<ConduitLLM.Configuration.ModelProviderMapping>(okResult.Value);
            Assert.Equal("gpt-4", returnedMapping.ModelAlias);
        }

        /// <summary>
        /// Test that GetMappingById returns 404 when mapping not found.
        /// </summary>
        [Fact]
        public async Task GetMappingById_Returns404WhenMappingNotFound()
        {
            // Arrange
            int id = 1;
            _mockMappingService.Setup(s => s.GetMappingByIdAsync(id))
                .ReturnsAsync((ConduitLLM.Configuration.ModelProviderMapping?)null);

            // Act
            var result = await _controller.GetMappingById(id);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        /// <summary>
        /// Test that GetMappingByAlias returns OK with mapping.
        /// </summary>
        [Fact]
        public async Task GetMappingByAlias_ReturnsOkWithMapping()
        {
            // Arrange
            string alias = "gpt-4";
            var mapping = new ConduitLLM.Configuration.ModelProviderMapping 
            { 
                ModelAlias = "gpt-4", 
                ProviderName = "openai", 
                ProviderModelId = "gpt-4-turbo-preview" 
            };
            
            _mockMappingService.Setup(s => s.GetMappingByModelAliasAsync(alias))
                .ReturnsAsync(mapping);

            // Act
            var result = await _controller.GetMappingByAlias(alias);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedMapping = Assert.IsType<ConduitLLM.Configuration.ModelProviderMapping>(okResult.Value);
            Assert.Equal("gpt-4", returnedMapping.ModelAlias);
        }

        /// <summary>
        /// Test that CreateMapping returns Created when successful.
        /// </summary>
        [Fact]
        public async Task CreateMapping_ReturnsCreatedWithMapping()
        {
            // Arrange
            var mapping = new ConduitLLM.Configuration.ModelProviderMapping 
            { 
                ModelAlias = "gpt-4", 
                ProviderName = "openai", 
                ProviderModelId = "gpt-4-turbo-preview" 
            };
            
            var credential = new ProviderCredential
            {
                Id = 1,
                ProviderName = "openai"
            };
            
            // First GetByProviderNameAsync call - in the validation code
            _mockCredentialRepository.Setup(r => r.GetByProviderNameAsync("openai", It.IsAny<CancellationToken>()))
                .ReturnsAsync(credential);
                
            // First GetMappingByModelAliasAsync call - checking if mapping already exists
            _mockMappingService.Setup(s => s.GetMappingByModelAliasAsync("gpt-4"))
                .ReturnsAsync((ConduitLLM.Configuration.ModelProviderMapping?)null);
                
            // AddMappingAsync call
            _mockMappingService.Setup(s => s.AddMappingAsync(It.IsAny<ConduitLLM.Configuration.ModelProviderMapping>()))
                .Returns(Task.CompletedTask);
                
            // Second GetMappingByModelAliasAsync call - after creation
            // This needs to use a different setup to handle sequential calls
            var callCount = 0;
            _mockMappingService.Setup(s => s.GetMappingByModelAliasAsync("gpt-4"))
                .Returns(() => {
                    callCount++;
                    return callCount == 1 
                        ? Task.FromResult<ConduitLLM.Configuration.ModelProviderMapping?>(null) 
                        : Task.FromResult<ConduitLLM.Configuration.ModelProviderMapping?>(mapping);
                })
                .Verifiable();

            // Act
            var result = await _controller.CreateMapping(mapping);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(201, createdResult.StatusCode);
            var returnedMapping = Assert.IsType<ConduitLLM.Configuration.ModelProviderMapping>(createdResult.Value);
            Assert.Equal("gpt-4", returnedMapping.ModelAlias);
            
            // Verify the method was called
            _mockMappingService.Verify(s => s.GetMappingByModelAliasAsync("gpt-4"), Times.AtLeast(2));
        }

        /// <summary>
        /// Test that CreateMapping returns BadRequest when provider not found.
        /// </summary>
        [Fact]
        public async Task CreateMapping_ReturnsBadRequestWhenProviderNotFound()
        {
            // Arrange
            var mapping = new ConduitLLM.Configuration.ModelProviderMapping 
            { 
                ModelAlias = "gpt-4", 
                ProviderName = "openai", 
                ProviderModelId = "gpt-4-turbo-preview" 
            };
            
            _mockCredentialRepository.Setup(r => r.GetByProviderNameAsync("openai", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProviderCredential?)null);

            // Act
            var result = await _controller.CreateMapping(mapping);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        /// <summary>
        /// Test that CreateMapping returns BadRequest when mapping with alias already exists.
        /// </summary>
        [Fact]
        public async Task CreateMapping_ReturnsBadRequestWhenMappingWithAliasAlreadyExists()
        {
            // Arrange
            var mapping = new ConduitLLM.Configuration.ModelProviderMapping 
            { 
                ModelAlias = "gpt-4", 
                ProviderName = "openai", 
                ProviderModelId = "gpt-4-turbo-preview" 
            };
            
            var credential = new ProviderCredential
            {
                Id = 1,
                ProviderName = "openai"
            };
            
            _mockCredentialRepository.Setup(r => r.GetByProviderNameAsync("openai", It.IsAny<CancellationToken>()))
                .ReturnsAsync(credential);
                
            _mockMappingService.Setup(s => s.GetMappingByModelAliasAsync("gpt-4"))
                .ReturnsAsync(mapping); // Existing mapping with same alias

            // Act
            var result = await _controller.CreateMapping(mapping);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        /// <summary>
        /// Test that UpdateMapping returns NoContent when successful.
        /// </summary>
        [Fact]
        public async Task UpdateMapping_ReturnsNoContentWhenSuccessful()
        {
            // Arrange
            int id = 1;
            var mapping = new ConduitLLM.Configuration.ModelProviderMapping 
            { 
                ModelAlias = "gpt-4", 
                ProviderName = "openai", 
                ProviderModelId = "gpt-4-turbo-preview" 
            };
            
            var credential = new ProviderCredential
            {
                Id = 1,
                ProviderName = "openai"
            };
            
            _mockMappingService.Setup(s => s.GetMappingByIdAsync(id))
                .ReturnsAsync(mapping);
                
            _mockCredentialRepository.Setup(r => r.GetByProviderNameAsync("openai", It.IsAny<CancellationToken>()))
                .ReturnsAsync(credential);
                
            _mockMappingService.Setup(s => s.UpdateMappingAsync(It.IsAny<ConduitLLM.Configuration.ModelProviderMapping>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateMapping(id, mapping);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        /// <summary>
        /// Test that DeleteMapping returns NoContent when successful.
        /// </summary>
        [Fact]
        public async Task DeleteMapping_ReturnsNoContentWhenSuccessful()
        {
            // Arrange
            int id = 1;
            var mapping = new ConduitLLM.Configuration.ModelProviderMapping 
            { 
                ModelAlias = "gpt-4", 
                ProviderName = "openai", 
                ProviderModelId = "gpt-4-turbo-preview" 
            };
            
            _mockMappingService.Setup(s => s.GetMappingByIdAsync(id))
                .ReturnsAsync(mapping);
                
            _mockMappingService.Setup(s => s.DeleteMappingAsync(id))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteMapping(id);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        /// <summary>
        /// Test that GetProviders returns OK with provider list.
        /// </summary>
        [Fact]
        public async Task GetProviders_ReturnsOkWithProviders()
        {
            // Arrange
            var providers = new List<ProviderCredential>
            {
                new ProviderCredential { Id = 1, ProviderName = "openai" },
                new ProviderCredential { Id = 2, ProviderName = "anthropic" }
            };
            
            _mockCredentialRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(providers);

            // Act
            var result = await _controller.GetProviders();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedProviders = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);
            Assert.Equal(2, returnedProviders.Count());
        }
    }
}