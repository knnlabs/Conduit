using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests;

/// <summary>
/// Basic integration tests for audio functionality.
/// </summary>
public class AudioIntegrationBasicTests
{
    [Fact]
    public void AudioCapabilityDetector_SupportsOpenAITranscription()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AudioCapabilityDetector>>();
        var capabilityServiceMock = new Mock<IModelCapabilityService>();
        capabilityServiceMock.Setup(x => x.SupportsAudioTranscriptionAsync("whisper-1"))
            .ReturnsAsync(true);
        var detector = new AudioCapabilityDetector(loggerMock.Object, capabilityServiceMock.Object);

        // Act
        var supports = detector.SupportsTranscription("openai", "whisper-1");

        // Assert
        Assert.True(supports);
    }

    [Fact]
    public void AudioCapabilityDetector_SupportsOpenAITextToSpeech()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AudioCapabilityDetector>>();
        var capabilityServiceMock = new Mock<IModelCapabilityService>();
        capabilityServiceMock.Setup(x => x.SupportsTextToSpeechAsync("tts-1"))
            .ReturnsAsync(true);
        var detector = new AudioCapabilityDetector(loggerMock.Object, capabilityServiceMock.Object);

        // Act
        var supports = detector.SupportsTextToSpeech("openai", "tts-1");

        // Assert
        Assert.True(supports);
    }

    [Fact]
    public void AudioCapabilityDetector_DoesNotSupportUnknownProvider()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AudioCapabilityDetector>>();
        var capabilityServiceMock = new Mock<IModelCapabilityService>();
        var detector = new AudioCapabilityDetector(loggerMock.Object, capabilityServiceMock.Object);

        // Act
        var supportsTranscription = detector.SupportsTranscription("unknown-provider");
        var supportsTts = detector.SupportsTextToSpeech("unknown-provider");

        // Assert
        Assert.False(supportsTranscription);
        Assert.False(supportsTts);
    }

    [Fact]
    public void AudioTranscriptionRequest_ValidatesCorrectly()
    {
        // Arrange
        var request = new AudioTranscriptionRequest
        {
            AudioData = Encoding.UTF8.GetBytes("fake-audio-data"),
            FileName = "test.mp3",
            AudioFormat = AudioFormat.Mp3,
            Model = "whisper-1"
        };

        // Act
        var isValid = request.IsValid(out var errorMessage);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void AudioTranscriptionRequest_FailsValidationWithoutData()
    {
        // Arrange
        var request = new AudioTranscriptionRequest
        {
            Model = "whisper-1"
        };

        // Act
        var isValid = request.IsValid(out var errorMessage);

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("AudioData or AudioUrl must be provided", errorMessage);
    }

    [Fact]
    public void TextToSpeechRequest_ValidatesCorrectly()
    {
        // Arrange
        var request = new TextToSpeechRequest
        {
            Input = "Hello, world!",
            Model = "tts-1",
            Voice = "alloy"
        };

        // Act
        var isValid = request.IsValid(out var errorMessage);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void AudioFormat_EnumHasExpectedValues()
    {
        // Assert basic audio formats exist
        Assert.True(System.Enum.IsDefined(typeof(AudioFormat), AudioFormat.Mp3));
        Assert.True(System.Enum.IsDefined(typeof(AudioFormat), AudioFormat.Wav));
        Assert.True(System.Enum.IsDefined(typeof(AudioFormat), AudioFormat.Flac));
        Assert.True(System.Enum.IsDefined(typeof(AudioFormat), AudioFormat.Opus));
    }
}