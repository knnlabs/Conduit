using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;
using ConduitLLM.Tests.TestUtilities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Entities;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;

using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Integration tests for the Embedding API functionality.
    /// These tests demonstrate end-to-end usage of the /v1/embeddings endpoint.
    /// </summary>
    public class EmbeddingApiIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>, IDisposable
    {
        private readonly TestWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly ITestOutputHelper _output;

        public EmbeddingApiIntegrationTests(TestWebApplicationFactory<Program> factory, ITestOutputHelper output)
        {
            _factory = factory;
            _output = output;
            
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task EmbeddingsEndpoint_Should_Return_BadRequest_For_Null_Request()
        {
            // Arrange
            var requestContent = new StringContent("null", Encoding.UTF8, "application/json");

            // Add authentication header
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await _client.PostAsync("/v1/embeddings", requestContent);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response: {responseContent}");
            
            Assert.Contains("Invalid request body", responseContent);
        }

        [Fact]
        public async Task EmbeddingsEndpoint_Should_Return_BadRequest_For_Empty_Request()
        {
            // Arrange
            var requestContent = new StringContent("{}", Encoding.UTF8, "application/json");

            // Add authentication header
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await _client.PostAsync("/v1/embeddings", requestContent);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task EmbeddingsEndpoint_Should_Accept_Valid_Request_Structure()
        {
            // Arrange
            var embeddingRequest = new EmbeddingRequest
            {
                Input = "Hello, world!",
                Model = "text-embedding-3-small",
                EncodingFormat = "float"
            };

            var json = JsonSerializer.Serialize(embeddingRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            
            var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

            // Add authentication header
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await _client.PostAsync("/v1/embeddings", requestContent);

            // Assert
            // We expect this to fail with a server error or model unavailable error
            // since we don't have actual provider credentials configured in tests
            // But it should NOT return a 501 Not Implemented anymore
            Assert.NotEqual(HttpStatusCode.NotImplemented, response.StatusCode);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseContent}");
        }

        [Fact]
        public async Task EmbeddingsEndpoint_Should_Handle_Array_Input()
        {
            // Arrange
            var embeddingRequest = new EmbeddingRequest
            {
                Input = new[] { "Hello, world!", "This is a test." },
                Model = "text-embedding-3-small",
                EncodingFormat = "float"
            };

            var json = JsonSerializer.Serialize(embeddingRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            
            var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

            // Add authentication header
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await _client.PostAsync("/v1/embeddings", requestContent);

            // Assert
            // Should not return 501 Not Implemented
            Assert.NotEqual(HttpStatusCode.NotImplemented, response.StatusCode);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseContent}");
        }

        [Fact]
        public async Task EmbeddingsEndpoint_Should_Handle_Different_Models()
        {
            // Arrange - Test with different embedding models
            var models = new[]
            {
                "text-embedding-3-small",
                "text-embedding-3-large", 
                "text-embedding-ada-002"
            };

            foreach (var model in models)
            {
                var embeddingRequest = new EmbeddingRequest
                {
                    Input = "Test input for embedding",
                    Model = model,
                    EncodingFormat = "float"
                };

                var json = JsonSerializer.Serialize(embeddingRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
                
                var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

                // Add authentication header
                _client.DefaultRequestHeaders.Clear();
                _client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

                // Act
                var response = await _client.PostAsync("/v1/embeddings", requestContent);

                // Assert
                Assert.NotEqual(HttpStatusCode.NotImplemented, response.StatusCode);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"Model: {model}, Status: {response.StatusCode}");
                _output.WriteLine($"Response: {responseContent}");
            }
        }

        [Fact]
        public async Task EmbeddingsEndpoint_Should_Handle_Optional_Parameters()
        {
            // Arrange
            var embeddingRequest = new EmbeddingRequest
            {
                Input = "Test input with optional parameters",
                Model = "text-embedding-3-small",
                EncodingFormat = "float",
                Dimensions = 1536,
                User = "test-user"
            };

            var json = JsonSerializer.Serialize(embeddingRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

            // Add authentication header
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await _client.PostAsync("/v1/embeddings", requestContent);

            // Assert
            Assert.NotEqual(HttpStatusCode.NotImplemented, response.StatusCode);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseContent}");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task EmbeddingsEndpoint_Should_Handle_Invalid_Input(string invalidInput)
        {
            // Arrange
            var embeddingRequest = new
            {
                input = invalidInput,
                model = "text-embedding-3-small",
                encoding_format = "float"
            };

            var json = JsonSerializer.Serialize(embeddingRequest);
            var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

            // Add authentication header
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await _client.PostAsync("/v1/embeddings", requestContent);

            // Assert
            // Should not return 501, but might return 400 for invalid input
            Assert.NotEqual(HttpStatusCode.NotImplemented, response.StatusCode);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Invalid Input: '{invalidInput}', Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseContent}");
        }

        [Fact]
        public async Task EmbeddingsEndpoint_Should_Return_Correct_Content_Type()
        {
            // Arrange
            var embeddingRequest = new EmbeddingRequest
            {
                Input = "Content type test",
                Model = "text-embedding-3-small",
                EncodingFormat = "float"
            };

            var json = JsonSerializer.Serialize(embeddingRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            
            var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

            // Add authentication header
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await _client.PostAsync("/v1/embeddings", requestContent);

            // Assert
            Assert.NotEqual(HttpStatusCode.NotImplemented, response.StatusCode);
            
            // Should return JSON content type
            var contentType = response.Content.Headers.ContentType?.MediaType;
            Assert.Equal("application/json", contentType);
            
            _output.WriteLine($"Content-Type: {response.Content.Headers.ContentType}");
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}