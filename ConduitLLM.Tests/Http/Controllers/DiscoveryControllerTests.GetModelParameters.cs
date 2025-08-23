using System.Security.Claims;
using System.Text.Json;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class DiscoveryControllerGetModelParametersTests : ControllerTestBase
    {
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _dbContextFactoryMock;
        private readonly Mock<IModelCapabilityService> _modelCapabilityServiceMock;
        private readonly Mock<IVirtualKeyService> _virtualKeyServiceMock;
        private readonly Mock<ILogger<DiscoveryController>> _loggerMock;
        private readonly DiscoveryController _controller;
        private readonly ConduitDbContext _context;

        public DiscoveryControllerGetModelParametersTests(ITestOutputHelper output) : base(output)
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: $"DiscoveryTest_{Guid.NewGuid()}")
                .Options;
            _context = new ConduitDbContext(options);

            // Setup mocks
            _dbContextFactoryMock = new Mock<IDbContextFactory<ConduitDbContext>>();
            _dbContextFactoryMock.Setup(x => x.CreateDbContextAsync(default(CancellationToken)))
                .ReturnsAsync(_context);

            _modelCapabilityServiceMock = new Mock<IModelCapabilityService>();
            _virtualKeyServiceMock = new Mock<IVirtualKeyService>();
            _loggerMock = new Mock<ILogger<DiscoveryController>>();

            // Create controller
            _controller = new DiscoveryController(
                _dbContextFactoryMock.Object,
                _modelCapabilityServiceMock.Object,
                _virtualKeyServiceMock.Object,
                _loggerMock.Object);

            // Setup HTTP context with claims
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("VirtualKey", "test-virtual-key")
            }));
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task GetModelParameters_WithValidModelAlias_ReturnsParameters()
        {
            // Arrange
            var virtualKey = new VirtualKey { KeyName = "test-virtual-key", IsEnabled = true };
            _virtualKeyServiceMock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.FromResult<VirtualKey?>(virtualKey));

            var author = new ModelAuthor { Id = 1, Name = "TestAuthor" };
            var series = new ModelSeries 
            { 
                Id = 1, 
                Name = "Test Series",
                AuthorId = 1,
                Author = author,
                Parameters = "{\"temperature\":{\"type\":\"slider\",\"min\":0,\"max\":2,\"default\":1}}"
            };
            var capabilities = new ModelCapabilities { Id = 1, MaxTokens = 4096 };
            var model = new Model 
            { 
                Id = 1, 
                Name = "TestModel",
                ModelSeriesId = 1,
                Series = series,
                ModelCapabilitiesId = 1,
                Capabilities = capabilities
            };
            var provider = new Provider { Id = 1, ProviderType = ProviderType.OpenAI, IsEnabled = true };
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = "test-model",
                ProviderModelId = "test-model-v1",
                ProviderId = 1,
                Provider = provider,
                ModelId = 1,
                Model = model,
                IsEnabled = true
            };

            _context.ModelAuthors.Add(author);
            _context.ModelSeries.Add(series);
            _context.ModelCapabilities.Add(capabilities);
            _context.Models.Add(model);
            _context.Providers.Add(provider);
            _context.ModelProviderMappings.Add(mapping);
            await _context.SaveChangesAsync(default(CancellationToken));

            // Act
            var result = await _controller.GetModelParameters("test-model");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            
            var json = JsonSerializer.Serialize(response);
            var jsonDoc = JsonDocument.Parse(json);
            
            Assert.Equal(1, jsonDoc.RootElement.GetProperty("model_id").GetInt32());
            Assert.Equal("test-model", jsonDoc.RootElement.GetProperty("model_alias").GetString());
            Assert.Equal("Test Series", jsonDoc.RootElement.GetProperty("series_name").GetString());
            Assert.True(jsonDoc.RootElement.TryGetProperty("parameters", out var parameters));
            Assert.True(parameters.TryGetProperty("temperature", out var temperature));
        }

        [Fact]
        public async Task GetModelParameters_WithModelId_ReturnsParameters()
        {
            // Arrange
            var virtualKey = new VirtualKey { KeyName = "test-virtual-key", IsEnabled = true };
            _virtualKeyServiceMock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.FromResult<VirtualKey?>(virtualKey));

            var series = new ModelSeries 
            { 
                Id = 1, 
                Name = "Test Series",
                Parameters = "{\"resolution\":{\"type\":\"select\",\"options\":[\"720p\",\"1080p\"]}}"
            };
            var model = new Model 
            { 
                Id = 42, 
                Name = "TestModel",
                ModelSeriesId = 1,
                Series = series,
                ModelCapabilitiesId = 1
            };
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = "test-model-42",
                ProviderModelId = "test-model-v1",
                ProviderId = 1,
                ModelId = 42,
                Model = model,
                IsEnabled = true
            };

            _context.ModelSeries.Add(series);
            _context.Models.Add(model);
            _context.ModelProviderMappings.Add(mapping);
            await _context.SaveChangesAsync(default(CancellationToken));

            // Act
            var result = await _controller.GetModelParameters("42");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            
            var json = JsonSerializer.Serialize(response);
            var jsonDoc = JsonDocument.Parse(json);
            
            Assert.Equal(42, jsonDoc.RootElement.GetProperty("model_id").GetInt32());
            Assert.Equal("test-model-42", jsonDoc.RootElement.GetProperty("model_alias").GetString());
        }

        [Fact]
        public async Task GetModelParameters_WithNonExistentModel_ReturnsNotFound()
        {
            // Arrange
            var virtualKey = new VirtualKey { KeyName = "test-virtual-key", IsEnabled = true };
            _virtualKeyServiceMock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.FromResult<VirtualKey?>(virtualKey));

            // Act
            var result = await _controller.GetModelParameters("non-existent-model");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
            Assert.Contains("not found", errorResponse.error.ToString()?.ToLower() ?? "");
        }

        [Fact]
        public async Task GetModelParameters_WithInvalidVirtualKey_ReturnsUnauthorized()
        {
            // Arrange
            _virtualKeyServiceMock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.FromResult<VirtualKey?>(null));

            // Act
            var result = await _controller.GetModelParameters("test-model");

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Invalid virtual key", errorResponse.error.ToString());
        }

        [Fact]
        public async Task GetModelParameters_WithNoVirtualKey_ReturnsUnauthorized()
        {
            // Arrange
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = await _controller.GetModelParameters("test-model");

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Virtual key not found", errorResponse.error.ToString());
        }

        [Fact]
        public async Task GetModelParameters_WithEmptyParameters_ReturnsEmptyObject()
        {
            // Arrange
            var virtualKey = new VirtualKey { KeyName = "test-virtual-key", IsEnabled = true };
            _virtualKeyServiceMock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.FromResult<VirtualKey?>(virtualKey));

            var series = new ModelSeries 
            { 
                Id = 1, 
                Name = "Test Series",
                Parameters = "{}" // Empty parameters
            };
            var model = new Model 
            { 
                Id = 1, 
                Name = "TestModel",
                ModelSeriesId = 1,
                Series = series
            };
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = "test-model",
                ProviderModelId = "test-model-v1",
                ModelId = 1,
                Model = model,
                IsEnabled = true
            };

            _context.ModelSeries.Add(series);
            _context.Models.Add(model);
            _context.ModelProviderMappings.Add(mapping);
            await _context.SaveChangesAsync(default(CancellationToken));

            // Act
            var result = await _controller.GetModelParameters("test-model");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            
            var json = JsonSerializer.Serialize(response);
            var jsonDoc = JsonDocument.Parse(json);
            
            Assert.True(jsonDoc.RootElement.TryGetProperty("parameters", out var parameters));
            Assert.Equal(JsonValueKind.Object, parameters.ValueKind);
            var count = 0;
            foreach (var _ in parameters.EnumerateObject())
                count++;
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task GetModelParameters_WithInvalidJson_ReturnsEmptyObject()
        {
            // Arrange
            var virtualKey = new VirtualKey { KeyName = "test-virtual-key", IsEnabled = true };
            _virtualKeyServiceMock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.FromResult<VirtualKey?>(virtualKey));

            var series = new ModelSeries 
            { 
                Id = 1, 
                Name = "Test Series",
                Parameters = "invalid json {" // Invalid JSON
            };
            var model = new Model 
            { 
                Id = 1, 
                Name = "TestModel",
                ModelSeriesId = 1,
                Series = series
            };
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = "test-model",
                ProviderModelId = "test-model-v1",
                ModelId = 1,
                Model = model,
                IsEnabled = true
            };

            _context.ModelSeries.Add(series);
            _context.Models.Add(model);
            _context.ModelProviderMappings.Add(mapping);
            await _context.SaveChangesAsync(default(CancellationToken));

            // Act
            var result = await _controller.GetModelParameters("test-model");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            
            var json = JsonSerializer.Serialize(response);
            var jsonDoc = JsonDocument.Parse(json);
            
            Assert.True(jsonDoc.RootElement.TryGetProperty("parameters", out var parameters));
            Assert.Equal(JsonValueKind.Object, parameters.ValueKind);
            var count = 0;
            foreach (var _ in parameters.EnumerateObject())
                count++;
            Assert.Equal(0, count); // Should be empty object on parse failure
        }

        [Fact]
        public async Task GetModelParameters_WithDisabledMapping_ReturnsNotFound()
        {
            // Arrange
            var virtualKey = new VirtualKey { KeyName = "test-virtual-key", IsEnabled = true };
            _virtualKeyServiceMock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.FromResult<VirtualKey?>(virtualKey));

            var series = new ModelSeries { Id = 1, Name = "Test Series" };
            var model = new Model { Id = 1, Name = "TestModel", ModelSeriesId = 1, Series = series };
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = "disabled-model",
                ProviderModelId = "test-model-v1",
                ModelId = 1,
                Model = model,
                IsEnabled = false // Disabled mapping
            };

            _context.ModelSeries.Add(series);
            _context.Models.Add(model);
            _context.ModelProviderMappings.Add(mapping);
            await _context.SaveChangesAsync(default(CancellationToken));

            // Act
            var result = await _controller.GetModelParameters("disabled-model");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
            Assert.Contains("not found", errorResponse.error.ToString()?.ToLower() ?? "");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}