using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public class ModelControllerIntegrationTests
    {
        private readonly Mock<IModelRepository> _mockModelRepository;
        private readonly Mock<IAdminModelProviderMappingService> _mockMappingService;
        private readonly Mock<IProviderRepository> _mockProviderRepository;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<ModelController>> _mockLogger;
        private readonly ModelController _controller;

        public ModelControllerIntegrationTests()
        {
            _mockModelRepository = new Mock<IModelRepository>();
            _mockMappingService = new Mock<IAdminModelProviderMappingService>();
            _mockProviderRepository = new Mock<IProviderRepository>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<ModelController>>();

            _controller = new ModelController(
                _mockModelRepository.Object,
                _mockMappingService.Object,
                _mockProviderRepository.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task UpdateModel_WithParameterChange_PublishesEventWithParametersChangedFlag()
        {
            // Arrange
            var modelId = 1;
            var existingModel = new Model
            {
                Id = modelId,
                Name = "kling-v2.1",
                ModelSeriesId = 1,
                IsActive = true,
                ModelParameters = null // Initially null
            };

            var updatedModel = new Model
            {
                Id = modelId,
                Name = "kling-v2.1",
                ModelSeriesId = 1,
                IsActive = true,
                ModelParameters = "{\"prompt\":{\"type\":\"string\"},\"duration\":5}"
            };

            var updateDto = new UpdateModelDto
            {
                ModelParameters = "{\"prompt\":{\"type\":\"string\"},\"duration\":5}"
            };

            _mockModelRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(existingModel);
            _mockModelRepository.Setup(r => r.UpdateModelAsync(It.IsAny<Model>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(updatedModel);

            ModelUpdated? capturedEvent = null;
            _mockPublishEndpoint.Setup(p => p.Publish(It.IsAny<ModelUpdated>(), default))
                .Callback<object, System.Threading.CancellationToken>((evt, _) => capturedEvent = evt as ModelUpdated)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateModel(modelId, updateDto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();

            // Verify the event was published
            _mockPublishEndpoint.Verify(p => p.Publish(It.IsAny<ModelUpdated>(), default), Times.Once);

            // Verify the event has correct properties
            Assert.NotNull(capturedEvent);
            Assert.Equal(modelId, capturedEvent.ModelId);
            Assert.Equal("kling-v2.1", capturedEvent.ModelName);
            Assert.Equal(1, capturedEvent.ModelSeriesId);
            Assert.Equal("Updated", capturedEvent.ChangeType);
            Assert.True(capturedEvent.ParametersChanged);
            Assert.Contains("ModelParameters", capturedEvent.ChangedProperties);
        }

        [Fact]
        public async Task UpdateModel_WithoutParameterChange_PublishesEventWithParametersChangedFalse()
        {
            // Arrange
            var modelId = 1;
            var existingModel = new Model
            {
                Id = modelId,
                Name = "kling-v2.1",
                ModelSeriesId = 1,
                IsActive = true,
                ModelParameters = "{\"prompt\":{\"type\":\"string\"}}"
            };

            var updatedModel = new Model
            {
                Id = modelId,
                Name = "kling-v2.1",
                ModelSeriesId = 1,
                IsActive = false, // Only changing IsActive
                ModelParameters = "{\"prompt\":{\"type\":\"string\"}}"
            };

            var updateDto = new UpdateModelDto
            {
                IsActive = false // Only updating IsActive, not parameters
            };

            _mockModelRepository.Setup(r => r.GetByIdWithDetailsAsync(modelId))
                .ReturnsAsync(existingModel);
            _mockModelRepository.Setup(r => r.UpdateModelAsync(It.IsAny<Model>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(updatedModel);

            ModelUpdated? capturedEvent = null;
            _mockPublishEndpoint.Setup(p => p.Publish(It.IsAny<ModelUpdated>(), default))
                .Callback<object, System.Threading.CancellationToken>((evt, _) => capturedEvent = evt as ModelUpdated)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateModel(modelId, updateDto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();

            // Verify the event was published
            _mockPublishEndpoint.Verify(p => p.Publish(It.IsAny<ModelUpdated>(), default), Times.Once);

            // Verify the event has correct properties
            Assert.NotNull(capturedEvent);
            Assert.Equal(modelId, capturedEvent.ModelId);
            Assert.False(capturedEvent.ParametersChanged); // Should be false
            Assert.Contains("IsActive", capturedEvent.ChangedProperties);
            Assert.DoesNotContain("ModelParameters", capturedEvent.ChangedProperties);
        }
    }
}