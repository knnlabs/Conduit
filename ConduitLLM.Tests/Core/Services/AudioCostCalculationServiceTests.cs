using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    // TODO: AudioCostCalculationService does not exist in Core project yet
    // This test file is commented out until the service is implemented
    /*
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    [Trait("Component", "Core")]
    public class AudioCostCalculationServiceTests : TestBase
    {
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<ILogger<AudioCostCalculationService>> _loggerMock;
        private readonly Mock<IAudioCostRepository> _audioCostRepositoryMock;
        private readonly Mock<IServiceScope> _serviceScopeMock;
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
        private readonly AudioCostCalculationService _service;

        public AudioCostCalculationServiceTests(ITestOutputHelper output) : base(output)
        {
            _serviceProviderMock = new Mock<IServiceProvider>();
            _loggerMock = CreateLogger<AudioCostCalculationService>();
            _audioCostRepositoryMock = new Mock<IAudioCostRepository>();
            _serviceScopeMock = new Mock<IServiceScope>();
            _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();

            // Setup service scope
            var scopedServiceProvider = new Mock<IServiceProvider>();
            scopedServiceProvider
                .Setup(x => x.GetService(typeof(IAudioCostRepository)))
                .Returns(_audioCostRepositoryMock.Object);

            _serviceScopeMock
                .Setup(x => x.ServiceProvider)
                .Returns(scopedServiceProvider.Object);

            _serviceScopeFactoryMock
                .Setup(x => x.CreateScope())
                .Returns(_serviceScopeMock.Object);

            _serviceProviderMock
                .Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(_serviceScopeFactoryMock.Object);

            _service = new AudioCostCalculationService(_serviceProviderMock.Object, _loggerMock.Object);
        }

        [Fact]
        public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new AudioCostCalculationService(null!, _loggerMock.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("serviceProvider");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new AudioCostCalculationService(_serviceProviderMock.Object, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Fact]
        public async Task CalculateTranscriptionCostAsync_WithBuiltInOpenAIRate_CalculatesCorrectly()
        {
            // Arrange
            var provider = "openai";
            var model = "whisper-1";
            var durationSeconds = 300.0; // 5 minutes

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);

            // Assert
            result.Should().NotBeNull();
            result.Provider.Should().Be(provider);
            result.Operation.Should().Be("transcription");
            result.Model.Should().Be(model);
            result.UnitCount.Should().Be(5.0); // 5 minutes
            result.UnitType.Should().Be("minutes");
            result.RatePerUnit.Should().Be(0.006m); // $0.006 per minute
            result.TotalCost.Should().Be(0.03); // 5 * 0.006
            result.IsEstimate.Should().BeFalse();
        }

        [Fact]
        public async Task CalculateTranscriptionCostAsync_WithCustomRate_UsesCustomRate()
        {
            // Arrange
            var provider = "openai";
            var model = "whisper-1";
            var durationSeconds = 600.0; // 10 minutes
            var customRate = 0.01m;

            var customCost = new AudioCost
            {
                Provider = provider,
                OperationType = "transcription",
                Model = model,
                CostPerUnit = customRate,
                IsActive = true
            };

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync(customCost);

            // Act
            var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);

            // Assert
            result.RatePerUnit.Should().Be(customRate);
            result.TotalCost.Should().Be(0.1); // 10 * 0.01
        }

        [Fact]
        public async Task CalculateTranscriptionCostAsync_WithUnknownProvider_UsesDefaultRate()
        {
            // Arrange
            var provider = "unknown-provider";
            var model = "unknown-model";
            var durationSeconds = 120.0; // 2 minutes

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);

            // Assert
            result.RatePerUnit.Should().Be(0.01m); // Default rate
            result.TotalCost.Should().Be(0.02); // 2 * 0.01
            result.IsEstimate.Should().BeTrue();
            _loggerMock.VerifyLog(LogLevel.Warning, "No pricing found");
        }

        [Fact]
        public async Task CalculateTextToSpeechCostAsync_WithOpenAITTS1_CalculatesCorrectly()
        {
            // Arrange
            var provider = "openai";
            var model = "tts-1";
            var characterCount = 1000000; // 1M characters

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "text-to-speech", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateTextToSpeechCostAsync(provider, model, characterCount);

            // Assert
            result.Provider.Should().Be(provider);
            result.Operation.Should().Be("text-to-speech");
            result.Model.Should().Be(model);
            result.UnitCount.Should().Be(1000000);
            result.UnitType.Should().Be("characters");
            result.RatePerUnit.Should().Be(0.000015m); // $15 per 1M characters
            result.TotalCost.Should().Be(15.0); // 1M * 0.000015
        }

        [Fact]
        public async Task CalculateTextToSpeechCostAsync_WithElevenLabs_CalculatesCorrectly()
        {
            // Arrange
            var provider = "elevenlabs";
            var model = "eleven_multilingual_v2";
            var characterCount = 500000; // 500K characters
            var voice = "Rachel";

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "text-to-speech", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateTextToSpeechCostAsync(provider, model, characterCount, voice);

            // Assert
            result.RatePerUnit.Should().Be(0.00006m); // $60 per 1M characters
            result.TotalCost.Should().Be(30.0); // 500K * 0.00006
            result.Voice.Should().Be(voice);
        }

        [Fact]
        public async Task CalculateRealtimeCostAsync_WithOpenAI_CalculatesAudioAndTokenCosts()
        {
            // Arrange
            var provider = "openai";
            var model = "gpt-4o-realtime-preview";
            var inputAudioSeconds = 300.0; // 5 minutes
            var outputAudioSeconds = 180.0; // 3 minutes
            var inputTokens = 1000;
            var outputTokens = 2000;

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "realtime", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateRealtimeCostAsync(
                provider, model, inputAudioSeconds, outputAudioSeconds, inputTokens, outputTokens);

            // Assert
            result.Operation.Should().Be("realtime");
            result.UnitCount.Should().Be(8.0); // 5 + 3 minutes
            
            // Audio cost: (5 * 0.10) + (3 * 0.20) = 0.5 + 0.6 = 1.1
            // Token cost: (1000 * 0.000005) + (2000 * 0.000015) = 0.005 + 0.03 = 0.035
            // Total: 1.1 + 0.035 = 1.135
            result.TotalCost.Should().BeApproximately(1.135, 0.0001);
            
            result.DetailedBreakdown.Should().NotBeNull();
            result.DetailedBreakdown!["audio_cost"].Should().BeApproximately(1.1, 0.0001);
            result.DetailedBreakdown["token_cost"].Should().BeApproximately(0.035, 0.0001);
            result.DetailedBreakdown["input_minutes"].Should().Be(5.0);
            result.DetailedBreakdown["output_minutes"].Should().Be(3.0);
            result.DetailedBreakdown["input_tokens"].Should().Be(1000);
            result.DetailedBreakdown["output_tokens"].Should().Be(2000);
        }

        [Fact]
        public async Task CalculateRealtimeCostAsync_WithUltravox_AppliesMinimumDuration()
        {
            // Arrange
            var provider = "ultravox";
            var model = "fixie-ai/ultravox-70b";
            var inputAudioSeconds = 30.0; // 0.5 minutes
            var outputAudioSeconds = 15.0; // 0.25 minutes

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "realtime", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateRealtimeCostAsync(
                provider, model, inputAudioSeconds, outputAudioSeconds);

            // Assert
            // Minimum duration is 1 minute for each, so:
            // (1 * 0.001) + (1 * 0.001) = 0.002
            result.TotalCost.Should().Be(0.002);
        }

        [Fact]
        public async Task CalculateRealtimeCostAsync_WithNoTokenRates_CalculatesOnlyAudioCost()
        {
            // Arrange
            var provider = "ultravox";
            var model = "fixie-ai/ultravox-70b";
            var inputAudioSeconds = 120.0; // 2 minutes
            var outputAudioSeconds = 180.0; // 3 minutes
            var inputTokens = 1000;
            var outputTokens = 2000;

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "realtime", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateRealtimeCostAsync(
                provider, model, inputAudioSeconds, outputAudioSeconds, inputTokens, outputTokens);

            // Assert
            // Only audio cost: (2 * 0.001) + (3 * 0.001) = 0.005
            result.TotalCost.Should().Be(0.005);
            result.DetailedBreakdown!["token_cost"].Should().Be(0.0);
        }

        [Fact]
        public async Task CalculateAudioCostAsync_WithTranscription_DelegatesToTranscriptionMethod()
        {
            // Arrange
            var provider = "groq";
            var model = "whisper-large-v3";
            var durationSeconds = 600.0;

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateAudioCostAsync(
                provider, "transcription", model, durationSeconds, 0);

            // Assert
            result.Operation.Should().Be("transcription");
            result.RatePerUnit.Should().Be(0.0001m); // Groq rate
        }

        [Fact]
        public async Task CalculateAudioCostAsync_WithTextToSpeech_DelegatesToTTSMethod()
        {
            // Arrange
            var provider = "openai";
            var model = "tts-1-hd";
            var characterCount = 1000;

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "text-to-speech", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateAudioCostAsync(
                provider, "text-to-speech", model, 0, characterCount);

            // Assert
            result.Operation.Should().Be("text-to-speech");
            result.RatePerUnit.Should().Be(0.00003m);
        }

        [Fact]
        public async Task CalculateAudioCostAsync_WithRealtime_DelegatesToRealtimeMethod()
        {
            // Arrange
            var provider = "openai";
            var model = "gpt-4o-realtime-preview";
            var durationSeconds = 120.0; // Split evenly between input/output

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "realtime", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateAudioCostAsync(
                provider, "realtime", model, durationSeconds, 0);

            // Assert
            result.Operation.Should().Be("realtime");
            // 60s input (1 min) * 0.10 + 60s output (1 min) * 0.20 = 0.10 + 0.20 = 0.30
            result.TotalCost.Should().Be(0.30);
        }

        [Fact]
        public async Task CalculateAudioCostAsync_WithUnknownOperation_ThrowsArgumentException()
        {
            // Arrange
            var provider = "openai";
            var model = "some-model";

            // Act
            var act = () => _service.CalculateAudioCostAsync(
                provider, "unknown-operation", model, 0, 0);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Unknown operation: unknown-operation");
        }

        [Fact]
        public async Task CalculateTranscriptionCostAsync_WithDeepgram_UsesCorrectRate()
        {
            // Arrange
            var provider = "deepgram";
            var model = "nova-2-medical";
            var durationSeconds = 300.0; // 5 minutes

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);

            // Assert
            result.RatePerUnit.Should().Be(0.0145m); // Medical rate
            result.TotalCost.Should().Be(0.0725); // 5 * 0.0145
        }

        [Fact]
        public async Task GetCustomRateAsync_WithRepositoryException_ReturnsNull()
        {
            // Arrange
            var provider = "openai";
            var model = "whisper-1";
            var durationSeconds = 60.0;

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);

            // Assert
            result.RatePerUnit.Should().Be(0.006m); // Falls back to built-in rate
            _loggerMock.VerifyLog(LogLevel.Error, "Failed to get custom rate");
        }

        [Fact]
        public async Task GetCustomRateAsync_WithNoRepository_UsesBuiltInRate()
        {
            // Arrange
            var provider = "openai";
            var model = "whisper-1";
            var durationSeconds = 60.0;

            // Setup service provider to return null for repository
            var scopedServiceProvider = new Mock<IServiceProvider>();
            scopedServiceProvider
                .Setup(x => x.GetService(typeof(IAudioCostRepository)))
                .Returns(null);

            _serviceScopeMock
                .Setup(x => x.ServiceProvider)
                .Returns(scopedServiceProvider.Object);

            // Act
            var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);

            // Assert
            result.RatePerUnit.Should().Be(0.006m); // Built-in rate
        }

        [Fact]
        public async Task CalculateTranscriptionCostAsync_WithInactiveCustomCost_UsesBuiltInRate()
        {
            // Arrange
            var provider = "openai";
            var model = "whisper-1";
            var durationSeconds = 60.0;

            var inactiveCost = new AudioCost
            {
                Provider = provider,
                OperationType = "transcription",
                Model = model,
                CostPerUnit = 0.01m,
                IsActive = false // Inactive
            };

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync(inactiveCost);

            // Act
            var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);

            // Assert
            result.RatePerUnit.Should().Be(0.006m); // Built-in rate, not custom
        }

        [Fact]
        public async Task CalculateTextToSpeechCostAsync_WithVirtualKey_IncludesInResult()
        {
            // Arrange
            var provider = "openai";
            var model = "tts-1";
            var characterCount = 1000;
            var virtualKey = "test-virtual-key";

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "text-to-speech", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateTextToSpeechCostAsync(
                provider, model, characterCount, null, virtualKey);

            // Assert
            result.VirtualKey.Should().Be(virtualKey);
        }

        [Theory]
        [InlineData("transcription", "transcription")]
        [InlineData("text-to-speech", "text-to-speech")]
        [InlineData("tts", "text-to-speech")]
        [InlineData("realtime", "realtime")]
        [InlineData("TRANSCRIPTION", "transcription")]
        [InlineData("TTS", "text-to-speech")]
        public async Task CalculateAudioCostAsync_WithVariousOperations_MapsCorrectly(
            string inputOperation, string expectedOperation)
        {
            // Arrange
            var provider = "openai";
            var model = "test-model";

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateAudioCostAsync(
                provider, inputOperation, model, 60, 1000);

            // Assert
            result.Operation.Should().Be(expectedOperation);
        }

        [Fact]
        public async Task CalculateTranscriptionCostAsync_VerifiesCancellationTokenPropagation()
        {
            // Arrange
            var provider = "openai";
            var model = "whisper-1";
            var durationSeconds = 60.0;
            using var cts = new CancellationTokenSource();

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            await _service.CalculateTranscriptionCostAsync(
                provider, model, durationSeconds, null, cts.Token);

            // Assert - Just verify it doesn't throw
            _serviceScopeMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public async Task ServiceScope_IsProperlyDisposed()
        {
            // Arrange
            var provider = "openai";
            var model = "whisper-1";
            var durationSeconds = 60.0;

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);

            // Assert
            _serviceScopeMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Theory]
        [InlineData(60.0, 1.0)]      // 60 seconds = 1 minute
        [InlineData(90.0, 1.5)]      // 90 seconds = 1.5 minutes
        [InlineData(30.0, 0.5)]      // 30 seconds = 0.5 minutes
        [InlineData(3600.0, 60.0)]   // 1 hour = 60 minutes
        [InlineData(0.0, 0.0)]       // 0 seconds = 0 minutes
        public async Task CalculateTranscriptionCostAsync_ConvertsSecondsToMinutesCorrectly(
            double durationSeconds, double expectedMinutes)
        {
            // Arrange
            var provider = "openai";
            var model = "whisper-1";

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync((AudioCost?)null);

            // Act
            var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);

            // Assert
            result.UnitCount.Should().Be(expectedMinutes);
        }

        [Fact]
        public async Task CalculateTextToSpeechCostAsync_WithCustomRatePerThousandChars_CalculatesCorrectly()
        {
            // Arrange
            var provider = "custom";
            var model = "custom-tts";
            var characterCount = 5000;
            var customRatePerThousand = 0.05m; // $0.05 per 1K chars

            var customCost = new AudioCost
            {
                Provider = provider,
                OperationType = "text-to-speech",
                Model = model,
                CostPerUnit = customRatePerThousand,
                IsActive = true
            };

            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "text-to-speech", model))
                .ReturnsAsync(customCost);

            // Act
            var result = await _service.CalculateTextToSpeechCostAsync(provider, model, characterCount);

            // Assert
            result.UnitCount.Should().Be(5.0); // 5K chars / 1K
            result.UnitType.Should().Be("1k-characters");
            result.RatePerUnit.Should().Be(customRatePerThousand);
            result.TotalCost.Should().Be(0.25); // 5 * 0.05
        }

        [Fact]
        public async Task CalculateTranscriptionCostAsync_WithNegativeDuration_HandlesAsRefund()
        {
            // Arrange
            var provider = "openai";
            var model = "whisper-1";
            var durationSeconds = -300.0; // -5 minutes (refund scenario)
            
            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync((AudioCost?)null);
            
            // Act
            var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);
            
            // Assert
            result.UnitCount.Should().Be(-5.0); // -5 minutes
            result.RatePerUnit.Should().Be(0.006m);
            result.TotalCost.Should().Be(-0.03); // -5 * 0.006
            result.IsEstimate.Should().BeFalse();
        }

        [Fact]
        public async Task CalculateTextToSpeechCostAsync_WithNegativeCharacterCount_CalculatesNegativeCost()
        {
            // Arrange
            var provider = "elevenlabs";
            var model = "eleven_multilingual_v2";
            var characterCount = -100000; // -100K characters
            
            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "text-to-speech", model))
                .ReturnsAsync((AudioCost?)null);
            
            // Act
            var result = await _service.CalculateTextToSpeechCostAsync(provider, model, characterCount);
            
            // Assert
            result.UnitCount.Should().Be(-100000); // Characters as-is
            result.RatePerUnit.Should().Be(0.00006m);
            result.TotalCost.Should().Be(-6.0); // -100000 * 0.00006
        }

        [Fact]
        public async Task CalculateRealtimeCostAsync_WithNegativeAudioSeconds_HandlesCorrectly()
        {
            // Arrange
            var provider = "openai";
            var model = "gpt-4o-realtime-preview";
            var inputAudioSeconds = -120.0; // -2 minutes
            var outputAudioSeconds = 60.0;  // 1 minute
            
            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "realtime", model))
                .ReturnsAsync((AudioCost?)null);
            
            // Act
            var result = await _service.CalculateRealtimeCostAsync(
                provider, model, inputAudioSeconds, outputAudioSeconds);
            
            // Assert
            result.UnitCount.Should().Be(-1.0); // -2 + 1 = -1 minute
            // Audio cost: (-2 * 0.10) + (1 * 0.20) = -0.20 + 0.20 = 0.00
            result.TotalCost.Should().Be(0.00);
        }

        [Fact]
        public async Task CalculateRealtimeCostAsync_WithNegativeTokens_CalculatesNegativeTokenCost()
        {
            // Arrange
            var provider = "openai";
            var model = "gpt-4o-realtime-preview";
            var inputAudioSeconds = 60.0;
            var outputAudioSeconds = 60.0;
            var inputTokens = -1000; // Negative tokens
            var outputTokens = 500;
            
            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "realtime", model))
                .ReturnsAsync((AudioCost?)null);
            
            // Act
            var result = await _service.CalculateRealtimeCostAsync(
                provider, model, inputAudioSeconds, outputAudioSeconds, inputTokens, outputTokens);
            
            // Assert
            result.UnitCount.Should().Be(2.0); // 1 + 1 = 2 minutes
            // Audio cost: (1 * 0.10) + (1 * 0.20) = 0.10 + 0.20 = 0.30
            // Token cost: (-1000 * 0.000005) + (500 * 0.000015) = -0.005 + 0.0075 = 0.0025
            // Total: 0.30 + 0.0025 = 0.3025
            result.TotalCost.Should().BeApproximately(0.3025, 0.0001);
        }

        [Fact]
        public async Task CalculateRealtimeCostAsync_WithAllNegativeValues_CalculatesNegativeTotal()
        {
            // Arrange
            var provider = "openai";
            var model = "gpt-4o-realtime-preview";
            var inputAudioSeconds = -180.0; // -3 minutes
            var outputAudioSeconds = -120.0; // -2 minutes
            var inputTokens = -1000;
            var outputTokens = -2000;
            
            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "realtime", model))
                .ReturnsAsync((AudioCost?)null);
            
            // Act
            var result = await _service.CalculateRealtimeCostAsync(
                provider, model, inputAudioSeconds, outputAudioSeconds, inputTokens, outputTokens);
            
            // Assert
            result.UnitCount.Should().Be(-5.0); // -3 + -2 = -5 minutes
            // Audio cost: (-3 * 0.10) + (-2 * 0.20) = -0.30 - 0.40 = -0.70
            // Token cost: (-1000 * 0.000005) + (-2000 * 0.000015) = -0.005 - 0.03 = -0.035
            // Total: -0.70 - 0.035 = -0.735
            result.TotalCost.Should().BeApproximately(-0.735, 0.0001);
        }

        [Fact]
        public async Task CalculateTranscriptionCostAsync_WithNegativeMinimumCharge_AppliesCorrectly()
        {
            // Arrange
            var provider = "custom";
            var model = "custom-stt";
            var durationSeconds = -30.0; // -0.5 minutes
            
            var customCost = new AudioCost
            {
                Provider = provider,
                OperationType = "transcription",
                Model = model,
                CostPerUnit = 0.01m,
                MinimumCharge = 0.10m, // Minimum charge for positive values
                IsActive = true
            };
            
            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync(customCost);
            
            // Act
            var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);
            
            // Assert
            // For negative values, minimum charge should not apply
            result.TotalCost.Should().Be(-0.005); // -0.5 * 0.01 = -0.005
        }

        // TODO: Fix decimal/double conversion issues in this test
        // [Theory]
        // [InlineData(-60.0, -1.0, 0.006, -0.006)] // -1 minute
        // [InlineData(-3600.0, -60.0, 0.006, -0.36)] // -1 hour
        // [InlineData(0.0, 0.0, 0.006, 0.0)] // Zero duration
        // [InlineData(-0.5, -0.00833333, 0.006, -0.00005)] // Very small negative
        // public async Task CalculateTranscriptionCostAsync_WithVariousNegativeDurations_CalculatesCorrectly(
        //     double durationSeconds, double expectedMinutes, decimal rate, decimal expectedCost)
        // {
        //     // Arrange
        //     var provider = "openai";
        //     var model = "whisper-1";
        //     
        //     _audioCostRepositoryMock
        //         .Setup(x => x.GetCurrentCostAsync(provider, "transcription", model))
        //         .ReturnsAsync((AudioCost?)null);
        //     
        //     // Act
        //     var result = await _service.CalculateTranscriptionCostAsync(provider, model, durationSeconds);
        //     
        //     // Assert
        //     Math.Abs(result.UnitCount - expectedMinutes).Should().BeLessThan(0.00001);
        //     result.RatePerUnit.Should().Be(rate);
        //     result.TotalCost.Should().Be(expectedCost);
        // }

        [Fact]
        public async Task CalculateAudioCostAsync_WithNegativeDurationForRealtime_SplitsEvenly()
        {
            // Arrange
            var provider = "openai";
            var model = "gpt-4o-realtime-preview";
            var durationSeconds = -120.0; // -2 minutes total
            
            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "realtime", model))
                .ReturnsAsync((AudioCost?)null);
            
            // Act
            var result = await _service.CalculateAudioCostAsync(
                provider, "realtime", model, durationSeconds, 0);
            
            // Assert
            // Should split evenly: -60s input, -60s output (-1 min each)
            // (-1 * 0.10) + (-1 * 0.20) = -0.10 - 0.20 = -0.30
            result.TotalCost.Should().Be(-0.30);
            result.DetailedBreakdown!["input_minutes"].Should().Be(-1.0);
            result.DetailedBreakdown!["output_minutes"].Should().Be(-1.0);
        }

        [Fact]
        public async Task CalculateTextToSpeechCostAsync_WithZeroCharacters_ReturnsZeroCost()
        {
            // Arrange
            var provider = "openai";
            var model = "tts-1";
            var characterCount = 0;
            
            _audioCostRepositoryMock
                .Setup(x => x.GetCurrentCostAsync(provider, "text-to-speech", model))
                .ReturnsAsync((AudioCost?)null);
            
            // Act
            var result = await _service.CalculateTextToSpeechCostAsync(provider, model, characterCount);
            
            // Assert
            result.UnitCount.Should().Be(0.0);
            result.TotalCost.Should().Be(0.0);
        }

        #region Refund Method Tests

        [Fact]
        public async Task CalculateTranscriptionRefundAsync_WithValidInputs_CalculatesCorrectRefund()
        {
            // Arrange
            var provider = "openai";
            var model = "whisper-1";
            var originalDurationSeconds = 600.0; // 10 minutes
            var refundDurationSeconds = 300.0; // 5 minutes
            var refundReason = "Transcription quality issue";
            var originalTransactionId = "txn_audio_123";

            // Act
            var result = await _service.CalculateTranscriptionRefundAsync(
                provider, model, originalDurationSeconds, refundDurationSeconds, 
                refundReason, originalTransactionId, "test-key");

            // Assert
            result.Should().NotBeNull();
            result.Provider.Should().Be(provider);
            result.Operation.Should().Be("transcription");
            result.Model.Should().Be(model);
            result.OriginalAmount.Should().Be(10.0); // 600/60 = 10 minutes
            result.RefundAmount.Should().Be(5.0); // 300/60 = 5 minutes
            result.TotalRefund.Should().Be(0.03); // 5 * 0.006
            result.RefundReason.Should().Be(refundReason);
            result.OriginalTransactionId.Should().Be(originalTransactionId);
            result.VirtualKey.Should().Be("test-key");
            result.IsPartialRefund.Should().BeFalse();
            result.ValidationMessages.Should().BeEmpty();
        }

        [Fact]
        public async Task CalculateTranscriptionRefundAsync_WithEmptyRefundReason_ReturnsValidationError()
        {
            // Act
            var result = await _service.CalculateTranscriptionRefundAsync(
                "openai", "whisper-1", 600.0, 300.0, "");

            // Assert
            result.Should().NotBeNull();
            result.TotalRefund.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund reason is required.");
        }

        [Fact]
        public async Task CalculateTranscriptionRefundAsync_WithNegativeDuration_ReturnsValidationError()
        {
            // Act
            var result = await _service.CalculateTranscriptionRefundAsync(
                "openai", "whisper-1", 600.0, -300.0, "Test refund");

            // Assert
            result.Should().NotBeNull();
            result.TotalRefund.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund duration must be non-negative.");
        }

        [Fact]
        public async Task CalculateTranscriptionRefundAsync_WithRefundExceedingOriginal_CapsRefund()
        {
            // Act
            var result = await _service.CalculateTranscriptionRefundAsync(
                "openai", "whisper-1", 600.0, 900.0, "Excessive refund test");

            // Assert
            result.Should().NotBeNull();
            result.IsPartialRefund.Should().BeTrue();
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund duration (900s) cannot exceed original duration (600s)"));
            result.TotalRefund.Should().Be(0.06); // Capped at 10 minutes * 0.006
        }

        [Fact]
        public async Task CalculateTextToSpeechRefundAsync_WithValidInputs_CalculatesCorrectRefund()
        {
            // Arrange
            var provider = "openai";
            var model = "tts-1-hd";
            var originalCharacterCount = 10000;
            var refundCharacterCount = 5000;
            var refundReason = "Poor audio quality";
            var voice = "alloy";

            // Act
            var result = await _service.CalculateTextToSpeechRefundAsync(
                provider, model, originalCharacterCount, refundCharacterCount,
                refundReason, "txn_tts_123", voice, "test-key");

            // Assert
            result.Should().NotBeNull();
            result.Provider.Should().Be(provider);
            result.Operation.Should().Be("text-to-speech");
            result.Model.Should().Be(model);
            result.OriginalAmount.Should().Be(10.0); // 10k chars
            result.RefundAmount.Should().Be(5.0); // 5k chars
            result.TotalRefund.Should().Be(0.15); // 5 * 0.03 (0.00003 * 1000)
            result.Voice.Should().Be(voice);
            result.IsPartialRefund.Should().BeFalse();
        }

        [Fact]
        public async Task CalculateTextToSpeechRefundAsync_WithNegativeCharacters_ReturnsValidationError()
        {
            // Act
            var result = await _service.CalculateTextToSpeechRefundAsync(
                "openai", "tts-1", 10000, -5000, "Test refund");

            // Assert
            result.Should().NotBeNull();
            result.TotalRefund.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund character count must be non-negative.");
        }

        [Fact]
        public async Task CalculateRealtimeRefundAsync_WithValidInputs_CalculatesAudioAndTokenRefunds()
        {
            // Arrange
            var provider = "openai";
            var model = "gpt-4o-realtime-preview";
            var originalInputAudioSeconds = 300.0;
            var refundInputAudioSeconds = 150.0;
            var originalOutputAudioSeconds = 200.0;
            var refundOutputAudioSeconds = 100.0;
            var originalInputTokens = 1000;
            var refundInputTokens = 500;
            var originalOutputTokens = 2000;
            var refundOutputTokens = 1000;
            var refundReason = "Connection dropped";

            // Act
            var result = await _service.CalculateRealtimeRefundAsync(
                provider, model,
                originalInputAudioSeconds, refundInputAudioSeconds,
                originalOutputAudioSeconds, refundOutputAudioSeconds,
                originalInputTokens, refundInputTokens,
                originalOutputTokens, refundOutputTokens,
                refundReason, "txn_realtime_123", "test-key");

            // Assert
            result.Should().NotBeNull();
            result.Provider.Should().Be(provider);
            result.Operation.Should().Be("realtime");
            result.Model.Should().Be(model);
            
            // Audio refund: (150/60 * 0.1) + (100/60 * 0.2) = 0.25 + 0.333... = 0.583...
            // Token refund: (500 * 0.000005) + (1000 * 0.000015) = 0.0025 + 0.015 = 0.0175
            // Total: ~0.601
            result.TotalRefund.Should().BeApproximately(0.601, 0.001);
            
            result.DetailedBreakdown.Should().NotBeNull();
            result.DetailedBreakdown!["audio_refund"].Should().BeApproximately(0.583, 0.001);
            result.DetailedBreakdown["token_refund"].Should().BeApproximately(0.0175, 0.0001);
            result.DetailedBreakdown["refund_input_minutes"].Should().Be(2.5);
            result.DetailedBreakdown["refund_output_minutes"].Should().BeApproximately(1.667, 0.001);
            result.DetailedBreakdown["refund_input_tokens"].Should().Be(500);
            result.DetailedBreakdown["refund_output_tokens"].Should().Be(1000);
        }

        [Fact]
        public async Task CalculateRealtimeRefundAsync_WithNegativeAudioSeconds_ReturnsValidationError()
        {
            // Act
            var result = await _service.CalculateRealtimeRefundAsync(
                "openai", "gpt-4o-realtime-preview",
                300.0, -150.0, 200.0, 100.0,
                refundReason: "Test refund");

            // Assert
            result.Should().NotBeNull();
            result.TotalRefund.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund audio durations must be non-negative.");
        }

        [Fact]
        public async Task CalculateRealtimeRefundAsync_WithExcessiveTokens_CapsAndMarksPartial()
        {
            // Act
            var result = await _service.CalculateRealtimeRefundAsync(
                "openai", "gpt-4o-realtime-preview",
                300.0, 150.0, 200.0, 100.0,
                1000, 2000, 2000, 3000, // Refund tokens exceed original
                "Excessive refund test");

            // Assert
            result.Should().NotBeNull();
            result.IsPartialRefund.Should().BeTrue();
            result.ValidationMessages.Should().HaveCount(2);
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund input tokens (2000) cannot exceed original (1000)"));
            result.ValidationMessages.Should().Contain(m => m.Contains("Refund output tokens (3000) cannot exceed original (2000)"));
        }

        [Fact]
        public async Task CalculateTranscriptionRefundAsync_WithCustomRate_UsesCustomPricing()
        {
            // Arrange
            var provider = "custom-provider";
            var model = "custom-model";
            var customRate = 0.02m; // $0.02 per minute

            _audioCostRepositoryMock.Setup(r => r.GetCurrentCostAsync(provider, "transcription", model))
                .ReturnsAsync(new AudioCost
                {
                    Provider = provider,
                    OperationType = "transcription",
                    Model = model,
                    CostPerUnit = customRate,
                    IsActive = true
                });

            // Act
            var result = await _service.CalculateTranscriptionRefundAsync(
                provider, model, 600.0, 300.0, "Custom rate refund");

            // Assert
            result.Should().NotBeNull();
            result.TotalRefund.Should().Be(0.1); // 5 minutes * 0.02
        }

        [Fact]
        public async Task CalculateTextToSpeechRefundAsync_WithUnknownModel_UsesDefaultRate()
        {
            // Act
            var result = await _service.CalculateTextToSpeechRefundAsync(
                "unknown-provider", "unknown-model", 10000, 5000, "Default rate test");

            // Assert
            result.Should().NotBeNull();
            result.TotalRefund.Should().Be(0.15); // 5 * 0.03 (default rate)
        }

        [Fact]
        public async Task CalculateRealtimeRefundAsync_WithoutTokenSupport_CalculatesAudioOnly()
        {
            // Arrange
            var provider = "ultravox";
            var model = "fixie-ai/ultravox-70b";

            // Act
            var result = await _service.CalculateRealtimeRefundAsync(
                provider, model,
                300.0, 150.0, 200.0, 100.0,
                refundReason: "Audio-only refund");

            // Assert
            result.Should().NotBeNull();
            result.DetailedBreakdown!["audio_refund"].Should().BeApproximately(0.0042, 0.0001); // ~4.17 minutes * 0.001
            result.DetailedBreakdown.Should().NotContainKey("token_refund");
        }

        [Fact]
        public async Task CalculateRealtimeRefundAsync_WithEmptyRefundReason_ReturnsValidationError()
        {
            // Act
            var result = await _service.CalculateRealtimeRefundAsync(
                "openai", "gpt-4o-realtime-preview",
                300.0, 150.0, 200.0, 100.0,
                refundReason: "");

            // Assert
            result.Should().NotBeNull();
            result.TotalRefund.Should().Be(0);
            result.ValidationMessages.Should().Contain("Refund reason is required.");
        }

        #endregion
    }
    */
}