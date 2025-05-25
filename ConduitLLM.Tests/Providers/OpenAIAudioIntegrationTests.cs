using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using ItExpr = Moq.Protected.ItExpr;

namespace ConduitLLM.Tests.Providers;

/// <summary>
/// Integration tests for OpenAI audio functionality.
/// These tests verify that the OpenAI client correctly implements audio interfaces.
/// </summary>
public class OpenAIAudioIntegrationTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<OpenAIClient>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly ProviderCredentials _openAICredentials;

    public OpenAIAudioIntegrationTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _loggerMock = new Mock<ILogger<OpenAIClient>>();
        
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(_httpClient);

        _openAICredentials = new ProviderCredentials
        {
            ProviderName = "OpenAI",
            ApiKey = "test-api-key",
            ApiBase = "https://api.openai.com"
        };
    }

    [Fact]
    public async Task TranscribeAudioAsync_BasicRequest_Success()
    {
        // Arrange
        var responseContent = new
        {
            text = "Hello, this is a test transcription.",
            language = "en",
            duration = 5.2
        };

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responseContent))
            });

        var client = new OpenAIClient(_openAICredentials, "whisper-1", _loggerMock.Object, _mockHttpClientFactory.Object);
        var request = new AudioTranscriptionRequest
        {
            AudioData = Encoding.UTF8.GetBytes("fake-audio-data"),
            FileName = "test.mp3",
            AudioFormat = AudioFormat.Mp3,
            Model = "whisper-1"
        };

        // Act
        var response = await client.TranscribeAudioAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Hello, this is a test transcription.", response.Text);
        Assert.Equal("en", response.Language);
        Assert.Equal(5.2, response.Duration);
    }

    [Fact]
    public async Task CreateSpeechAsync_BasicRequest_Success()
    {
        // Arrange
        var audioData = new byte[] { 0x1, 0x2, 0x3, 0x4 };
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(audioData)
            });

        var client = new OpenAIClient(_openAICredentials, "tts-1", _loggerMock.Object, _mockHttpClientFactory.Object);
        var request = new TextToSpeechRequest
        {
            Input = "Hello, world!",
            Model = "tts-1",
            Voice = "alloy",
            ResponseFormat = AudioFormat.Mp3
        };

        // Act
        var response = await client.CreateSpeechAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(audioData, response.AudioData);
        Assert.Equal("mp3", response.Format?.ToLower());
    }

    [Fact]
    public async Task SupportsTranscriptionAsync_ReturnsTrue()
    {
        // Arrange
        var client = new OpenAIClient(_openAICredentials, "whisper-1", _loggerMock.Object, _mockHttpClientFactory.Object);

        // Act
        var supports = await client.SupportsTranscriptionAsync();

        // Assert
        Assert.True(supports);
    }

    [Fact]
    public async Task SupportsTextToSpeechAsync_ReturnsTrue()
    {
        // Arrange
        var client = new OpenAIClient(_openAICredentials, "tts-1", _loggerMock.Object, _mockHttpClientFactory.Object);

        // Act
        var supports = await client.SupportsTextToSpeechAsync();

        // Assert
        Assert.True(supports);
    }

    [Fact]
    public async Task ListVoicesAsync_ReturnsExpectedVoices()
    {
        // Arrange
        var client = new OpenAIClient(_openAICredentials, "tts-1", _loggerMock.Object, _mockHttpClientFactory.Object);

        // Act
        var voices = await client.ListVoicesAsync();

        // Assert
        Assert.NotNull(voices);
        Assert.NotEmpty(voices);
        // OpenAI provides specific voices
        Assert.Contains(voices, v => v.VoiceId == "alloy");
        Assert.Contains(voices, v => v.VoiceId == "echo");
        Assert.Contains(voices, v => v.VoiceId == "fable");
    }
}