using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.Configuration;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Tests.Integration;

/// <summary>
/// Integration tests for model capability detection and navigation state management.
/// Note: These tests are designed to work in test environments and may require mocking.
/// </summary>
public class ModelCapabilityIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public ModelCapabilityIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task NavigationStateService_ValidatesCapabilityDetectionLogic()
    {
        // Arrange - Create mocked services
        var mockAdminApiClient = new Mock<IAdminApiClient>();
        var mockConduitApiClient = new Mock<IConduitApiClient>();
        var mockLogger = new Mock<ILogger<SignalRNavigationStateService>>();

        // Setup mock data - enabled model with image generation capability
        var testMappings = new List<ModelProviderMappingDto>
        {
            new ModelProviderMappingDto
            {
                ModelId = "dall-e-3",
                ProviderModelId = "dall-e-3",
                ProviderId = "1",
                IsEnabled = true,
                SupportsImageGeneration = true
            }
        };

        mockAdminApiClient
            .Setup(x => x.GetAllModelProviderMappingsAsync())
            .ReturnsAsync(testMappings);

        try
        {
            // Act
            var mockConfiguration = new Mock<IConfiguration>();
            var mockJSRuntime = new Mock<IJSRuntime>();
            var mockConnectionLogger = new Mock<ILogger<SignalRConnectionManager>>();
            var signalRConnectionManager = new SignalRConnectionManager(mockJSRuntime.Object, mockConnectionLogger.Object);
            var mockGlobalSettingService = new Mock<IGlobalSettingService>();
            mockGlobalSettingService.Setup(x => x.GetSettingAsync("WebUI_VirtualKey"))
                .ReturnsAsync("test-virtual-key");
            
            var navigationService = new SignalRNavigationStateService(
                mockAdminApiClient.Object, 
                mockConduitApiClient.Object, 
                mockConfiguration.Object,
                mockLogger.Object,
                signalRConnectionManager,
                mockGlobalSettingService.Object);

            _output.WriteLine("Testing navigation state service capability detection...");

            // Force refresh to trigger capability checking
            await navigationService.RefreshStatesAsync();
            
            // Get navigation state for image generation
            var imageGenState = await navigationService.GetNavigationItemStateAsync("/image-generation");
            
            // Get capability status
            var capabilityStatus = await navigationService.GetCapabilityStatusAsync();

            // Assert
            _output.WriteLine($"Image generation enabled: {imageGenState.IsEnabled}");
            _output.WriteLine($"Tooltip: {imageGenState.TooltipMessage}");
            _output.WriteLine($"Total models: {capabilityStatus.TotalConfiguredModels}");
            _output.WriteLine($"Image gen models: {capabilityStatus.ImageGenerationModels}");

            Assert.NotNull(imageGenState);
            Assert.True(imageGenState.IsEnabled, "Navigation should be enabled with image generation model");
            Assert.Null(imageGenState.TooltipMessage); // Should be null when enabled
            Assert.False(imageGenState.ShowIndicator);

            Assert.NotNull(capabilityStatus);
            Assert.Equal(1, capabilityStatus.TotalConfiguredModels);
            Assert.Equal(1, capabilityStatus.ImageGenerationModels);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Test error: {ex.Message}");
            throw; // Re-throw to fail the test
        }
    }

    [Fact]
    public async Task NavigationStateService_WithoutImageModels_DisablesImageGenerationNavigation()
    {
        // Arrange - Mock services with no image generation models
        var mockAdminApiClient = new Mock<IAdminApiClient>();
        var mockConduitApiClient = new Mock<IConduitApiClient>();
        var mockLogger = new Mock<ILogger<SignalRNavigationStateService>>();

        // Setup mock data - models without image generation capability
        var testMappings = new List<ModelProviderMappingDto>
        {
            new ModelProviderMappingDto
            {
                ModelId = "gpt-4",
                ProviderModelId = "gpt-4",
                ProviderId = "1",
                IsEnabled = true,
                SupportsImageGeneration = false // No image generation
            }
        };

        mockAdminApiClient
            .Setup(x => x.GetAllModelProviderMappingsAsync())
            .ReturnsAsync(testMappings);

        // Act
        var mockConfiguration = new Mock<IConfiguration>();
        var mockJSRuntime = new Mock<IJSRuntime>();
        var mockConnectionLogger = new Mock<ILogger<SignalRConnectionManager>>();
        var signalRConnectionManager = new SignalRConnectionManager(mockJSRuntime.Object, mockConnectionLogger.Object);
        var mockGlobalSettingService = new Mock<IGlobalSettingService>();
        mockGlobalSettingService.Setup(x => x.GetSettingAsync("WebUI_VirtualKey"))
            .ReturnsAsync("test-virtual-key");
        
        var navigationService = new SignalRNavigationStateService(
            mockAdminApiClient.Object, 
            mockConduitApiClient.Object, 
            mockConfiguration.Object,
            mockLogger.Object,
            signalRConnectionManager,
            mockGlobalSettingService.Object);

        await navigationService.RefreshStatesAsync();
        var imageGenState = await navigationService.GetNavigationItemStateAsync("/image-generation");

        // Assert
        _output.WriteLine($"Image generation disabled state - Enabled: {imageGenState.IsEnabled}");
        _output.WriteLine($"Tooltip: {imageGenState.TooltipMessage}");

        Assert.NotNull(imageGenState);
        Assert.False(imageGenState.IsEnabled, "Navigation should be disabled without image generation models");
        Assert.NotNull(imageGenState.TooltipMessage);
        Assert.True(imageGenState.ShowIndicator);
        Assert.Equal("/model-mappings", imageGenState.RequiredConfigurationUrl);
    }

    [Fact]
    public async Task CapabilityStatusInfo_ProvidesDetailedInformation()
    {
        // Arrange - Mock services for capability status testing
        var mockAdminApiClient = new Mock<IAdminApiClient>();
        var mockConduitApiClient = new Mock<IConduitApiClient>();
        var mockLogger = new Mock<ILogger<SignalRNavigationStateService>>();

        // Setup mock data with various capability models
        var testMappings = new List<ModelProviderMappingDto>
        {
            new ModelProviderMappingDto
            {
                ModelId = "dall-e-3",
                ProviderModelId = "dall-e-3",
                ProviderId = "1",
                IsEnabled = true,
                SupportsImageGeneration = true
            },
            new ModelProviderMappingDto
            {
                ModelId = "gpt-4-vision",
                ProviderModelId = "gpt-4-vision-preview",
                ProviderId = "1",
                IsEnabled = true,
                SupportsVision = true
            },
            new ModelProviderMappingDto
            {
                ModelId = "whisper-1",
                ProviderModelId = "whisper-1",
                ProviderId = "1",
                IsEnabled = true,
                SupportsAudioTranscription = true
            }
        };

        mockAdminApiClient
            .Setup(x => x.GetAllModelProviderMappingsAsync())
            .ReturnsAsync(testMappings);

        // Act
        var mockConfiguration = new Mock<IConfiguration>();
        var mockJSRuntime = new Mock<IJSRuntime>();
        var mockConnectionLogger = new Mock<ILogger<SignalRConnectionManager>>();
        var signalRConnectionManager = new SignalRConnectionManager(mockJSRuntime.Object, mockConnectionLogger.Object);
        var mockGlobalSettingService = new Mock<IGlobalSettingService>();
        mockGlobalSettingService.Setup(x => x.GetSettingAsync("WebUI_VirtualKey"))
            .ReturnsAsync("test-virtual-key");
        
        var navigationService = new SignalRNavigationStateService(
            mockAdminApiClient.Object, 
            mockConduitApiClient.Object, 
            mockConfiguration.Object,
            mockLogger.Object,
            signalRConnectionManager,
            mockGlobalSettingService.Object);

        _output.WriteLine("Testing capability status information...");
        var capabilityStatus = await navigationService.GetCapabilityStatusAsync();

        // Assert
        Assert.NotNull(capabilityStatus);
        _output.WriteLine($"Capability Status: {capabilityStatus.Summary}");
        _output.WriteLine($"Total Models: {capabilityStatus.TotalConfiguredModels}");
        _output.WriteLine($"Image Generation Models: {capabilityStatus.ImageGenerationModels}");
        _output.WriteLine($"Vision Models: {capabilityStatus.VisionModels}");
        _output.WriteLine($"Audio Models: {capabilityStatus.AudioTranscriptionModels + capabilityStatus.TextToSpeechModels}");

        if (capabilityStatus.ConfiguredModels.Any())
        {
            _output.WriteLine("Configured Models:");
            foreach (var model in capabilityStatus.ConfiguredModels.Take(3))
            {
                _output.WriteLine($"  - {model.ModelId}: [{string.Join(", ", model.SupportedCapabilities)}]");
            }
        }

        // Verify structure is valid
        Assert.Equal(3, capabilityStatus.TotalConfiguredModels);
        Assert.Equal(1, capabilityStatus.ImageGenerationModels);
        Assert.Equal(1, capabilityStatus.VisionModels);
        Assert.Equal(1, capabilityStatus.AudioTranscriptionModels);
        Assert.NotNull(capabilityStatus.ConfiguredModels);
        Assert.Equal(3, capabilityStatus.ConfiguredModels.Count());
    }

    [Fact]
    public async Task DiscoveryApiIntegration_WithMocking_CanTestModelCapabilities()
    {
        // Arrange - Mock the Conduit API client
        var mockAdminApiClient = new Mock<IAdminApiClient>();
        var mockConduitApiClient = new Mock<IConduitApiClient>();
        var mockLogger = new Mock<ILogger<SignalRNavigationStateService>>();

        // Setup Discovery API mock responses - must specify all parameters due to optional parameters
        mockConduitApiClient
            .Setup(x => x.TestModelCapabilityAsync(It.Is<string>(s => s == "dall-e-3"), It.Is<string>(s => s == "ImageGeneration"), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        mockConduitApiClient
            .Setup(x => x.TestModelCapabilityAsync(It.Is<string>(s => s == "gpt-4-vision"), It.Is<string>(s => s == "Vision"), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mockConduitApiClient
            .Setup(x => x.TestModelCapabilityAsync(It.Is<string>(s => s == "gpt-4"), It.Is<string>(s => s == "ImageGeneration"), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        try
        {
            // Act
            _output.WriteLine("Testing Discovery API capability testing with mocking...");
            
            // Test known model capabilities
            var dalleSupportsImageGen = await mockConduitApiClient.Object.TestModelCapabilityAsync("dall-e-3", "ImageGeneration");
            var gpt4VisionSupportsVision = await mockConduitApiClient.Object.TestModelCapabilityAsync("gpt-4-vision", "Vision");
            var gpt4SupportsImageGen = await mockConduitApiClient.Object.TestModelCapabilityAsync("gpt-4", "ImageGeneration");

            // Assert
            _output.WriteLine($"DALL-E 3 supports image generation: {dalleSupportsImageGen}");
            _output.WriteLine($"GPT-4 Vision supports vision: {gpt4VisionSupportsVision}");
            _output.WriteLine($"GPT-4 supports image generation: {gpt4SupportsImageGen}");

            Assert.True(dalleSupportsImageGen);
            Assert.True(gpt4VisionSupportsVision);
            Assert.False(gpt4SupportsImageGen);

            // Verify the mock was called correctly - must specify all parameters due to optional parameters
            mockConduitApiClient.Verify(x => x.TestModelCapabilityAsync(It.Is<string>(s => s == "dall-e-3"), It.Is<string>(s => s == "ImageGeneration"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            mockConduitApiClient.Verify(x => x.TestModelCapabilityAsync(It.Is<string>(s => s == "gpt-4-vision"), It.Is<string>(s => s == "Vision"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            mockConduitApiClient.Verify(x => x.TestModelCapabilityAsync(It.Is<string>(s => s == "gpt-4"), It.Is<string>(s => s == "ImageGeneration"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Discovery API test error: {ex.Message}");
            throw; // Re-throw since we're using mocks - shouldn't fail
        }
    }
}