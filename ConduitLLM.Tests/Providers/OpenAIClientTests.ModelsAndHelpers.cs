using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.OpenAI;
using Moq;
using Moq.Protected;
using Xunit;

namespace ConduitLLM.Tests.Providers
{
    public partial class OpenAIClientTests
    {
        #region Model Listing Tests

        [Fact]
        public async Task GetModelsAsync_ForOpenAI_ReturnsStandardModels()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var response = new ListModelsResponse
            {
                Data = new List<OpenAIModelData>
                {
                    new() { Id = "gpt-4", Object = "model", OwnedBy = "openai" },
                    new() { Id = "gpt-3.5-turbo", Object = "model", OwnedBy = "openai" }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var models = await client.GetModelsAsync();

            // Assert
            Assert.NotNull(models);
            Assert.Equal(2, models.Count);
            Assert.Contains(models, m => m.Id == "gpt-4");
            Assert.Contains(models, m => m.Id == "gpt-3.5-turbo");
        }

        [Fact]
        public async Task GetModelsAsync_ForAzure_ReturnsDeployments()
        {
            // Arrange
            var client = CreateAzureOpenAIClient();
            var response = new ConduitLLM.Providers.OpenAI.AzureOpenAIModels.ListDeploymentsResponse
            {
                Data = new List<ConduitLLM.Providers.OpenAI.AzureOpenAIModels.DeploymentInfo>
                {
                    new() { DeploymentId = "my-gpt4", Model = "gpt-4", Status = "succeeded" },
                    new() { DeploymentId = "my-gpt35", Model = "gpt-3.5-turbo", Status = "succeeded" }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var models = await client.GetModelsAsync();

            // Assert
            Assert.NotNull(models);
            Assert.Equal(2, models.Count);
            Assert.Contains(models, m => m.Id == "my-gpt4");
            Assert.Contains(models, m => m.Id == "my-gpt35");
        }

        [Fact]
        public async Task GetModelsAsync_WithUnauthorizedResponse_ThrowsException()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var errorResponse = new
            {
                error = new
                {
                    message = "Invalid API key provided",
                    type = "invalid_request_error",
                    code = "invalid_api_key"
                }
            };

            SetupHttpResponse(HttpStatusCode.Unauthorized, errorResponse);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.GetModelsAsync());

            Assert.Contains("Invalid API key", exception.Message);
        }

        [Fact]
        public async Task GetModelsAsync_WithForbiddenResponse_ThrowsException()
        {
            // Arrange
            var client = CreateOpenAIClient();
            var errorResponse = new
            {
                error = new
                {
                    message = "Access denied. Your API key does not have permission to access this resource.",
                    type = "permission_error",
                    code = "insufficient_quota"
                }
            };

            SetupHttpResponse(HttpStatusCode.Forbidden, errorResponse);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.GetModelsAsync());

            Assert.Contains("Access denied", exception.Message);
        }

        [Fact]
        public async Task GetModelsAsync_WithInvalidApiKey_DoesNotReturnFallbackModels()
        {
            // Arrange
            var client = CreateOpenAIClient(); 
            var errorResponse = new
            {
                error = new
                {
                    message = "Invalid API key provided: badkey",
                    type = "invalid_request_error", 
                    code = "invalid_api_key"
                }
            };

            SetupHttpResponse(HttpStatusCode.Unauthorized, errorResponse);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.GetModelsAsync());

            // Verify that no fallback models are returned - should throw exception instead
            Assert.NotNull(exception);
            Assert.Contains("Invalid API key", exception.Message);
        }

        #endregion

        #region Helper Methods

        private OpenAIClient CreateOpenAIClient(string modelId = "gpt-4")
        {
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };
            
            var logger = CreateLogger<OpenAIClient>();

            return new OpenAIClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);
        }

        private OpenAIClient CreateAzureOpenAIClient(string deploymentId = "my-deployment")
        {
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key",
                BaseUrl = "https://myinstance.openai.azure.com"
            };
            
            var logger = CreateLogger<OpenAIClient>();

            return new OpenAIClient(
                provider,
                keyCredential,
                deploymentId,
                logger.Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object,
                providerName: "azure");
        }

        private void SetupHttpResponse<T>(HttpStatusCode statusCode, T content, string contentType = "application/json")
        {
            HttpContent httpContent;
            if (content is byte[] bytes)
            {
                httpContent = new ByteArrayContent(bytes);
                httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            }
            else if (content is string str)
            {
                httpContent = new StringContent(str, Encoding.UTF8, contentType);
            }
            else
            {
                var json = JsonSerializer.Serialize(content);
                httpContent = new StringContent(json, Encoding.UTF8, contentType);
            }

            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = httpContent
                });
        }

        #endregion
    }
}