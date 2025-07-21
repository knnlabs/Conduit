using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class HybridAudioControllerTests : ControllerTestBase
    {
        private readonly Mock<IHybridAudioService> _mockHybridAudioService;
        private readonly Mock<ConduitLLM.Configuration.Services.IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ILogger<HybridAudioController>> _mockLogger;
        private readonly HybridAudioController _controller;

        public HybridAudioControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockHybridAudioService = new Mock<IHybridAudioService>();
            _mockVirtualKeyService = new Mock<ConduitLLM.Configuration.Services.IVirtualKeyService>();
            _mockLogger = CreateLogger<HybridAudioController>();

            _controller = new HybridAudioController(
                _mockHybridAudioService.Object,
                _mockVirtualKeyService.Object,
                _mockLogger.Object);

            _controller.ControllerContext = CreateControllerContext();
        }

        #region ProcessAudio Tests

        [Fact]
        public async Task ProcessAudio_WithValidRequest_ShouldReturnAudioFile()
        {
            // Arrange
            var audioContent = Encoding.UTF8.GetBytes("test audio content");
            var formFile = CreateFormFile("test.mp3", audioContent, "audio/mpeg");
            
            var response = new HybridAudioResponse
            {
                AudioData = Encoding.UTF8.GetBytes("response audio"),
                AudioFormat = "mp3",
                TranscribedText = "Hello",
                ResponseText = "Hi there!",
                DurationSeconds = 2.5,
                Metrics = new ProcessingMetrics
                {
                    InputDurationSeconds = 1.5,
                    OutputDurationSeconds = 2.5
                }
            };

            _mockHybridAudioService.Setup(x => x.ProcessAudioAsync(
                    It.Is<HybridAudioRequest>(r => 
                        r.AudioData.Length == audioContent.Length &&
                        r.AudioFormat == "mp3" &&
                        r.Temperature == 0.7 &&
                        r.MaxTokens == 150),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ProcessAudio(formFile);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("audio/mpeg", fileResult.ContentType);
            Assert.Equal("response.mp3", fileResult.FileDownloadName);
            Assert.Equal(response.AudioData, fileResult.FileContents);
        }

        [Fact]
        public async Task ProcessAudio_WithAllParameters_ShouldPassCorrectRequest()
        {
            // Arrange
            var audioContent = Encoding.UTF8.GetBytes("test audio");
            var formFile = CreateFormFile("test.wav", audioContent, "audio/wav");
            var sessionId = "session-123";
            var language = "es";
            var systemPrompt = "Be helpful";
            var voiceId = "voice-1";
            var outputFormat = "wav";
            var temperature = 1.2;
            var maxTokens = 300;

            var response = new HybridAudioResponse
            {
                AudioData = new byte[] { 1, 2, 3 },
                AudioFormat = outputFormat
            };

            HybridAudioRequest capturedRequest = null;
            _mockHybridAudioService.Setup(x => x.ProcessAudioAsync(
                    It.IsAny<HybridAudioRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<HybridAudioRequest, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ProcessAudio(
                formFile, 
                sessionId, 
                language, 
                systemPrompt, 
                voiceId, 
                outputFormat, 
                temperature, 
                maxTokens);

            // Assert
            Assert.IsType<FileContentResult>(result);
            Assert.NotNull(capturedRequest);
            Assert.Equal(sessionId, capturedRequest.SessionId);
            Assert.Equal("wav", capturedRequest.AudioFormat);
            Assert.Equal(language, capturedRequest.Language);
            Assert.Equal(systemPrompt, capturedRequest.SystemPrompt);
            Assert.Equal(voiceId, capturedRequest.VoiceId);
            Assert.Equal(outputFormat, capturedRequest.OutputFormat);
            Assert.Equal(temperature, capturedRequest.Temperature);
            Assert.Equal(maxTokens, capturedRequest.MaxTokens);
            Assert.False(capturedRequest.EnableStreaming);
        }

        [Fact]
        public async Task ProcessAudio_WithoutFile_ShouldReturnBadRequest()
        {
            // Arrange & Act
            var result = await _controller.ProcessAudio(null);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic error = badRequestResult.Value;
            Assert.Equal("No audio file provided", error.error.ToString());
        }

        [Fact]
        public async Task ProcessAudio_WithEmptyFile_ShouldReturnBadRequest()
        {
            // Arrange
            var formFile = CreateFormFile("empty.mp3", Array.Empty<byte>(), "audio/mpeg");

            // Act
            var result = await _controller.ProcessAudio(formFile);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic error = badRequestResult.Value;
            Assert.Equal("No audio file provided", error.error.ToString());
        }

        [Fact]
        public async Task ProcessAudio_WithVirtualKey_ShouldCheckPermissions()
        {
            // Arrange
            var audioContent = new byte[] { 1, 2, 3 };
            var formFile = CreateFormFile("test.mp3", audioContent, "audio/mpeg");
            var virtualKey = "vk-test-key";
            
            _controller.HttpContext.Items["ApiKey"] = virtualKey;
            
            var keyEntity = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyHash = "test-hash"
            };

            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByKeyValueAsync(virtualKey))
                .ReturnsAsync(keyEntity);

            _mockHybridAudioService.Setup(x => x.ProcessAudioAsync(
                    It.IsAny<HybridAudioRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HybridAudioResponse { AudioData = new byte[] { 4, 5, 6 }, AudioFormat = "mp3" });

            // Act
            var result = await _controller.ProcessAudio(formFile);

            // Assert
            Assert.IsType<FileContentResult>(result);
            _mockVirtualKeyService.Verify(x => x.GetVirtualKeyByKeyValueAsync(virtualKey), Times.Once);
        }

        [Fact]
        public async Task ProcessAudio_WithInvalidVirtualKey_ShouldReturnForbidden()
        {
            // Arrange
            var audioContent = new byte[] { 1, 2, 3 };
            var formFile = CreateFormFile("test.mp3", audioContent, "audio/mpeg");
            var virtualKey = "vk-invalid-key";
            
            _controller.HttpContext.Items["ApiKey"] = virtualKey;
            
            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByKeyValueAsync(virtualKey))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await _controller.ProcessAudio(formFile);

            // Assert
            var forbidResult = Assert.IsType<ForbidResult>(result);
            Assert.Equal("Virtual key is not valid or enabled", forbidResult.AuthenticationSchemes[0]);
        }

        [Fact]
        public async Task ProcessAudio_WithDisabledVirtualKey_ShouldReturnForbidden()
        {
            // Arrange
            var audioContent = new byte[] { 1, 2, 3 };
            var formFile = CreateFormFile("test.mp3", audioContent, "audio/mpeg");
            var virtualKey = "vk-disabled-key";
            
            _controller.HttpContext.Items["ApiKey"] = virtualKey;
            
            var keyEntity = new VirtualKey
            {
                Id = 1,
                IsEnabled = false,
                KeyHash = "test-hash"
            };

            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByKeyValueAsync(virtualKey))
                .ReturnsAsync(keyEntity);

            // Act
            var result = await _controller.ProcessAudio(formFile);

            // Assert
            var forbidResult = Assert.IsType<ForbidResult>(result);
            Assert.Equal("Virtual key is not valid or enabled", forbidResult.AuthenticationSchemes[0]);
        }

        [Fact]
        public async Task ProcessAudio_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var audioContent = new byte[] { 1, 2, 3 };
            var formFile = CreateFormFile("test.mp3", audioContent, "audio/mpeg");
            var errorMessage = "Invalid audio format";

            _mockHybridAudioService.Setup(x => x.ProcessAudioAsync(
                    It.IsAny<HybridAudioRequest>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException(errorMessage));

            // Act
            var result = await _controller.ProcessAudio(formFile);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic error = badRequestResult.Value;
            Assert.Equal(errorMessage, error.error.ToString());
        }

        [Fact]
        public async Task ProcessAudio_WithGeneralException_ShouldReturn500()
        {
            // Arrange
            var audioContent = new byte[] { 1, 2, 3 };
            var formFile = CreateFormFile("test.mp3", audioContent, "audio/mpeg");

            _mockHybridAudioService.Setup(x => x.ProcessAudioAsync(
                    It.IsAny<HybridAudioRequest>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Processing failed"));

            // Act
            var result = await _controller.ProcessAudio(formFile);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("An error occurred processing the audio", error.error.ToString());
        }

        [Theory]
        [InlineData("audio/mpeg", "test.mp3", "mp3")]
        [InlineData("audio/wav", "test.wav", "wav")]
        [InlineData("audio/webm", "test.webm", "webm")]
        [InlineData("audio/flac", "test.flac", "flac")]
        [InlineData("audio/ogg", "test.ogg", "ogg")]
        [InlineData("application/octet-stream", "test.mp3", "mp3")]
        [InlineData(null, "test.wav", "wav")]
        [InlineData("unknown/type", "test.mp3", "mp3")]
        [InlineData("unknown/type", "test", "mp3")]  // Test edge case - file with no extension defaults to mp3
        public async Task ProcessAudio_ShouldDetectCorrectAudioFormat(string contentType, string fileName, string expectedFormat)
        {
            // Arrange
            var audioContent = new byte[] { 1, 2, 3 };
            var formFile = CreateFormFile(fileName, audioContent, contentType);

            HybridAudioRequest capturedRequest = null;
            _mockHybridAudioService.Setup(x => x.ProcessAudioAsync(
                    It.IsAny<HybridAudioRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<HybridAudioRequest, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HybridAudioResponse { AudioData = new byte[] { 4, 5, 6 }, AudioFormat = "mp3" });

            // Act
            await _controller.ProcessAudio(formFile);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(expectedFormat, capturedRequest.AudioFormat);
        }

        #endregion

        #region CreateSession Tests

        [Fact]
        public async Task CreateSession_WithValidConfig_ShouldReturnSessionId()
        {
            // Arrange
            var config = new HybridSessionConfig
            {
                SttProvider = "whisper",
                LlmModel = "gpt-4",
                TtsProvider = "elevenlabs",
                SystemPrompt = "Be helpful",
                DefaultVoice = "voice-1"
            };

            var sessionId = "session-" + Guid.NewGuid();
            _mockHybridAudioService.Setup(x => x.CreateSessionAsync(config, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessionId);

            // Act
            var result = await _controller.CreateSession(config);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<HybridAudioController.CreateSessionResponse>(okResult.Value);
            Assert.Equal(sessionId, response.SessionId);
        }

        [Fact]
        public async Task CreateSession_WithVirtualKey_ShouldCheckPermissions()
        {
            // Arrange
            var config = new HybridSessionConfig();
            var virtualKey = "vk-test-key";
            
            _controller.HttpContext.Items["ApiKey"] = virtualKey;
            
            var keyEntity = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyHash = "test-hash"
            };

            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByKeyValueAsync(virtualKey))
                .ReturnsAsync(keyEntity);

            _mockHybridAudioService.Setup(x => x.CreateSessionAsync(
                    It.IsAny<HybridSessionConfig>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("session-123");

            // Act
            var result = await _controller.CreateSession(config);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockVirtualKeyService.Verify(x => x.GetVirtualKeyByKeyValueAsync(virtualKey), Times.Once);
        }

        [Fact]
        public async Task CreateSession_WithInvalidVirtualKey_ShouldReturnForbidden()
        {
            // Arrange
            var config = new HybridSessionConfig();
            var virtualKey = "vk-invalid-key";
            
            _controller.HttpContext.Items["ApiKey"] = virtualKey;
            
            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByKeyValueAsync(virtualKey))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await _controller.CreateSession(config);

            // Assert
            var forbidResult = Assert.IsType<ForbidResult>(result);
            Assert.Equal("Virtual key is not valid or enabled", forbidResult.AuthenticationSchemes[0]);
        }

        [Fact]
        public async Task CreateSession_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var config = new HybridSessionConfig();
            var errorMessage = "Invalid configuration";

            _mockHybridAudioService.Setup(x => x.CreateSessionAsync(
                    It.IsAny<HybridSessionConfig>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException(errorMessage));

            // Act
            var result = await _controller.CreateSession(config);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic error = badRequestResult.Value;
            Assert.Equal(errorMessage, error.error.ToString());
        }

        [Fact]
        public async Task CreateSession_WithGeneralException_ShouldReturn500()
        {
            // Arrange
            var config = new HybridSessionConfig();

            _mockHybridAudioService.Setup(x => x.CreateSessionAsync(
                    It.IsAny<HybridSessionConfig>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.CreateSession(config);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("An error occurred creating the session", error.error.ToString());
        }

        #endregion

        #region CloseSession Tests

        [Fact]
        public async Task CloseSession_WithValidSessionId_ShouldReturnNoContent()
        {
            // Arrange
            var sessionId = "session-123";

            _mockHybridAudioService.Setup(x => x.CloseSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.CloseSession(sessionId);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockHybridAudioService.Verify(x => x.CloseSessionAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CloseSession_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var sessionId = "invalid-session";
            var errorMessage = "Session not found";

            _mockHybridAudioService.Setup(x => x.CloseSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException(errorMessage));

            // Act
            var result = await _controller.CloseSession(sessionId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic error = badRequestResult.Value;
            Assert.Equal(errorMessage, error.error.ToString());
        }

        [Fact]
        public async Task CloseSession_WithGeneralException_ShouldReturn500()
        {
            // Arrange
            var sessionId = "session-123";

            _mockHybridAudioService.Setup(x => x.CloseSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.CloseSession(sessionId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("An error occurred closing the session", error.error.ToString());
        }

        #endregion

        #region GetStatus Tests

        [Fact]
        public async Task GetStatus_WhenServiceAvailable_ShouldReturnStatusWithMetrics()
        {
            // Arrange
            var metrics = new HybridLatencyMetrics
            {
                AverageSttLatencyMs = 100,
                AverageLlmLatencyMs = 200,
                AverageTtsLatencyMs = 150,
                AverageTotalLatencyMs = 450,
                P95LatencyMs = 600,
                P99LatencyMs = 800,
                SampleCount = 100
            };

            _mockHybridAudioService.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockHybridAudioService.Setup(x => x.GetLatencyMetricsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(metrics);

            // Act
            var result = await _controller.GetStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var status = Assert.IsType<HybridAudioController.ServiceStatus>(okResult.Value);
            Assert.True(status.Available);
            Assert.NotNull(status.LatencyMetrics);
            Assert.Equal(metrics.AverageTotalLatencyMs, status.LatencyMetrics.AverageTotalLatencyMs);
        }

        [Fact]
        public async Task GetStatus_WhenServiceUnavailable_ShouldReturnUnavailableStatus()
        {
            // Arrange
            _mockHybridAudioService.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mockHybridAudioService.Setup(x => x.GetLatencyMetricsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HybridLatencyMetrics());

            // Act
            var result = await _controller.GetStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var status = Assert.IsType<HybridAudioController.ServiceStatus>(okResult.Value);
            Assert.False(status.Available);
        }

        [Fact]
        public async Task GetStatus_WhenExceptionOccurs_ShouldReturnUnavailableStatus()
        {
            // Arrange
            _mockHybridAudioService.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service check failed"));

            // Act
            var result = await _controller.GetStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var status = Assert.IsType<HybridAudioController.ServiceStatus>(okResult.Value);
            Assert.False(status.Available);
            Assert.Null(status.LatencyMetrics);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullHybridAudioService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new HybridAudioController(
                null,
                _mockVirtualKeyService.Object,
                _mockLogger.Object));
            Assert.Equal("hybridAudioService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullVirtualKeyService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new HybridAudioController(
                _mockHybridAudioService.Object,
                null,
                _mockLogger.Object));
            Assert.Equal("virtualKeyService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new HybridAudioController(
                _mockHybridAudioService.Object,
                _mockVirtualKeyService.Object,
                null));
            Assert.Equal("logger", ex.ParamName);
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(HybridAudioController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
            var authAttribute = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)authorizeAttribute;
            Assert.Equal("MasterKeyOrVirtualKey", authAttribute.Policy);
        }

        #endregion

        #region Helper Methods

        private IFormFile CreateFormFile(string fileName, byte[] content, string contentType)
        {
            var stream = new MemoryStream(content);
            var formFile = new FormFile(stream, 0, content.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
            return formFile;
        }

        #endregion
    }
}