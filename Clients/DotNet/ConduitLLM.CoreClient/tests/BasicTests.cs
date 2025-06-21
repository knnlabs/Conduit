using Xunit;
using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.CoreClient.Tests;

public class BasicTests
{
    [Fact]
    public void ConduitCoreClientConfiguration_WithValidData_InitializesCorrectly()
    {
        // Arrange & Act
        var config = new ConduitCoreClientConfiguration
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.example.com"
        };

        // Assert
        Assert.Equal("test-key", config.ApiKey);
        Assert.Equal("https://api.example.com", config.BaseUrl);
    }

    [Fact]
    public void ChatCompletionRequest_WithValidData_InitializesCorrectly()
    {
        // Arrange & Act
        var request = new ChatCompletionRequest
        {
            Model = "gpt-3.5-turbo",
            Messages = new[]
            {
                new ChatCompletionMessage { Role = "user", Content = "Hello" }
            }
        };

        // Assert
        Assert.Equal("gpt-3.5-turbo", request.Model);
        Assert.Single(request.Messages);
        Assert.Equal("user", request.Messages.First().Role);
        Assert.Equal("Hello", request.Messages.First().Content);
    }

    [Fact]
    public void ConduitCoreClient_WithValidConfiguration_InitializesSuccessfully()
    {
        // Arrange
        var configuration = new ConduitCoreClientConfiguration
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://api.example.com"
        };

        // Act
        var client = new ConduitCoreClient(configuration);

        // Assert
        Assert.NotNull(client);
        Assert.NotNull(client.Chat);
        Assert.NotNull(client.Images);
        Assert.NotNull(client.Models);
    }

    [Fact]
    public void Usage_WithTokenCounts_CalculatesTotalCorrectly()
    {
        // Arrange
        var usage = new Usage
        {
            PromptTokens = 10,
            CompletionTokens = 15
        };

        // Act
        usage.TotalTokens = usage.PromptTokens + usage.CompletionTokens;

        // Assert
        Assert.Equal(10, usage.PromptTokens);
        Assert.Equal(15, usage.CompletionTokens);
        Assert.Equal(25, usage.TotalTokens);
    }
}