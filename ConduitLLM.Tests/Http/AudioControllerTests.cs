using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Http.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Http
{
    public class AudioControllerTests
    {
        private readonly Mock<IAudioRouter> _mockAudioRouter;
        private readonly Mock<Configuration.Services.IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ILogger<AudioController>> _mockLogger;
        private readonly AudioController _controller;

        public AudioControllerTests()
        {
            _mockAudioRouter = new Mock<IAudioRouter>();
            _mockVirtualKeyService = new Mock<Configuration.Services.IVirtualKeyService>();
            _mockLogger = new Mock<ILogger<AudioController>>();
            _controller = new AudioController(
                _mockAudioRouter.Object,
                _mockVirtualKeyService.Object,
                _mockLogger.Object);

            // Setup default HTTP context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Fact]
        public async Task TranscribeAudio_Should_Return_Unauthorized_Without_VirtualKey()
        {
            // Arrange
            var file = CreateMockFile("test.wav", new byte[] { 1, 2, 3 });

            // Act
            var result = await _controller.TranscribeAudio(file);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task TranscribeAudio_Should_Return_BadRequest_For_Empty_File()
        {
            // Arrange
            _controller.HttpContext.Items["VirtualKey"] = "test-key";
            var file = CreateMockFile("test.wav", Array.Empty<byte>());

            // Act
            var result = await _controller.TranscribeAudio(file);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task TranscribeAudio_Should_Return_BadRequest_For_Large_File()
        {
            // Arrange
            _controller.HttpContext.Items["VirtualKey"] = "test-key";
            var file = CreateMockFile("test.wav", new byte[26 * 1024 * 1024]); // 26MB

            // Act
            var result = await _controller.TranscribeAudio(file);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task TranscribeAudio_Should_Return_Success_With_Valid_Request()
        {
            // Arrange
            _controller.HttpContext.Items["VirtualKey"] = "test-key";
            var file = CreateMockFile("test.wav", new byte[] { 1, 2, 3, 4, 5 });

            var mockClient = new Mock<IAudioTranscriptionClient>();
            mockClient.Setup(c => c.TranscribeAudioAsync(
                It.IsAny<AudioTranscriptionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AudioTranscriptionResponse
                {
                    Text = "Hello, world!",
                    Language = "en"
                });

            _mockAudioRouter.Setup(r => r.GetTranscriptionClientAsync(
                It.IsAny<AudioTranscriptionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);

            // Setup virtual key service
            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyByKeyValueAsync("test-key"))
                .ReturnsAsync(new VirtualKey { Id = 1, KeyHash = "test-key" });

            // Act
            var result = await _controller.TranscribeAudio(file);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<AudioTranscriptionResponse>(okResult.Value);
            Assert.Equal("Hello, world!", response.Text);
            Assert.Equal("en", response.Language);

            // Verify spend was updated
            _mockVirtualKeyService.Verify(s => s.UpdateSpendAsync(
                1, // Virtual key ID
                It.IsAny<decimal>()), Times.Once);
        }

        [Fact]
        public async Task TranscribeAudio_Should_Return_PlainText_For_VTT_Format()
        {
            // Arrange
            _controller.HttpContext.Items["VirtualKey"] = "test-key";
            var file = CreateMockFile("test.wav", new byte[] { 1, 2, 3 });

            var mockClient = new Mock<IAudioTranscriptionClient>();
            mockClient.Setup(c => c.TranscribeAudioAsync(
                It.IsAny<AudioTranscriptionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AudioTranscriptionResponse
                {
                    Text = "WEBVTT\n\n00:00:00.000 --> 00:00:05.000\nHello, world!"
                });

            _mockAudioRouter.Setup(r => r.GetTranscriptionClientAsync(
                It.IsAny<AudioTranscriptionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);

            // Act
            var result = await _controller.TranscribeAudio(file, response_format: "vtt");

            // Assert
            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal("text/plain", contentResult.ContentType);
            Assert.Contains("WEBVTT", contentResult.Content);
        }

        [Fact]
        public async Task GenerateSpeech_Should_Return_Unauthorized_Without_VirtualKey()
        {
            // Arrange
            var request = new AudioController.TextToSpeechRequestDto
            {
                Input = "Hello, world!",
                Model = "tts-1",
                Voice = "alloy"
            };

            // Act
            var result = await _controller.GenerateSpeech(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GenerateSpeech_Should_Return_BadRequest_For_Empty_Input()
        {
            // Arrange
            _controller.HttpContext.Items["VirtualKey"] = "test-key";
            var request = new AudioController.TextToSpeechRequestDto
            {
                Input = "",
                Model = "tts-1",
                Voice = "alloy"
            };

            // Act
            var result = await _controller.GenerateSpeech(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GenerateSpeech_Should_Return_BadRequest_For_Long_Input()
        {
            // Arrange
            _controller.HttpContext.Items["VirtualKey"] = "test-key";
            var request = new AudioController.TextToSpeechRequestDto
            {
                Input = new string('a', 4097), // 4097 chars
                Model = "tts-1",
                Voice = "alloy"
            };

            // Act
            var result = await _controller.GenerateSpeech(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GenerateSpeech_Should_Return_Audio_File()
        {
            // Arrange
            _controller.HttpContext.Items["VirtualKey"] = "test-key";
            var request = new AudioController.TextToSpeechRequestDto
            {
                Input = "Hello, world!",
                Model = "tts-1",
                Voice = "alloy"
            };

            var mockClient = new Mock<ITextToSpeechClient>();
            mockClient.Setup(c => c.CreateSpeechAsync(
                It.IsAny<TextToSpeechRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TextToSpeechResponse
                {
                    AudioData = new byte[] { 1, 2, 3, 4, 5 },
                    Format = "mp3"
                });

            _mockAudioRouter.Setup(r => r.GetTextToSpeechClientAsync(
                It.IsAny<TextToSpeechRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);

            // Setup virtual key service
            _mockVirtualKeyService.Setup(s => s.GetVirtualKeyByKeyValueAsync("test-key"))
                .ReturnsAsync(new VirtualKey { Id = 1, KeyHash = "test-key" });

            // Act
            var result = await _controller.GenerateSpeech(request);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("audio/mpeg", fileResult.ContentType);
            Assert.Equal(5, fileResult.FileContents.Length);

            // Verify spend was updated
            _mockVirtualKeyService.Verify(s => s.UpdateSpendAsync(
                1, // Virtual key ID
                It.IsAny<decimal>()), Times.Once);
        }

        [Fact]
        public async Task GenerateSpeech_Should_Support_Different_Audio_Formats()
        {
            // Arrange
            _controller.HttpContext.Items["VirtualKey"] = "test-key";
            var request = new AudioController.TextToSpeechRequestDto
            {
                Input = "Hello, world!",
                Model = "tts-1",
                Voice = "alloy",
                ResponseFormat = "wav"
            };

            var mockClient = new Mock<ITextToSpeechClient>();
            mockClient.Setup(c => c.CreateSpeechAsync(
                It.Is<TextToSpeechRequest>(r => r.ResponseFormat == AudioFormat.Wav),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TextToSpeechResponse
                {
                    AudioData = new byte[] { 1, 2, 3 },
                    Format = "wav"
                });

            _mockAudioRouter.Setup(r => r.GetTextToSpeechClientAsync(
                It.IsAny<TextToSpeechRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);

            // Act
            var result = await _controller.GenerateSpeech(request);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("audio/wav", fileResult.ContentType);
        }

        [Fact]
        public async Task TranslateAudio_Should_Force_English_Output()
        {
            // Arrange
            _controller.HttpContext.Items["VirtualKey"] = "test-key";
            var file = CreateMockFile("test.wav", new byte[] { 1, 2, 3 });

            var mockClient = new Mock<IAudioTranscriptionClient>();
            mockClient.Setup(c => c.TranscribeAudioAsync(
                It.Is<AudioTranscriptionRequest>(r => r.Language == "en"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AudioTranscriptionResponse
                {
                    Text = "Hello, world!",
                    Language = "en"
                });

            _mockAudioRouter.Setup(r => r.GetTranscriptionClientAsync(
                It.IsAny<AudioTranscriptionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);

            // Act
            var result = await _controller.TranslateAudio(file);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<AudioTranscriptionResponse>(okResult.Value);
            Assert.Equal("en", response.Language);
        }

        private IFormFile CreateMockFile(string fileName, byte[] content)
        {
            var stream = new MemoryStream(content);
            var file = new FormFile(stream, 0, content.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "audio/wav"
            };
            return file;
        }
    }
}
