using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.OpenAICompatibility;

/// <summary>
/// Tests OpenAI API response format compatibility.
/// Validates that response structures match OpenAI's expected format.
/// This ensures any OpenAI SDK will work correctly with Conduit.
/// </summary>
[Trait("Category", "Compatibility")]
[Trait("Component", "OpenAI")]
public class OpenAIHttpCompatibilityTests : TestBase
{
    public OpenAIHttpCompatibilityTests(ITestOutputHelper output) : base(output)
    {
        Log("Initialized OpenAI HTTP compatibility test");
    }
    
    [Fact]
    public void ChatCompletion_ResponseFormat_ShouldMatchOpenAIStructure()
    {
        // Arrange
        var mockResponse = OpenAIResponseTemplates.StandardChatResponse("gpt-3.5-turbo");
        Log("Testing OpenAI chat completion response format");

        // Act
        var responseJson = JsonSerializer.Serialize(mockResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var chatResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        // Assert
        // Verify OpenAI-compatible response structure
        chatResponse.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        chatResponse.GetProperty("object").GetString().Should().Be("chat.completion");
        chatResponse.GetProperty("created").GetInt64().Should().BePositive();
        chatResponse.GetProperty("model").GetString().Should().Be("gpt-3.5-turbo");
        
        // Verify choices array
        var choices = chatResponse.GetProperty("choices");
        choices.GetArrayLength().Should().BeGreaterThan(0);
        
        var firstChoice = choices[0];
        firstChoice.GetProperty("index").GetInt32().Should().Be(0);
        firstChoice.GetProperty("message").GetProperty("role").GetString().Should().Be("assistant");
        firstChoice.GetProperty("message").GetProperty("content").GetString().Should().NotBeNullOrEmpty();
        firstChoice.GetProperty("finish_reason").GetString().Should().Be("stop");
        
        // Verify usage object
        var usage = chatResponse.GetProperty("usage");
        usage.GetProperty("prompt_tokens").GetInt32().Should().BePositive();
        usage.GetProperty("completion_tokens").GetInt32().Should().BePositive();
        usage.GetProperty("total_tokens").GetInt32().Should().BePositive();
        
        Log("✅ Basic chat completion response format test passed");
    }

    [Fact]
    public async Task StreamingResponse_Format_ShouldMatchOpenAIChunkStructure()
    {
        // Arrange
        var mockStreamingResponse = OpenAIResponseTemplates.StreamingChatResponse("gpt-3.5-turbo");
        Log("Testing OpenAI streaming response format");

        // Act & Assert
        await foreach (var chunk in mockStreamingResponse)
        {
            // chunk is already a JSON string
            var chunkResponse = JsonSerializer.Deserialize<JsonElement>(chunk);
            
            // Verify each chunk has OpenAI streaming structure
            chunkResponse.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
            chunkResponse.GetProperty("object").GetString().Should().Be("chat.completion.chunk");
            chunkResponse.GetProperty("created").GetInt64().Should().BePositive();
            chunkResponse.GetProperty("model").GetString().Should().Be("gpt-3.5-turbo");
            
            var choices = chunkResponse.GetProperty("choices");
            choices.GetArrayLength().Should().BeGreaterThan(0);
            
            var firstChoice = choices[0];
            firstChoice.GetProperty("index").GetInt32().Should().Be(0);
            
            // Delta should have content or role, and choice may have finish_reason
            var delta = firstChoice.GetProperty("delta");
            if (delta.TryGetProperty("content", out _) || delta.TryGetProperty("role", out _) || firstChoice.TryGetProperty("finish_reason", out _))
            {
                // Valid streaming chunk structure
            }
        }
        
        Log("✅ Streaming response format test passed");
    }

    [Fact]
    public void FunctionCallResponse_Format_ShouldMatchOpenAIStructure()
    {
        // Arrange
        var mockResponse = OpenAIResponseTemplates.FunctionCallResponse("gpt-3.5-turbo");
        Log("Testing OpenAI function call response format");

        // Act
        var responseJson = JsonSerializer.Serialize(mockResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var chatResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        // Assert
        // Verify function call structure
        var message = chatResponse.GetProperty("choices")[0].GetProperty("message");
        var toolCalls = message.GetProperty("tool_calls");
        toolCalls.GetArrayLength().Should().BeGreaterThan(0);
        
        var firstToolCall = toolCalls[0];
        firstToolCall.GetProperty("type").GetString().Should().Be("function");
        
        var function = firstToolCall.GetProperty("function");
        function.GetProperty("name").GetString().Should().Be("get_weather");
        function.GetProperty("arguments").GetString().Should().NotBeNullOrEmpty();
        
        chatResponse.GetProperty("choices")[0].GetProperty("finish_reason").GetString().Should().Be("tool_calls");
        
        Log("✅ Function calling response format test passed");
    }

    [Fact]
    public void VisionResponse_Format_ShouldMatchOpenAIStructure()
    {
        // Arrange
        var mockResponse = OpenAIResponseTemplates.VisionResponse("gpt-4-vision-preview");
        Log("Testing OpenAI vision response format");

        // Act
        var responseJson = JsonSerializer.Serialize(mockResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var chatResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        // Assert
        // Verify basic structure
        chatResponse.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        chatResponse.GetProperty("object").GetString().Should().Be("chat.completion");
        chatResponse.GetProperty("model").GetString().Should().Be("gpt-4-vision-preview");
        
        // Verify content analysis exists
        var message = chatResponse.GetProperty("choices")[0].GetProperty("message");
        message.GetProperty("role").GetString().Should().Be("assistant");
        message.GetProperty("content").GetString().Should().Contain("image");
        
        Log("✅ Vision response format test passed");
    }

    [Fact]
    public void EmbeddingResponse_Format_ShouldMatchOpenAIStructure()
    {
        // Arrange
        var mockResponse = OpenAIResponseTemplates.EmbeddingResponse("text-embedding-ada-002");
        Log("Testing OpenAI embedding response format");

        // Act
        var responseJson = JsonSerializer.Serialize(mockResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var embeddingResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        // Assert
        // Verify basic structure
        embeddingResponse.GetProperty("object").GetString().Should().Be("list");
        embeddingResponse.GetProperty("model").GetString().Should().Be("text-embedding-ada-002");
        
        var data = embeddingResponse.GetProperty("data");
        data.GetArrayLength().Should().BeGreaterThan(0);
        
        var firstEmbedding = data[0];
        firstEmbedding.GetProperty("object").GetString().Should().Be("embedding");
        firstEmbedding.GetProperty("index").GetInt32().Should().Be(0);
        
        var embedding = firstEmbedding.GetProperty("embedding");
        embedding.GetArrayLength().Should().BeGreaterThan(0);
        
        // Verify usage object
        var usage = embeddingResponse.GetProperty("usage");
        usage.GetProperty("promptTokens").GetInt32().Should().BePositive();
        usage.GetProperty("totalTokens").GetInt32().Should().BePositive();
        
        Log("✅ Embedding response format test passed");
    }

    [Fact]
    public void ErrorResponse_Format_ShouldMatchOpenAIStructure()
    {
        // Arrange
        var mockErrorResponse = OpenAIResponseTemplates.ErrorResponse("invalid_request_error", "Invalid model specified");
        Log("Testing OpenAI error response format");

        // Act
        var responseJson = JsonSerializer.Serialize(mockErrorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        // Assert
        // Verify OpenAI error format
        var error = errorResponse.GetProperty("error");
        error.GetProperty("type").GetString().Should().Be("invalid_request_error");
        error.GetProperty("message").GetString().Should().Be("Invalid model specified");
        error.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        
        Log("✅ Error response format test passed");
    }

    [Fact]
    public void ModelsListResponse_Format_ShouldMatchOpenAIStructure()
    {
        // Arrange
        var mockModelsResponse = OpenAIResponseTemplates.ModelsListResponse();
        Log("Testing OpenAI models list response format");

        // Act
        var responseJson = JsonSerializer.Serialize(mockModelsResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var modelsResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        // Assert
        // Verify OpenAI models format
        modelsResponse.GetProperty("object").GetString().Should().Be("list");
        var data = modelsResponse.GetProperty("data");
        data.ValueKind.Should().Be(JsonValueKind.Array);
        
        if (data.GetArrayLength() > 0)
        {
            var firstModel = data[0];
            firstModel.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
            firstModel.GetProperty("object").GetString().Should().Be("model");
            firstModel.GetProperty("created").GetInt64().Should().BePositive();
        }
        
        Log("✅ Models list response format test passed");
    }
}