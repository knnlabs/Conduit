using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Validation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Validation
{
    public class MinimalParameterValidatorTests
    {
        private readonly MinimalParameterValidator _validator;
        private readonly Mock<ILogger<MinimalParameterValidator>> _mockLogger;

        public MinimalParameterValidatorTests()
        {
            _mockLogger = new Mock<ILogger<MinimalParameterValidator>>();
            _validator = new MinimalParameterValidator(_mockLogger.Object);
        }

        #region Text Parameter Validation Tests

        [Fact]
        public void ValidateTextParameters_WithNullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _validator.ValidateTextParameters(null!));
        }

        [Fact]
        public void ValidateTextParameters_WithMissingModel_ThrowsArgumentException()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = null,
                Messages = new List<Message> { new Message { Role = "user", Content = "test" } }
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _validator.ValidateTextParameters(request));
            Assert.Contains("Model is required", ex.Message);
        }

        [Fact]
        public void ValidateTextParameters_WithEmptyMessages_ThrowsArgumentException()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message>()
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _validator.ValidateTextParameters(request));
            Assert.Contains("At least one message is required", ex.Message);
        }

        [Fact]
        public void ValidateTextParameters_WithNegativeTemperature_CorrectsTpZero()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message> { new Message { Role = "user", Content = "test" } },
                Temperature = -0.5
            };

            // Act
            _validator.ValidateTextParameters(request);

            // Assert
            Assert.Equal(0, request.Temperature);
        }

        [Fact]
        public void ValidateTextParameters_WithNegativeMaxTokens_RemovesParameter()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message> { new Message { Role = "user", Content = "test" } },
                MaxTokens = -100
            };

            // Act
            _validator.ValidateTextParameters(request);

            // Assert
            Assert.Null(request.MaxTokens);
        }

        [Fact]
        public void ValidateTextParameters_WithNLessThanOne_CorrectsTpOne()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message> { new Message { Role = "user", Content = "test" } },
                N = 0
            };

            // Act
            _validator.ValidateTextParameters(request);

            // Assert
            Assert.Equal(1, request.N);
        }

        [Fact]
        public void ValidateTextParameters_WithValidParameters_DoesNotModify()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message> { new Message { Role = "user", Content = "test" } },
                Temperature = 0.7,
                MaxTokens = 1000,
                N = 2,
                TopP = 0.9
            };

            // Act
            _validator.ValidateTextParameters(request);

            // Assert
            Assert.Equal(0.7, request.Temperature);
            Assert.Equal(1000, request.MaxTokens);
            Assert.Equal(2, request.N);
            Assert.Equal(0.9, request.TopP);
        }

        [Fact]
        public void ValidateTextParameters_WithNullExtensionData_RemovesNullValues()
        {
            // Arrange
            var extensionData = new Dictionary<string, JsonElement>
            {
                ["valid_param"] = JsonDocument.Parse("\"value\"").RootElement,
                ["null_param"] = JsonDocument.Parse("null").RootElement
            };

            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message> { new Message { Role = "user", Content = "test" } },
                ExtensionData = extensionData
            };

            // Act
            _validator.ValidateTextParameters(request);

            // Assert
            Assert.Single(request.ExtensionData);
            Assert.True(request.ExtensionData.ContainsKey("valid_param"));
            Assert.False(request.ExtensionData.ContainsKey("null_param"));
        }

        #endregion

        #region Image Parameter Validation Tests

        [Fact]
        public void ValidateImageParameters_WithNullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _validator.ValidateImageParameters(null!));
        }

        [Fact]
        public void ValidateImageParameters_WithMissingModel_ThrowsArgumentException()
        {
            // Arrange
            var request = new ImageGenerationRequest
            {
                Model = null,
                Prompt = "test prompt"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _validator.ValidateImageParameters(request));
            Assert.Contains("Model is required", ex.Message);
        }

        [Fact]
        public void ValidateImageParameters_WithMissingPrompt_ThrowsArgumentException()
        {
            // Arrange
            var request = new ImageGenerationRequest
            {
                Model = "test-model",
                Prompt = ""
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _validator.ValidateImageParameters(request));
            Assert.Contains("Prompt is required", ex.Message);
        }

        [Fact]
        public void ValidateImageParameters_WithNLessThanOne_CorrectsTpOne()
        {
            // Arrange
            var request = new ImageGenerationRequest
            {
                Model = "test-model",
                Prompt = "test prompt",
                N = 0
            };

            // Act
            _validator.ValidateImageParameters(request);

            // Assert
            Assert.Equal(1, request.N);
        }

        [Fact]
        public void ValidateImageParameters_WithValidParameters_DoesNotModify()
        {
            // Arrange
            var request = new ImageGenerationRequest
            {
                Model = "test-model",
                Prompt = "test prompt",
                N = 3,
                Size = "1024x1024",
                ResponseFormat = "url"
            };

            // Act
            _validator.ValidateImageParameters(request);

            // Assert
            Assert.Equal(3, request.N);
            Assert.Equal("1024x1024", request.Size);
            Assert.Equal("url", request.ResponseFormat);
        }

        #endregion

        #region Video Parameter Validation Tests

        [Fact]
        public void ValidateVideoParameters_WithNullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _validator.ValidateVideoParameters(null!));
        }

        [Fact]
        public void ValidateVideoParameters_WithMissingModel_ThrowsArgumentException()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = null,
                Prompt = "test prompt"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _validator.ValidateVideoParameters(request));
            Assert.Contains("Model is required", ex.Message);
        }

        [Fact]
        public void ValidateVideoParameters_WithMissingPrompt_ThrowsArgumentException()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = ""
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _validator.ValidateVideoParameters(request));
            Assert.Contains("Prompt is required", ex.Message);
        }

        [Fact]
        public void ValidateVideoParameters_WithNegativeDuration_RemovesParameter()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "test prompt",
                Duration = -5
            };

            // Act
            _validator.ValidateVideoParameters(request);

            // Assert
            Assert.Null(request.Duration);
        }

        [Fact]
        public void ValidateVideoParameters_WithNegativeFps_RemovesParameter()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "test prompt",
                Fps = -30
            };

            // Act
            _validator.ValidateVideoParameters(request);

            // Assert
            Assert.Null(request.Fps);
        }

        [Fact]
        public void ValidateVideoParameters_WithValidParameters_DoesNotModify()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "test prompt",
                N = 2,
                Duration = 10,
                Fps = 24,
                Size = "1920x1080"
            };

            // Act
            _validator.ValidateVideoParameters(request);

            // Assert
            Assert.Equal(2, request.N);
            Assert.Equal(10, request.Duration);
            Assert.Equal(24, request.Fps);
            Assert.Equal("1920x1080", request.Size);
        }

        #endregion

        #region Extension Data Validation Tests

        [Fact]
        public void ValidateTextParameters_WithNegativeTokensInExtensionData_RemovesParameter()
        {
            // Arrange
            var extensionData = new Dictionary<string, JsonElement>
            {
                ["custom_tokens"] = JsonDocument.Parse("-100").RootElement,
                ["valid_param"] = JsonDocument.Parse("50").RootElement
            };

            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message> { new Message { Role = "user", Content = "test" } },
                ExtensionData = extensionData
            };

            // Act
            _validator.ValidateTextParameters(request);

            // Assert
            Assert.Single(request.ExtensionData);
            Assert.True(request.ExtensionData.ContainsKey("valid_param"));
            Assert.False(request.ExtensionData.ContainsKey("custom_tokens"));
        }

        [Fact]
        public void ValidateImageParameters_WithNegativeWidthInExtensionData_RemovesParameter()
        {
            // Arrange
            var extensionData = new Dictionary<string, JsonElement>
            {
                ["width"] = JsonDocument.Parse("-512").RootElement,
                ["height"] = JsonDocument.Parse("512").RootElement
            };

            var request = new ImageGenerationRequest
            {
                Model = "test-model",
                Prompt = "test prompt",
                ExtensionData = extensionData
            };

            // Act
            _validator.ValidateImageParameters(request);

            // Assert
            Assert.Single(request.ExtensionData);
            Assert.True(request.ExtensionData.ContainsKey("height"));
            Assert.False(request.ExtensionData.ContainsKey("width"));
        }

        [Fact]
        public void ValidateVideoParameters_WithNegativeSeedInExtensionData_RemovesParameter()
        {
            // Arrange
            var extensionData = new Dictionary<string, JsonElement>
            {
                ["seed"] = JsonDocument.Parse("-42").RootElement,
                ["guidance_scale"] = JsonDocument.Parse("7.5").RootElement
            };

            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "test prompt",
                ExtensionData = extensionData
            };

            // Act
            _validator.ValidateVideoParameters(request);

            // Assert
            Assert.Single(request.ExtensionData);
            Assert.True(request.ExtensionData.ContainsKey("guidance_scale"));
            Assert.False(request.ExtensionData.ContainsKey("seed"));
        }

        #endregion
    }
}