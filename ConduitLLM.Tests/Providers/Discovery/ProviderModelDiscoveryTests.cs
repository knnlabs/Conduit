using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;
using Moq;
using Moq.Protected;
using Xunit;

namespace ConduitLLM.Tests.Providers.Discovery
{
    /// <summary>
    /// Tests for provider model discovery classes.
    /// </summary>
    public class ProviderModelDiscoveryTests
    {
        private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, object? responseContent = null)
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            
            var response = new HttpResponseMessage(statusCode);
            if (responseContent != null)
            {
                response.Content = new StringContent(
                    JsonSerializer.Serialize(responseContent), 
                    Encoding.UTF8, 
                    "application/json");
            }

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            return new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("https://api.example.com")
            };
        }

        #region OpenAI Tests

        [Fact]
        public async Task OpenAIModelDiscovery_WithValidApiKey_ReturnsModels()
        {
            // Arrange
            var responseContent = new
            {
                data = new[]
                {
                    new { id = "gpt-4-turbo-preview", created = 1234567890, owned_by = "openai" },
                    new { id = "gpt-3.5-turbo", created = 1234567890, owned_by = "openai" },
                    new { id = "text-embedding-ada-002", created = 1234567890, owned_by = "openai" }
                }
            };
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);

            // Act
            var models = await OpenAIModelDiscovery.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.NotEmpty(models);
            Assert.Contains(models, m => m.ModelId == "gpt-4-turbo-preview");
            Assert.Contains(models, m => m.ModelId == "gpt-3.5-turbo");
            Assert.Contains(models, m => m.ModelId == "text-embedding-ada-002");
            
            // Check capabilities
            var gpt4 = models.First(m => m.ModelId == "gpt-4-turbo-preview");
            Assert.True(gpt4.Capabilities.Chat);
            Assert.True(gpt4.Capabilities.ChatStream);
            Assert.True(gpt4.Capabilities.FunctionCalling);
            
            var embedding = models.First(m => m.ModelId == "text-embedding-ada-002");
            Assert.True(embedding.Capabilities.Embeddings);
            Assert.False(embedding.Capabilities.Chat);
        }

        [Fact]
        public async Task OpenAIModelDiscovery_WithoutApiKey_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK);

            // Act
            var models = await OpenAIModelDiscovery.DiscoverAsync(httpClient, null);

            // Assert
            Assert.Empty(models);
        }

        [Fact]
        public async Task OpenAIModelDiscovery_WithApiError_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized);

            // Act
            var models = await OpenAIModelDiscovery.DiscoverAsync(httpClient, "invalid-key");

            // Assert
            Assert.Empty(models);
        }

        #endregion

        #region Anthropic Tests

        [Fact]
        public async Task AnthropicModels_WithValidApiKey_ReturnsKnownModels()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK);

            // Act
            var models = await AnthropicModels.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.NotEmpty(models);
            Assert.Contains(models, m => m.ModelId == "claude-3-5-haiku-20241022");
            Assert.Contains(models, m => m.ModelId == "claude-3-5-sonnet-20241022");
            Assert.Contains(models, m => m.ModelId == "claude-3-opus-20240229");
            
            // Check capabilities
            var claude35 = models.First(m => m.ModelId.Contains("claude-3"));
            Assert.True(claude35.Capabilities.Chat);
            Assert.True(claude35.Capabilities.Vision);
            Assert.True(claude35.Capabilities.ToolUse);
            Assert.False(claude35.Capabilities.JsonMode);
        }

        [Fact]
        public async Task AnthropicModels_WithoutApiKey_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK);

            // Act
            var models = await AnthropicModels.DiscoverAsync(httpClient, null);

            // Assert
            Assert.Empty(models);
        }

        #endregion

        #region Google/Gemini Tests

        [Fact]
        public async Task GoogleModels_WithValidApiKey_ReturnsModels()
        {
            // Arrange
            var responseContent = new
            {
                models = new[]
                {
                    new 
                    { 
                        name = "models/gemini-1.5-pro", 
                        displayName = "Gemini 1.5 Pro",
                        supportedGenerationMethods = new[] { "generateContent", "streamGenerateContent" },
                        inputTokenLimit = 2097152,
                        outputTokenLimit = 8192
                    }
                }
            };
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);

            // Act
            var models = await GoogleModels.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.NotEmpty(models);
            var gemini = models.First(m => m.ModelId == "gemini-1.5-pro");
            Assert.Equal("Gemini 1.5 Pro", gemini.DisplayName);
            Assert.True(gemini.Capabilities.Chat);
            Assert.True(gemini.Capabilities.Vision);
            Assert.True(gemini.Capabilities.VideoUnderstanding);
            Assert.Equal(2097152, gemini.Capabilities.MaxTokens);
        }

        [Fact]
        public async Task GoogleModels_WithApiError_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.Forbidden);

            // Act
            var models = await GoogleModels.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.Empty(models);
        }

        #endregion

        #region Groq Tests

        [Fact]
        public async Task GroqModels_WithValidApiKey_ReturnsModels()
        {
            // Arrange
            var responseContent = new
            {
                data = new[]
                {
                    new { id = "llama3-70b-8192", created = 1234567890, owned_by = "groq" },
                    new { id = "mixtral-8x7b-32768", created = 1234567890, owned_by = "groq" }
                }
            };
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);

            // Act
            var models = await GroqModels.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.NotEmpty(models);
            var llama = models.First(m => m.ModelId == "llama3-70b-8192");
            Assert.True(llama.Capabilities.Chat);
            Assert.Equal(8192, llama.Capabilities.MaxTokens);
            
            var mixtral = models.First(m => m.ModelId == "mixtral-8x7b-32768");
            Assert.Equal(32768, mixtral.Capabilities.MaxTokens);
        }

        #endregion

        #region MiniMax Tests

        [Fact]
        public async Task MiniMaxModels_WithValidApiKey_ReturnsModels()
        {
            // Arrange
            var responseContent = new
            {
                data = new[]
                {
                    new { id = "abab6.5-chat", name = "ABAB 6.5 Chat", created = 1234567890 },
                    new { id = "image-01", name = "Image-01", created = 1234567890 }
                }
            };
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);

            // Act
            var models = await MiniMaxModels.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.NotEmpty(models);
            
            var chat = models.First(m => m.ModelId == "abab6.5-chat");
            Assert.True(chat.Capabilities.Chat);
            Assert.True(chat.Capabilities.Vision);
            Assert.Equal(245760, chat.Capabilities.MaxTokens);
            
            var image = models.First(m => m.ModelId == "image-01");
            Assert.True(image.Capabilities.ImageGeneration);
            Assert.Contains("16:9", image.Capabilities.SupportedImageSizes);
        }

        [Fact]
        public async Task MiniMaxModels_WithApiError_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized);

            // Act
            var models = await MiniMaxModels.DiscoverAsync(httpClient, "invalid-key");

            // Assert
            Assert.Empty(models);
        }

        #endregion

        #region Mistral Tests

        [Fact]
        public async Task MistralModels_WithValidApiKey_ReturnsModels()
        {
            // Arrange
            var responseContent = new
            {
                data = new[]
                {
                    new { id = "mistral-large-latest", created = 1234567890, owned_by = "mistralai" },
                    new { id = "mistral-embed", created = 1234567890, owned_by = "mistralai" }
                }
            };
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);

            // Act
            var models = await MistralModels.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.NotEmpty(models);
            
            var large = models.First(m => m.ModelId == "mistral-large-latest");
            Assert.True(large.Capabilities.Chat);
            Assert.True(large.Capabilities.FunctionCalling);
            Assert.True(large.Capabilities.JsonMode);
            
            var embed = models.First(m => m.ModelId == "mistral-embed");
            Assert.True(embed.Capabilities.Embeddings);
            Assert.False(embed.Capabilities.Chat);
        }

        [Fact]
        public async Task MistralModels_WithApiError_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized);

            // Act
            var models = await MistralModels.DiscoverAsync(httpClient, "invalid-key");

            // Assert
            Assert.Empty(models);
        }

        #endregion

        #region Replicate Tests

        [Fact]
        public async Task ReplicateModels_WithValidApiKey_ReturnsPopularModels()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK);

            // Act
            var models = await ReplicateModels.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.NotEmpty(models);
            Assert.Contains(models, m => m.ModelId.Contains("flux"));
            Assert.Contains(models, m => m.ModelId.Contains("stable-diffusion"));
            Assert.Contains(models, m => m.ModelId.Contains("llama"));
            
            // Check different model types
            var flux = models.First(m => m.ModelId.Contains("flux"));
            Assert.True(flux.Capabilities.ImageGeneration);
            
            var llama = models.First(m => m.ModelId.Contains("llama") && m.ModelId.Contains("chat"));
            Assert.True(llama.Capabilities.Chat);
        }

        #endregion

        #region Ollama Tests

        [Fact]
        public async Task OllamaModelDiscovery_WithLocalService_ReturnsModels()
        {
            // Arrange
            var responseContent = new
            {
                models = new[]
                {
                    new { name = "llama3.2:latest", modifiedAt = "2024-01-01", size = 5000000000L },
                    new { name = "mistral:7b", modifiedAt = "2024-01-01", size = 4000000000L }
                }
            };
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);

            // Act
            var models = await OllamaModelDiscovery.DiscoverAsync(httpClient, "not-used");

            // Assert
            Assert.NotEmpty(models);
            Assert.Contains(models, m => m.ModelId == "llama3.2:latest");
            Assert.Contains(models, m => m.ModelId == "mistral:7b");
        }

        [Fact]
        public async Task OllamaModelDiscovery_WithoutLocalService_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable);

            // Act
            var models = await OllamaModelDiscovery.DiscoverAsync(httpClient, "not-used");

            // Assert
            Assert.Empty(models);
        }

        #endregion

        #region Cerebras Tests

        [Fact]
        public async Task CerebrasModels_WithApiError_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized);

            // Act
            var models = await CerebrasModels.DiscoverAsync(httpClient, "invalid-key");

            // Assert
            Assert.Empty(models);
        }

        #endregion

        #region Cohere Tests

        [Fact]
        public async Task CohereModels_WithApiError_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized);

            // Act
            var models = await CohereModels.DiscoverAsync(httpClient, "invalid-key");

            // Assert
            Assert.Empty(models);
        }

        #endregion

        #region Fireworks Tests

        [Fact]
        public async Task FireworksModels_WithApiError_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized);

            // Act
            var models = await FireworksModels.DiscoverAsync(httpClient, "invalid-key");

            // Assert
            Assert.Empty(models);
        }

        #endregion

        #region Helper Methods

        [Theory]
        [InlineData("gpt-4", true, false)]
        [InlineData("text-embedding-ada-002", false, true)]
        [InlineData("dall-e-3", false, false)]
        public async Task ModelCapabilities_AreCorrectlyInferred(string modelId, bool expectChat, bool expectEmbeddings)
        {
            // This tests that capability inference is working correctly across providers
            var responseContent = new
            {
                data = new[]
                {
                    new { id = modelId, created = 1234567890, owned_by = "openai" }
                }
            };
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);
            
            var models = await OpenAIModelDiscovery.DiscoverAsync(httpClient, "test-key");
            var model = models.FirstOrDefault(m => m.ModelId == modelId);
            
            Assert.NotNull(model);
            Assert.Equal(expectChat, model.Capabilities.Chat);
            Assert.Equal(expectEmbeddings, model.Capabilities.Embeddings);
        }

        #endregion
    }
}