using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Validation;

using Xunit;

namespace ConduitLLM.Tests.Core.Models;

/// <summary>
/// Tests for the tool-related model classes in ConduitLLM.Core.Models
/// </summary>
public class ToolModelTests
{
    [Fact]
    public void Tool_Serialization_IsCorrect()
    {
        // Arrange
        var tool = new Tool
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = "get_weather",
                Description = "Get the current weather in a given location",
                Parameters = JsonNode.Parse(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""location"": {
                            ""type"": ""string"",
                            ""description"": ""The city and state, e.g. San Francisco, CA""
                        },
                        ""unit"": {
                            ""type"": ""string"",
                            ""enum"": [""celsius"", ""fahrenheit""]
                        }
                    },
                    ""required"": [""location""]
                }")?.AsObject()
            }
        };

        // Act
        var json = JsonSerializer.Serialize(tool);
        var deserializedTool = JsonSerializer.Deserialize<Tool>(json);

        // Assert
        Assert.NotNull(deserializedTool);
        Assert.Equal("function", deserializedTool.Type);
        Assert.Equal("get_weather", deserializedTool.Function.Name);
        Assert.Equal("Get the current weather in a given location", deserializedTool.Function.Description);
        Assert.NotNull(deserializedTool.Function.Parameters);
    }

    [Fact]
    public void ToolChoice_Specific_CreatesCorrectFormat()
    {
        // Arrange & Act
        var toolChoice = ToolChoice.Function("get_weather");
        var serialized = toolChoice.GetSerializedValue();

        // Assert
        Assert.NotNull(serialized);
        var json = JsonSerializer.Serialize(serialized);
        Assert.Contains("\"type\":\"function\"", json);
        Assert.Contains("\"name\":\"get_weather\"", json);
    }

    [Fact]
    public void ToolChoice_NoneAndAuto_CreatesCorrectFormat()
    {
        // Arrange & Act
        var noneChoice = ToolChoice.None;
        var autoChoice = ToolChoice.Auto;

        // Assert
        Assert.Equal("none", noneChoice.GetSerializedValue());
        Assert.Equal("auto", autoChoice.GetSerializedValue());
    }

    [Fact]
    public void ToolCall_Serialization_IsCorrect()
    {
        // Arrange
        var toolCall = new ToolCall
        {
            Id = "call_abc123",
            Type = "function",
            Function = new FunctionCall
            {
                Name = "get_weather",
                Arguments = "{\"location\":\"San Francisco, CA\",\"unit\":\"celsius\"}"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(toolCall);
        var deserializedToolCall = JsonSerializer.Deserialize<ToolCall>(json);

        // Assert
        Assert.NotNull(deserializedToolCall);
        Assert.Equal("call_abc123", deserializedToolCall.Id);
        Assert.Equal("function", deserializedToolCall.Type);
        Assert.Equal("get_weather", deserializedToolCall.Function.Name);
        Assert.Contains("San Francisco", deserializedToolCall.Function.Arguments);
    }

    [Fact]
    public void Message_WithToolCalls_SerializationIsCorrect()
    {
        // Arrange
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Content = null, // Content is null when tool calls are present
            ToolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_abc123",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "get_weather",
                        Arguments = "{\"location\":\"San Francisco, CA\"}"
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(message);
        var deserializedMessage = JsonSerializer.Deserialize<Message>(json);

        // Assert
        Assert.NotNull(deserializedMessage);
        Assert.Equal(MessageRole.Assistant, deserializedMessage.Role);
        Assert.Null(deserializedMessage.Content);
        Assert.NotNull(deserializedMessage.ToolCalls);
        Assert.Single(deserializedMessage.ToolCalls);
        Assert.Equal("get_weather", deserializedMessage.ToolCalls[0].Function.Name);
    }

    [Fact]
    public void Message_ToolResponse_SerializationIsCorrect()
    {
        // Arrange
        var message = new Message
        {
            Role = MessageRole.Tool,
            Content = "{\"temperature\":22,\"unit\":\"celsius\"}",
            ToolCallId = "call_abc123"
        };

        // Act
        var json = JsonSerializer.Serialize(message);
        var deserializedMessage = JsonSerializer.Deserialize<Message>(json);

        // Assert
        Assert.NotNull(deserializedMessage);
        Assert.Equal(MessageRole.Tool, deserializedMessage.Role);
        Assert.NotNull(deserializedMessage.Content);
        Assert.Equal("call_abc123", deserializedMessage.ToolCallId);
    }

    [Fact]
    public void ToolValidation_ValidatesTool_SucceedsForValidTool()
    {
        // Arrange
        var tool = new Tool
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = "get_weather",
                Description = "Get the weather"
            }
        };

        // Act & Assert
        ToolValidation.ValidateTools(new List<Tool> { tool }); // Should not throw
    }

    [Fact]
    public void ToolValidation_ValidateToolCall_SucceedsForValidToolCall()
    {
        // Arrange
        var toolCall = new ToolCall
        {
            Id = "call_123",
            Type = "function",
            Function = new FunctionCall
            {
                Name = "get_weather",
                Arguments = "{\"location\":\"San Francisco\"}"
            }
        };

        // Act & Assert
        ToolValidation.ValidateToolCalls(new List<ToolCall> { toolCall }); // Should not throw
    }

    [Fact]
    public void ToolValidation_ValidateToolWithInvalidType_ThrowsValidationException()
    {
        // Arrange
        var tool = new Tool
        {
            Type = "invalid_type", // Only "function" is currently supported
            Function = new FunctionDefinition
            {
                Name = "get_weather",
                Description = "Get the weather"
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() =>
            ToolValidation.ValidateTools(new List<Tool> { tool }));
        Assert.Contains("not supported", ex.Message);
    }

    [Fact]
    public void ToolValidation_ValidateFunctionCallWithInvalidJson_ThrowsValidationException()
    {
        // Arrange
        var functionCall = new FunctionCall
        {
            Name = "get_weather",
            Arguments = "{invalid json}" // Invalid JSON format
        };

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() =>
            ToolValidation.ValidateFunctionCall(functionCall));
        Assert.Contains("must be valid JSON", ex.Message);
    }

    [Fact]
    public void ChatCompletionRequest_WithToolsAndToolChoice_SerializationIsCorrect()
    {
        // Arrange
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<Message> { new Message { Role = "user", Content = "What's the weather like?" } },
            Tools = new List<Tool>
            {
                new Tool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "get_weather",
                        Description = "Get weather information"
                    }
                }
            },
            ToolChoice = ToolChoice.Function("get_weather")
        };

        // Act
        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<ChatCompletionRequest>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Tools);
        Assert.Single(deserialized.Tools);
        Assert.Equal("function", deserialized.Tools[0].Type);
        Assert.Equal("get_weather", deserialized.Tools[0].Function.Name);
        Assert.NotNull(deserialized.ToolChoice);
    }
}
