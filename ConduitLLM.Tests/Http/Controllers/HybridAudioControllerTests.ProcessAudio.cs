using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class HybridAudioControllerTests
    {
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
            var errorResponse = Assert.IsType<ErrorResponseDto>(badRequestResult.Value);
            Assert.Equal("No audio file provided", errorResponse.error.ToString());
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
            var errorResponse = Assert.IsType<ErrorResponseDto>(badRequestResult.Value);
            Assert.Equal("No audio file provided", errorResponse.error.ToString());
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
            var errorResponse = Assert.IsType<ErrorResponseDto>(badRequestResult.Value);
            Assert.Equal(errorMessage, errorResponse.error.ToString());
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
            var errorResponse = Assert.IsType<ErrorResponseDto>(statusCodeResult.Value);
            Assert.Equal("An error occurred processing the audio", errorResponse.error.ToString());
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
    }
}