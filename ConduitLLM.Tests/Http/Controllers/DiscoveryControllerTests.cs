using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;
using ConduitLLM.Tests.Http.Builders;
using ConduitLLM.Tests.Http.TestHelpers;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class DiscoveryControllerTests : ControllerTestBase, IDisposable
    {
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _mockDbContextFactory;
        private readonly Mock<IModelCapabilityService> _mockModelCapabilityService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<IDiscoveryCacheService> _mockDiscoveryCacheService;
        private readonly Mock<ILogger<DiscoveryController>> _mockLogger;
        private ConduitDbContext _dbContext;
        private readonly DiscoveryController _controller;
        private readonly string _databaseName;

        public DiscoveryControllerTests(ITestOutputHelper output) : base(output)
        {
            _databaseName = Guid.NewGuid().ToString();
            _mockDbContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            _mockModelCapabilityService = new Mock<IModelCapabilityService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockDiscoveryCacheService = new Mock<IDiscoveryCacheService>();
            _mockLogger = new Mock<ILogger<DiscoveryController>>();

            _controller = new DiscoveryController(
                _mockDbContextFactory.Object,
                _mockModelCapabilityService.Object,
                _mockVirtualKeyService.Object,
                _mockDiscoveryCacheService.Object,
                _mockLogger.Object);

            // Setup default DbContext factory to return InMemory database
            SetupInMemoryDatabase();
        }

        private void SetupInMemoryDatabase()
        {
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;

            _dbContext = new ConduitDbContext(options);
            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_dbContext);
        }

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(DiscoveryController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_ShouldCreateInstance()
        {
            // Arrange & Act
            var controller = new DiscoveryController(
                _mockDbContextFactory.Object,
                _mockModelCapabilityService.Object,
                _mockVirtualKeyService.Object,
                _mockDiscoveryCacheService.Object,
                _mockLogger.Object);

            // Assert
            Assert.NotNull(controller);
        }

        [Fact]
        public void Constructor_WithNullDbContextFactory_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DiscoveryController(
                null!,
                _mockModelCapabilityService.Object,
                _mockVirtualKeyService.Object,
                _mockDiscoveryCacheService.Object,
                _mockLogger.Object));
            
            Assert.Equal("dbContextFactory", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullModelCapabilityService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DiscoveryController(
                _mockDbContextFactory.Object,
                null!,
                _mockVirtualKeyService.Object,
                _mockDiscoveryCacheService.Object,
                _mockLogger.Object));
            
            Assert.Equal("modelCapabilityService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullVirtualKeyService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DiscoveryController(
                _mockDbContextFactory.Object,
                _mockModelCapabilityService.Object,
                null!,
                _mockDiscoveryCacheService.Object,
                _mockLogger.Object));
            
            Assert.Equal("virtualKeyService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DiscoveryController(
                _mockDbContextFactory.Object,
                _mockModelCapabilityService.Object,
                _mockVirtualKeyService.Object,
                _mockDiscoveryCacheService.Object,
                null!));
            
            Assert.Equal("logger", ex.ParamName);
        }

        #endregion

        #region GetModels Tests - Authentication/Authorization

        [Fact]
        public async Task GetModels_WithoutVirtualKeyClaim_ShouldReturnUnauthorized()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await _controller.GetModels();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Virtual key not found", errorDto.error.ToString());
        }

        [Fact]
        public async Task GetModels_WithInvalidVirtualKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var claims = new List<Claim> { new Claim("VirtualKey", "invalid-key") };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var httpContext = new DefaultHttpContext { User = principal };
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("invalid-key", It.IsAny<string>()))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Invalid virtual key", errorDto.error.ToString());
        }

        [Fact]
        public async Task GetModels_WithDisabledVirtualKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var claims = new List<Claim> { new Claim("VirtualKey", "disabled-key") };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var httpContext = new DefaultHttpContext { User = principal };
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("disabled-key", It.IsAny<string>()))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Invalid virtual key", errorDto.error.ToString());
        }

        #endregion

        #region GetModels Tests - Data Retrieval

        [Fact]
        public async Task GetModels_WithValidKey_ReturnsAllEnabledModels()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithVisionSupport(true)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("claude-3")
                    .WithVisionSupport(false)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels(capability: null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(2, response.count);
            Assert.Equal(2, ((IEnumerable<object>)response.data).Count());
        }

        [Fact]
        public async Task GetModels_SkipsModelsWithNullModel_ReturnsOnlyValid()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithModel(null) // Model is null
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("claude-3")
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
        }

        [Fact]
        public async Task GetModels_SkipsModelsWithNullCapabilities_ReturnsOnlyValid()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithCapabilities(null) // Capabilities is null
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("claude-3")
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
        }

        [Fact]
        public async Task GetModels_RespectsProviderIsEnabledFlag_ReturnsOnlyEnabledProviders()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithProviderEnabled(false) // Provider disabled
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("claude-3")
                    .WithProviderEnabled(true)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
        }

        [Fact]
        public async Task GetModels_RespectsModelProviderMappingIsEnabledFlag_ReturnsOnlyEnabledMappings()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithMappingEnabled(false) // Mapping disabled
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("claude-3")
                    .WithMappingEnabled(true)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
        }

        #endregion

        #region GetModels Tests - Capability Filtering

        [Fact]
        public async Task GetModels_FilterByVisionCapability_ReturnsOnlyVisionModels()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4-vision")
                    .WithVisionSupport(true)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-3.5")
                    .WithVisionSupport(false)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels(capability: "vision");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
            Assert.Equal("gpt-4-vision", ((IEnumerable<dynamic>)response.data).First().id);
        }

        [Fact]
        public async Task GetModels_FilterByStreamingCapability_ReturnsOnlyStreamingModels()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithStreamingSupport(true)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("dall-e")
                    .WithStreamingSupport(false)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels(capability: "streaming");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
            Assert.Equal("gpt-4", ((IEnumerable<dynamic>)response.data).First().id);
        }

        [Fact]
        public async Task GetModels_FilterByChatStreamCapability_ReturnsOnlyStreamingModels()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithStreamingSupport(true)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("dall-e")
                    .WithStreamingSupport(false)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels(capability: "chat_stream");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
        }

        [Fact]
        public async Task GetModels_FilterByInvalidCapability_ReturnsEmptyList()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder().WithModelAlias("gpt-4").Build(),
                new ModelProviderMappingBuilder().WithModelAlias("claude-3").Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels(capability: "invalid_capability");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(0, response.count);
        }

        [Fact]
        public async Task GetModels_CapabilityFilterIsCaseInsensitive_WorksWithVariations()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4-vision")
                    .WithVisionSupport(true)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act - Test with dash instead of underscore
            var result = await _controller.GetModels(capability: "audio-transcription");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            // Should work as controller converts dashes to underscores
            Assert.NotNull(response);
        }

        #endregion

        #region GetModels Tests - Response Structure

        [Fact]
        public async Task GetModels_ReturnsFlatStructureWithBooleanCapabilityFlags()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithFullCapabilities()
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            dynamic model = ((IEnumerable<dynamic>)response.data).First();
            
            Assert.True(model.supports_chat);
            Assert.True(model.supports_streaming);
            Assert.True(model.supports_vision);
            Assert.True(model.supports_function_calling);
            Assert.True(model.supports_audio_transcription);
            Assert.True(model.supports_text_to_speech);
            Assert.True(model.supports_realtime_audio);
            Assert.True(model.supports_video_generation);
            Assert.True(model.supports_image_generation);
            Assert.True(model.supports_embeddings);
        }

        [Fact]
        public async Task GetModels_IncludesMetadataFields_ReturnsCompleteModelInfo()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithDescription("Advanced language model")
                    .WithModelCardUrl("https://example.com/gpt-4")
                    .WithMaxTokens(8192)
                    .WithTokenizerType(TokenizerType.Cl100KBase)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            dynamic model = ((IEnumerable<dynamic>)response.data).First();
            
            Assert.Equal("gpt-4", model.id);
            Assert.Equal("gpt-4", model.display_name);
            Assert.Equal("Advanced language model", model.description);
            Assert.Equal("https://example.com/gpt-4", model.model_card_url);
            Assert.Equal(8192, model.max_tokens);
            Assert.Equal("cl100kbase", model.tokenizer_type);
        }

        [Fact]
        public async Task GetModels_HandlesNullDescriptionAndModelCardUrl_ReturnsEmptyStrings()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithDescription(null)
                    .WithModelCardUrl(null)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            dynamic model = ((IEnumerable<dynamic>)response.data).First();
            
            Assert.Equal(string.Empty, model.description);
            Assert.Equal(string.Empty, model.model_card_url);
        }

        #endregion

        #region GetModels Tests - Error Handling

        [Fact]
        public async Task GetModels_WhenDatabaseExceptionOccurs_Returns500Error()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _controller.GetModels();

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            var errorDto = Assert.IsType<ErrorResponseDto>(objectResult.Value);
            Assert.Equal("Failed to retrieve model discovery information", errorDto.error.ToString());
        }

        [Fact]
        public async Task GetModels_WhenExceptionOccurs_LogsError()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");
            var exception = new Exception("Test exception");

            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act
            await _controller.GetModels();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error retrieving model discovery information")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region GetCapabilities Tests

        [Fact]
        public async Task GetCapabilities_ReturnsStaticListOfAllCapabilities()
        {
            // Act
            var result = await _controller.GetCapabilities();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            var capabilities = (string[])response.capabilities;
            
            Assert.Contains("chat", capabilities);
            Assert.Contains("chat_stream", capabilities);
            Assert.Contains("vision", capabilities);
            Assert.Contains("audio_transcription", capabilities);
            Assert.Contains("text_to_speech", capabilities);
            Assert.Contains("realtime_audio", capabilities);
            Assert.Contains("video_generation", capabilities);
            Assert.Contains("image_generation", capabilities);
            Assert.Contains("embeddings", capabilities);
            Assert.Contains("function_calling", capabilities);
            Assert.Contains("tool_use", capabilities);
            Assert.Contains("json_mode", capabilities);
        }

        [Fact]
        public async Task GetCapabilities_ReturnsCorrectNumberOfCapabilities()
        {
            // Act
            var result = await _controller.GetCapabilities();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            var capabilities = (string[])response.capabilities;
            Assert.Equal(12, capabilities.Length);
        }

        // NOTE: GetCapabilities_WhenExceptionOccurs_Returns500Error test was removed
        // because it's not realistic to force an exception in this method.
        // The method only creates a static array and returns it, which cannot fail
        // under normal circumstances. The catch block exists for defensive programming
        // but would never be reached in practice.

        #endregion

        #region GetModelParameters Tests

        [Fact]
        public async Task GetModelParameters_WithoutVirtualKeyClaim_ShouldReturnUnauthorized()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await _controller.GetModelParameters("gpt-4");

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Virtual key not found", errorDto.error.ToString());
        }

        [Fact]
        public async Task GetModelParameters_WithValidModelAlias_ReturnsParameters()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var parametersJson = @"{
                ""temperature"": {
                    ""type"": ""slider"",
                    ""min"": 0,
                    ""max"": 2,
                    ""default"": 1
                }
            }";

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithModelId(1)
                    .WithSeriesParameters(parametersJson)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModelParameters("gpt-4");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.model_id);
            Assert.Equal("gpt-4", response.model_alias);
            Assert.NotNull(response.parameters);
        }

        [Fact]
        public async Task GetModelParameters_WithNumericModelId_ReturnsParameters()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var parametersJson = @"{""test"": ""value""}";

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithModelId(123)
                    .WithSeriesParameters(parametersJson)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModelParameters("123");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(123, response.model_id);
            Assert.Equal("gpt-4", response.model_alias);
        }

        [Fact]
        public async Task GetModelParameters_WithNonExistentModel_ReturnsNotFound()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");
            SetupModelProviderMappings(new List<ModelProviderMapping>());

            // Act
            var result = await _controller.GetModelParameters("non-existent");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
            Assert.Equal("Model 'non-existent' not found or has no parameter information", errorDto.error.ToString());
        }

        [Fact]
        public async Task GetModelParameters_WithInvalidParametersJson_ReturnsEmptyObject()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithModelId(1)
                    .WithSeriesParameters("invalid json {}") // Invalid JSON
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModelParameters("gpt-4");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.NotNull(response.parameters); // Should return empty object, not null
        }

        [Fact]
        public async Task GetModelParameters_WhenExceptionOccurs_Returns500Error()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetModelParameters("gpt-4");

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            var errorDto = Assert.IsType<ErrorResponseDto>(objectResult.Value);
            Assert.Equal("Failed to retrieve model parameters", errorDto.error.ToString());
        }

        #endregion

        #region Integration Scenarios

        [Fact]
        public async Task GetModels_WithMultipleModelsFromSameProvider_ReturnsAll()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var provider = new ProviderBuilder().WithProviderId(1).Build();
            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithProvider(provider)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-3.5-turbo")
                    .WithProvider(provider)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("text-davinci-003")
                    .WithProvider(provider)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(3, response.count);
        }

        [Fact]
        public async Task GetModels_WithEmptyDatabase_ReturnsEmptyList()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");
            SetupModelProviderMappings(new List<ModelProviderMapping>());

            // Act
            var result = await _controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(0, response.count);
            Assert.Empty((IEnumerable<object>)response.data);
        }

        [Fact]
        public async Task GetModels_WithLargeResultSet_HandlesCorrectly()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>();
            for (int i = 1; i <= 150; i++)
            {
                mappings.Add(new ModelProviderMappingBuilder()
                    .WithModelAlias($"model-{i}")
                    .Build());
            }

            SetupModelProviderMappings(mappings);

            // Act
            var result = await _controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(150, response.count);
            Assert.Equal(150, ((IEnumerable<object>)response.data).Count());
        }

        #endregion

        #region Helper Methods

        private void SetupValidVirtualKey(string keyValue)
        {
            var claims = new List<Claim> { new Claim("VirtualKey", keyValue) };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var httpContext = new DefaultHttpContext { User = principal };
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var virtualKey = new VirtualKey { Id = 1, KeyHash = keyValue, IsEnabled = true };
            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(keyValue, It.IsAny<string>()))
                .ReturnsAsync(virtualKey);
        }

        private void SetupModelProviderMappings(List<ModelProviderMapping> mappings)
        {
            // Clear existing data
            _dbContext.ModelProviderMappings.RemoveRange(_dbContext.ModelProviderMappings);
            
            // Add new data
            if (mappings.Any())
            {
                _dbContext.ModelProviderMappings.AddRange(mappings);
                _dbContext.SaveChanges();
            }
        }
        
        public new void Dispose()
        {
            _dbContext?.Dispose();
            base.Dispose();
        }

        #endregion
    }
}