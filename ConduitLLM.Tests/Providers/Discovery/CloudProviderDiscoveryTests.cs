using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ConduitLLM.Providers;
using Xunit;

namespace ConduitLLM.Tests.Providers.Discovery
{
    /// <summary>
    /// Tests for cloud provider model discovery classes (Azure, AWS, GCP).
    /// </summary>
    public class CloudProviderDiscoveryTests
    {
        private static HttpClient CreateMockHttpClient()
        {
            // Simple mock client for providers that don't make API calls
            return new HttpClient(new HttpClientHandler())
            {
                BaseAddress = new Uri("https://api.example.com")
            };
        }

        #region Azure OpenAI Tests

        [Fact]
        public async Task AzureOpenAIModelDiscovery_WithApiKey_ReturnsDeployableModels()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();

            // Act
            var models = await AzureOpenAIModelDiscovery.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.NotEmpty(models);
            Assert.Contains(models, m => m.ModelId == "gpt-4");
            Assert.Contains(models, m => m.ModelId == "gpt-4-turbo");
            Assert.Contains(models, m => m.ModelId == "gpt-35-turbo");
            Assert.Contains(models, m => m.ModelId == "text-embedding-ada-002");
            Assert.Contains(models, m => m.ModelId == "dall-e-3");
            
            // Check capabilities
            var gpt4 = models.First(m => m.ModelId == "gpt-4");
            Assert.True(gpt4.Capabilities.Chat);
            Assert.True(gpt4.Capabilities.FunctionCalling);
            Assert.True(gpt4.Capabilities.ToolUse);
            Assert.True(gpt4.Capabilities.JsonMode);
            
            var dalle = models.First(m => m.ModelId == "dall-e-3");
            Assert.True(dalle.Capabilities.ImageGeneration);
            Assert.Contains("1024x1024", dalle.Capabilities.SupportedImageSizes);
        }

        [Fact]
        public async Task AzureOpenAIModelDiscovery_WithoutApiKey_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();

            // Act
            var models = await AzureOpenAIModelDiscovery.DiscoverAsync(httpClient, null);

