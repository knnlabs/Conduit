using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.WebUI.Adapters
{
    public class ModelProviderMappingServiceAdapterTests
    {
        private readonly Mock<IAdminApiClient> _adminApiClientMock;
        private readonly Mock<ILogger<ModelProviderMappingServiceAdapter>> _loggerMock;
        private readonly ModelProviderMappingServiceAdapter _adapter;

        public ModelProviderMappingServiceAdapterTests()
        {
            _adminApiClientMock = new Mock<IAdminApiClient>();
            _loggerMock = new Mock<ILogger<ModelProviderMappingServiceAdapter>>();
            _adapter = new ModelProviderMappingServiceAdapter(_adminApiClientMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetAllAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedMappings = new List<ModelProviderMappingDto>
            {
                new ModelProviderMappingDto { Id = 1, ModelId = "gpt-4", ProviderModelId = "gpt-4", ProviderId = "1" },
                new ModelProviderMappingDto { Id = 2, ModelId = "claude-3-opus", ProviderModelId = "claude-3-opus-20240229", ProviderId = "2" }
            };

            _adminApiClientMock.Setup(c => c.GetAllModelProviderMappingsAsync())
                .ReturnsAsync(expectedMappings);

            // Act
            var result = await _adapter.GetAllAsync();

            // Assert
            Assert.Equal(expectedMappings.Count(), result.Count());
            Assert.Equal(expectedMappings, result);
            _adminApiClientMock.Verify(c => c.GetAllModelProviderMappingsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsEmptyListWhenEmptyResult()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetAllModelProviderMappingsAsync())
                .ReturnsAsync(Enumerable.Empty<ModelProviderMappingDto>());

            // Act
            var result = await _adapter.GetAllAsync();

            // Assert
            Assert.Empty(result);
            _adminApiClientMock.Verify(c => c.GetAllModelProviderMappingsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedMapping = new ModelProviderMappingDto
            {
                Id = 1,
                ModelId = "gpt-4",
                ProviderModelId = "gpt-4",
                ProviderId = "1",
                IsEnabled = true
            };

            _adminApiClientMock.Setup(c => c.GetModelProviderMappingByIdAsync(1))
                .ReturnsAsync(expectedMapping);

            // Act
            var result = await _adapter.GetByIdAsync(1);

            // Assert
            Assert.Same(expectedMapping, result);
            _adminApiClientMock.Verify(c => c.GetModelProviderMappingByIdAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetByModelIdAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var modelId = "gpt-4";
            var expectedMapping = new ModelProviderMappingDto
            {
                Id = 1,
                ModelId = modelId,
                ProviderModelId = "gpt-4",
                ProviderId = "1",
                IsEnabled = true
            };

            _adminApiClientMock.Setup(c => c.GetModelProviderMappingByAliasAsync(modelId))
                .ReturnsAsync(expectedMapping);

            // Act
            var result = await _adapter.GetByModelIdAsync(modelId);

            // Assert
            Assert.Same(expectedMapping, result);
            _adminApiClientMock.Verify(c => c.GetModelProviderMappingByAliasAsync(modelId), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var mappingDto = new ModelProviderMappingDto
            {
                ModelId = "gpt-4",
                ProviderModelId = "gpt-4",
                ProviderId = "1",
                IsEnabled = true,
                MaxContextLength = 8192
            };

            _adminApiClientMock.Setup(c => c.CreateModelProviderMappingAsync(It.IsAny<ModelProviderMapping>()))
                .ReturnsAsync(true);

            _adminApiClientMock.Setup(c => c.GetModelProviderMappingByAliasAsync(mappingDto.ModelId))
                .ReturnsAsync(mappingDto);

            // Act
            var result = await _adapter.CreateAsync(mappingDto);

            // Assert
            Assert.Same(mappingDto, result);
            _adminApiClientMock.Verify(c => c.CreateModelProviderMappingAsync(It.Is<ModelProviderMapping>(m => 
                m.ModelAlias == mappingDto.ModelId && 
                m.ProviderModelName == mappingDto.ProviderModelId &&
                m.ProviderCredentialId == 1 &&
                m.IsEnabled == mappingDto.IsEnabled &&
                m.MaxContextTokens == mappingDto.MaxContextLength)), Times.Once);
            _adminApiClientMock.Verify(c => c.GetModelProviderMappingByAliasAsync(mappingDto.ModelId), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ReturnsNullWhenCreationFails()
        {
            // Arrange
            var mappingDto = new ModelProviderMappingDto
            {
                ModelId = "gpt-4",
                ProviderModelId = "gpt-4",
                ProviderId = "1",
                IsEnabled = true
            };

            _adminApiClientMock.Setup(c => c.CreateModelProviderMappingAsync(It.IsAny<ModelProviderMapping>()))
                .ReturnsAsync(false);

            // Act
            var result = await _adapter.CreateAsync(mappingDto);

            // Assert
            Assert.Null(result);
            _adminApiClientMock.Verify(c => c.CreateModelProviderMappingAsync(It.IsAny<ModelProviderMapping>()), Times.Once);
            _adminApiClientMock.Verify(c => c.GetModelProviderMappingByAliasAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var mappingDto = new ModelProviderMappingDto
            {
                Id = 1,
                ModelId = "gpt-4",
                ProviderModelId = "gpt-4",
                ProviderId = "1",
                IsEnabled = true,
                MaxContextLength = 8192
            };

            _adminApiClientMock.Setup(c => c.UpdateModelProviderMappingAsync(mappingDto.Id, It.IsAny<ModelProviderMapping>()))
                .ReturnsAsync(true);

            // Act
            var result = await _adapter.UpdateAsync(mappingDto);

            // Assert
            Assert.Equal(mappingDto, result);
            _adminApiClientMock.Verify(c => c.UpdateModelProviderMappingAsync(mappingDto.Id, It.Is<ModelProviderMapping>(m => 
                m.Id == mappingDto.Id &&
                m.ModelAlias == mappingDto.ModelId && 
                m.ProviderModelName == mappingDto.ProviderModelId &&
                m.ProviderCredentialId == 1 &&
                m.IsEnabled == mappingDto.IsEnabled &&
                m.MaxContextTokens == mappingDto.MaxContextLength)), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ReturnsNullWhenUpdateFails()
        {
            // Arrange
            var mappingDto = new ModelProviderMappingDto
            {
                Id = 1,
                ModelId = "gpt-4",
                ProviderModelId = "gpt-4",
                ProviderId = "1",
                IsEnabled = true
            };

            _adminApiClientMock.Setup(c => c.UpdateModelProviderMappingAsync(mappingDto.Id, It.IsAny<ModelProviderMapping>()))
                .ReturnsAsync(false);

            // Act
            var result = await _adapter.UpdateAsync(mappingDto);

            // Assert
            Assert.Null(result);
            _adminApiClientMock.Verify(c => c.UpdateModelProviderMappingAsync(mappingDto.Id, It.IsAny<ModelProviderMapping>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.DeleteModelProviderMappingAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _adapter.DeleteAsync(1);

            // Assert
            Assert.True(result);
            _adminApiClientMock.Verify(c => c.DeleteModelProviderMappingAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetProvidersAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var credentials = new List<ProviderCredentialDto>
            {
                new ProviderCredentialDto { Id = 1, ProviderName = "OpenAI" },
                new ProviderCredentialDto { Id = 2, ProviderName = "Anthropic" }
            };

            var expectedProviders = credentials.Select(c => new ProviderDataDto
            {
                Id = c.Id,
                ProviderName = c.ProviderName
            }).ToList();

            _adminApiClientMock.Setup(c => c.GetAllProviderCredentialsAsync())
                .ReturnsAsync(credentials);

            // Act
            var result = await _adapter.GetProvidersAsync();

            // Assert
            var resultList = result.ToList();
            Assert.Equal(expectedProviders.Count, resultList.Count);
            for (int i = 0; i < expectedProviders.Count; i++)
            {
                Assert.Equal(expectedProviders[i].Id, resultList[i].Id);
                Assert.Equal(expectedProviders[i].ProviderName, resultList[i].ProviderName);
            }
            _adminApiClientMock.Verify(c => c.GetAllProviderCredentialsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_HandlesException()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetAllModelProviderMappingsAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _adapter.GetAllAsync());
            _adminApiClientMock.Verify(c => c.GetAllModelProviderMappingsAsync(), Times.Once);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }
    }
}