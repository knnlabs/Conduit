using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static ConduitLLM.Http.Controllers.AudioController;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    public class AudioControllerTests : ControllerTestBase
    {
        private readonly Mock<IAudioRouter> _audioRouterMock;
        private readonly Mock<ConduitLLM.Configuration.Services.IVirtualKeyService> _virtualKeyServiceMock;
        private readonly Mock<ILogger<AudioController>> _loggerMock;
        private readonly AudioController _controller;

        public AudioControllerTests(ITestOutputHelper output) : base(output)
        {
            _audioRouterMock = new Mock<IAudioRouter>();
            _virtualKeyServiceMock = new Mock<ConduitLLM.Configuration.Services.IVirtualKeyService>();
            _loggerMock = CreateLogger<AudioController>();
            
            _controller = new AudioController(
                _audioRouterMock.Object,
                _virtualKeyServiceMock.Object,
                _loggerMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullAudioRouter_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new AudioController(null!, _virtualKeyServiceMock.Object, _loggerMock.Object);
            Assert.Throws<ArgumentNullException>(act);
        }

        [Fact]
        public void Constructor_WithNullVirtualKeyService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new AudioController(_audioRouterMock.Object, (ConduitLLM.Configuration.Services.IVirtualKeyService)null!, _loggerMock.Object);
            Assert.Throws<ArgumentNullException>(act);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new AudioController(_audioRouterMock.Object, _virtualKeyServiceMock.Object, null!);
            Assert.Throws<ArgumentNullException>(act);
        }

        #endregion

        #region TranscribeAudio Tests

        [Fact]
        public async Task TranscribeAudio_WithValidRequest_ReturnsTranscription()
        {
            // Arrange
            var virtualKey = "test-virtual-key";
            var fileContent = "test audio content";
            var fileName = "test.mp3";
            var model = "whisper-1";
            
            var formFile = CreateFormFile(fileContent, fileName);
            var expectedResponse = new AudioTranscriptionResponse
            {
                Text = "This is the transcribed text",
                Language = "en",
                Duration = 10.5
            };

            var mockClient = new Mock<IAudioTranscriptionClient>();
            mockClient.Setup(x => x.TranscribeAudioAsync(
                    It.IsAny<AudioTranscriptionRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            
            _audioRouterMock.Setup(x => x.GetTranscriptionClientAsync(
                    It.IsAny<AudioTranscriptionRequest>(),
                    It.Is<string>(k => k == virtualKey),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);

            // Setup controller context with authenticated user
            _controller.ControllerContext = CreateAuthenticatedContext(virtualKey);

            // Act
            var result = await _controller.TranscribeAudio(formFile, model);

            // Assert
            AssertOkObjectResult<AudioTranscriptionResponse>(result, response =>
            {
                Assert.Equal(expectedResponse.Text, response.Text);
                Assert.Equal(expectedResponse.Language, response.Language);
                Assert.Equal(expectedResponse.Duration, response.Duration);
            });

            _audioRouterMock.Verify(x => x.GetTranscriptionClientAsync(
                It.IsAny<AudioTranscriptionRequest>(),
                It.Is<string>(k => k == virtualKey),
                It.IsAny<CancellationToken>()), Times.Once);
            mockClient.Verify(x => x.TranscribeAudioAsync(
                It.IsAny<AudioTranscriptionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TranscribeAudio_WithMissingVirtualKey_ReturnsUnauthorized()
        {
            // Arrange
            var formFile = CreateFormFile("content", "test.mp3");
            _controller.ControllerContext = CreateControllerContext(); // No authentication

            // Act
            var result = await _controller.TranscribeAudio(formFile);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
            Assert.Equal("Unauthorized", problemDetails.Title);
            Assert.Equal("Invalid or missing API key", problemDetails.Detail);
        }

        [Fact]
        public async Task TranscribeAudio_WithEmptyFile_ReturnsBadRequest()
        {
            // Arrange
            var virtualKey = "test-virtual-key";
            var formFile = CreateFormFile("", "test.mp3"); // Empty content
            _controller.ControllerContext = CreateAuthenticatedContext(virtualKey);

            // Act
            var result = await _controller.TranscribeAudio(formFile);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
            Assert.Equal("Invalid Request", problemDetails.Title);
            Assert.Equal("Audio file is empty", problemDetails.Detail);
        }

        [Fact]
        public async Task TranscribeAudio_WithOversizedFile_ReturnsBadRequest()
        {
            // Arrange
            var virtualKey = "test-virtual-key";
            var largeContent = new string('x', 26 * 1024 * 1024); // 26MB
            var formFile = CreateFormFile(largeContent, "test.mp3");
            _controller.ControllerContext = CreateAuthenticatedContext(virtualKey);

            // Act
            var result = await _controller.TranscribeAudio(formFile);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
            Assert.Equal("Invalid Request", problemDetails.Title);
            Assert.Contains("exceeds maximum size", problemDetails.Detail);
        }

        [Fact]
        public async Task TranscribeAudio_WhenRouterThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var virtualKey = "test-virtual-key";
            var formFile = CreateFormFile("content", "test.mp3");

            _audioRouterMock.Setup(x => x.GetTranscriptionClientAsync(
                    It.IsAny<AudioTranscriptionRequest>(),
                    It.Is<string>(k => k == virtualKey),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Router error"));

            _controller.ControllerContext = CreateAuthenticatedContext(virtualKey);

            // Act
            var result = await _controller.TranscribeAudio(formFile);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("Internal Server Error", problemDetails.Title);
        }

        #endregion

        #region TranslateAudio Tests

        [Fact]
        public async Task TranslateAudio_WithValidRequest_ReturnsTranslation()
        {
            // Arrange
            var virtualKey = "test-virtual-key";
            var fileContent = "test audio content";
            var fileName = "test.mp3";
            var model = "whisper-1";
            
            var formFile = CreateFormFile(fileContent, fileName);
            var expectedResponse = new AudioTranscriptionResponse // TranslateAudio returns AudioTranscriptionResponse
            {
                Text = "This is the translated text"
            };

            var mockClient = new Mock<IAudioTranscriptionClient>();
            mockClient.Setup(x => x.TranscribeAudioAsync(
                    It.IsAny<AudioTranscriptionRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
                
            _audioRouterMock.Setup(x => x.GetTranscriptionClientAsync(
                    It.IsAny<AudioTranscriptionRequest>(),
                    It.Is<string>(k => k == virtualKey),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);

            _controller.ControllerContext = CreateAuthenticatedContext(virtualKey);

            // Act
            var result = await _controller.TranslateAudio(formFile, model);

            // Assert
            AssertOkObjectResult<AudioTranscriptionResponse>(result, response =>
            {
                Assert.Equal(expectedResponse.Text, response.Text);
            });
        }

        [Fact]
        public async Task TranslateAudio_WithMissingVirtualKey_ReturnsUnauthorized()
        {
            // Arrange
            var formFile = CreateFormFile("content", "test.mp3");
            _controller.ControllerContext = CreateControllerContext();

            // Act
            var result = await _controller.TranslateAudio(formFile);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
            Assert.Equal("Unauthorized", problemDetails.Title);
        }

        #endregion

        #region GenerateSpeech Tests

        [Fact]
        public async Task GenerateSpeech_WithValidRequest_ReturnsAudioStream()
        {
            // Arrange
            var virtualKey = "test-virtual-key";
            var request = new TextToSpeechRequestDto
            {
                Model = "tts-1",
                Input = "Hello, world!",
                Voice = "alloy"
            };

            var audioContent = Encoding.UTF8.GetBytes("fake audio data");
            var ttsResponse = new TextToSpeechResponse
            {
                AudioData = audioContent
            };
            
            var mockClient = new Mock<ITextToSpeechClient>();
            mockClient.Setup(x => x.CreateSpeechAsync(
                    It.IsAny<TextToSpeechRequest>(), 
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(ttsResponse);
                
            _audioRouterMock.Setup(x => x.GetTextToSpeechClientAsync(
                    It.IsAny<TextToSpeechRequest>(),
                    It.Is<string>(k => k == virtualKey),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);

            _controller.ControllerContext = CreateAuthenticatedContext(virtualKey);

            // Act
            var result = await _controller.GenerateSpeech(request);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("audio/mpeg", fileResult.ContentType);
            Assert.Equal(audioContent, fileResult.FileContents);
        }

        [Fact]
        public async Task GenerateSpeech_WithMissingVirtualKey_ReturnsUnauthorized()
        {
            // Arrange
            var request = new TextToSpeechRequestDto
            {
                Model = "tts-1",
                Input = "Hello, world!",
                Voice = "alloy"
            };
            _controller.ControllerContext = CreateControllerContext();

            // Act
            var result = await _controller.GenerateSpeech(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
            Assert.Equal("Unauthorized", problemDetails.Title);
        }

        [Fact]
        public async Task GenerateSpeech_WithEmptyInput_ReturnsBadRequest()
        {
            // Arrange
            var virtualKey = "test-virtual-key";
            var request = new TextToSpeechRequestDto
            {
                Model = "tts-1",
                Input = "", // Empty input
                Voice = "alloy"
            };
            _controller.ControllerContext = CreateAuthenticatedContext(virtualKey);

            // Act
            var result = await _controller.GenerateSpeech(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
            Assert.Equal("Input text is required", problemDetails.Detail);
        }

        [Fact]
        public async Task GenerateSpeech_WhenRouterThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var virtualKey = "test-virtual-key";
            var request = new TextToSpeechRequestDto
            {
                Model = "tts-1",
                Input = "Hello",
                Voice = "alloy"
            };

            _audioRouterMock.Setup(x => x.GetTextToSpeechClientAsync(
                    It.IsAny<TextToSpeechRequest>(),
                    It.Is<string>(k => k == virtualKey),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Router error"));

            _controller.ControllerContext = CreateAuthenticatedContext(virtualKey);

            // Act
            var result = await _controller.GenerateSpeech(request);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("An error occurred while generating speech", problemDetails.Detail);
        }

        #endregion

        #region Helper Methods

        private ControllerContext CreateAuthenticatedContext(string virtualKey)
        {
            var context = CreateControllerContext();
            
            var claims = new[]
            {
                new Claim("VirtualKey", virtualKey)
            };
            
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            
            context.HttpContext.User = principal;
            return context;
        }

        private IFormFile CreateFormFile(string content, string fileName)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            
            var formFile = new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "audio/mpeg"
            };
            
            return formFile;
        }

        #endregion
    }
}