            // Assert
            Assert.Empty(models);
        }

        #endregion

        #region AWS Bedrock Tests

        [Fact]
        public async Task BedrockModelDiscovery_WithApiKey_ReturnsFoundationModels()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();

            // Act
            var models = await BedrockModelDiscovery.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.NotEmpty(models);
            
            // Check Anthropic models via Bedrock
            Assert.Contains(models, m => m.ModelId == "anthropic.claude-3-opus-20240229");
            Assert.Contains(models, m => m.ModelId == "anthropic.claude-3-sonnet-20240229");
            
            // Check Amazon Titan models
            Assert.Contains(models, m => m.ModelId == "amazon.titan-text-express-v1");
            Assert.Contains(models, m => m.ModelId == "amazon.titan-embed-text-v1");
            Assert.Contains(models, m => m.ModelId == "amazon.titan-image-generator-v1");
            
            // Check Meta models
            Assert.Contains(models, m => m.ModelId == "meta.llama3-8b-instruct-v1:0");
            Assert.Contains(models, m => m.ModelId == "meta.llama3-70b-instruct-v1:0");
            
            // Check Stability AI
            Assert.Contains(models, m => m.ModelId == "stability.stable-diffusion-xl-v1");
            
            // Verify capabilities
            var claude = models.First(m => m.ModelId.Contains("claude-3-opus"));
            Assert.True(claude.Capabilities.Chat);
            Assert.True(claude.Capabilities.Vision);
            Assert.True(claude.Capabilities.ToolUse);
            Assert.Equal(200000, claude.Capabilities.MaxTokens);
            
            var titanImage = models.First(m => m.ModelId.Contains("titan-image"));
            Assert.True(titanImage.Capabilities.ImageGeneration);
            
            var embedding = models.First(m => m.ModelId.Contains("embed"));
            Assert.True(embedding.Capabilities.Embeddings);
            Assert.False(embedding.Capabilities.Chat);
        }

        [Fact]
        public async Task BedrockModelDiscovery_ChecksMetadata()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();

            // Act
            var models = await BedrockModelDiscovery.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            var model = models.First();
            Assert.Contains("model_arn_format", model.Metadata.Keys);
            Assert.Contains("foundation_provider", model.Metadata.Keys);
            Assert.Contains("description", model.Metadata.Keys);
        }

        #endregion

        #region Vertex AI Tests

        [Fact]
        public async Task VertexAIModelDiscovery_WithApiKey_ReturnsGoogleModels()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();

            // Act
            var models = await VertexAIModelDiscovery.DiscoverAsync(httpClient, "test-api-key");

            // Assert
            Assert.NotEmpty(models);
            
            // Check Gemini models
            Assert.Contains(models, m => m.ModelId == "gemini-1.5-pro-001");
            Assert.Contains(models, m => m.ModelId == "gemini-1.5-flash-001");
            Assert.Contains(models, m => m.ModelId == "gemini-pro-vision");
            
            // Check PaLM models
            Assert.Contains(models, m => m.ModelId == "text-bison");
            Assert.Contains(models, m => m.ModelId == "chat-bison");
            Assert.Contains(models, m => m.ModelId == "code-bison");
            
            // Check Claude via Vertex
            Assert.Contains(models, m => m.ModelId == "claude-3-opus@20240229");
            
            // Check Imagen models
            Assert.Contains(models, m => m.ModelId == "imagegeneration@005");
            Assert.Contains(models, m => m.ModelId == "imagen-3.0-generate-001");
            
            // Verify capabilities
            var gemini15 = models.First(m => m.ModelId.Contains("gemini-1.5-pro"));
            Assert.True(gemini15.Capabilities.Chat);
            Assert.True(gemini15.Capabilities.Vision);
            Assert.True(gemini15.Capabilities.VideoUnderstanding);
            Assert.Equal(2097152, gemini15.Capabilities.MaxTokens); // 2M context
            
            var imagen = models.First(m => m.ModelId.Contains("imagen-3"));
            Assert.True(imagen.Capabilities.ImageGeneration);
            Assert.Contains("2048x2048", imagen.Capabilities.SupportedImageSizes);
        }

        #endregion

        #region Common Cloud Provider Tests

        [Theory]
        [InlineData(typeof(AzureOpenAIModelDiscovery))]
        [InlineData(typeof(BedrockModelDiscovery))]
        [InlineData(typeof(VertexAIModelDiscovery))]
        public async Task CloudProviders_ReturnEmptyWithoutApiKey(Type discoveryType)
        {
            // Arrange
            var httpClient = CreateMockHttpClient();
            var method = discoveryType.GetMethod("DiscoverAsync");

            // Act
            var task = (Task)method.Invoke(null, new object[] { httpClient, null, default(CancellationToken) });
            await task;
            var models = ((dynamic)task).Result;

            // Assert
            Assert.Empty(models);
        }

        [Fact]
        public async Task CloudProviders_MetadataContainsProviderSpecificInfo()
        {
            // Arrange
            var httpClient = CreateMockHttpClient();

            // Azure
            var azureModels = await AzureOpenAIModelDiscovery.DiscoverAsync(httpClient, "key");
            Assert.All(azureModels, m => Assert.Contains("deployment_note", m.Metadata.Keys));

            // Bedrock
            var bedrockModels = await BedrockModelDiscovery.DiscoverAsync(httpClient, "key");
            Assert.All(bedrockModels, m => Assert.Contains("model_arn_format", m.Metadata.Keys));

            // Vertex AI
            var vertexModels = await VertexAIModelDiscovery.DiscoverAsync(httpClient, "key");
            Assert.All(vertexModels, m => Assert.Contains("endpoint_format", m.Metadata.Keys));
        }

        #endregion
    }
}