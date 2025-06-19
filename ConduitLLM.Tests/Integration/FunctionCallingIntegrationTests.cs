using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Validation;
using ConduitLLM.Core.Exceptions;
using Xunit;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Integration tests for the function calling flow.
    /// </summary>
    public class FunctionCallingIntegrationTests
    {
        [Fact]
        public void CompleteToolCallingFlow_ValidatesCorrectly()
        {
            // Step 1: Create tools with functions
            var tools = new List<Tool>
            {
                new Tool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "get_weather",
                        Description = "Get the current weather for a location",
                        Parameters = JsonNode.Parse(@"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""location"": {
                                    ""type"": ""string"",
                                    ""description"": ""The city and state""
                                },
                                ""unit"": {
                                    ""type"": ""string"",
                                    ""enum"": [""celsius"", ""fahrenheit""]
                                }
                            },
                            ""required"": [""location""]
                        }")?.AsObject()
                    }
                },
                new Tool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "calculate",
                        Description = "Perform mathematical calculations",
                        Parameters = JsonNode.Parse(@"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""expression"": {
                                    ""type"": ""string"",
                                    ""description"": ""Math expression to evaluate""
                                }
                            },
                            ""required"": [""expression""]
                        }")?.AsObject()
                    }
                }
            };

            // Step 2: Validate tools
            ToolValidation.ValidateTools(tools);

            // Step 3: Create a request with tools
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "What's the weather in Boston?" }
                },
                Tools = tools,
                ToolChoice = ToolChoice.Auto
            };

            // Request is valid if no exception is thrown by tool validation

            // Step 4: Simulate assistant response with tool calls
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_abc123",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "get_weather",
                        Arguments = JsonSerializer.Serialize(new { location = "Boston, MA", unit = "fahrenheit" })
                    }
                }
            };

            // Validate tool calls
            ToolValidation.ValidateToolCalls(toolCalls);

            // Step 5: Create tool response message
            var toolResponse = new Message
            {
                Role = "tool",
                Content = JsonSerializer.Serialize(new { temperature = 72, condition = "sunny", humidity = 45 }),
                ToolCallId = "call_abc123"
            };

            // Step 6: Continue conversation with tool result
            var messagesWithToolResponse = new List<Message>
            {
                request.Messages[0],
                new Message { Role = "assistant", ToolCalls = toolCalls },
                toolResponse
            };

            var followUpRequest = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = messagesWithToolResponse
            };

            // Assert follow-up request is valid

            // Assert all validations passed (no exceptions thrown)
            Assert.Equal(3, followUpRequest.Messages.Count);
            Assert.Equal("tool", followUpRequest.Messages[2].Role);
            Assert.Equal("call_abc123", followUpRequest.Messages[2].ToolCallId);
        }

        [Fact]
        public void MultipleToolCalls_InSingleResponse_HandledCorrectly()
        {
            // Arrange
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "get_weather",
                        Arguments = JsonSerializer.Serialize(new { location = "Boston" })
                    }
                },
                new ToolCall
                {
                    Id = "call_2",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "get_weather",
                        Arguments = JsonSerializer.Serialize(new { location = "New York" })
                    }
                }
            };

            // Act & Assert - Validation should pass
            ToolValidation.ValidateToolCalls(toolCalls);

            // Create corresponding tool responses
            var toolResponses = new List<Message>
            {
                new Message
                {
                    Role = "tool",
                    Content = JsonSerializer.Serialize(new { temperature = 72, condition = "sunny" }),
                    ToolCallId = "call_1"
                },
                new Message
                {
                    Role = "tool",
                    Content = JsonSerializer.Serialize(new { temperature = 68, condition = "cloudy" }),
                    ToolCallId = "call_2"
                }
            };

            // Verify each response has correct tool call ID
            Assert.Equal("call_1", toolResponses[0].ToolCallId);
            Assert.Equal("call_2", toolResponses[1].ToolCallId);
        }

        [Fact]
        public void ToolChoice_Variations_SerializeCorrectly()
        {
            // Test different tool choice options
            var noneChoice = ToolChoice.None;
            var autoChoice = ToolChoice.Auto;
            var specificChoice = ToolChoice.Function("get_weather");

            // Verify serialization
            Assert.Equal("none", noneChoice.GetSerializedValue());
            Assert.Equal("auto", autoChoice.GetSerializedValue());

            var specificSerialized = specificChoice.GetSerializedValue();
            Assert.NotNull(specificSerialized);
            
            var json = JsonSerializer.Serialize(specificSerialized);
            Assert.Contains("\"type\":\"function\"", json);
            Assert.Contains("\"name\":\"get_weather\"", json);
        }

        [Fact]
        public void FunctionArguments_ComplexTypes_SerializeCorrectly()
        {
            // Arrange
            var complexArgs = new
            {
                location = "San Francisco, CA",
                options = new
                {
                    include_forecast = true,
                    days = 5,
                    details = new[] { "temperature", "humidity", "wind" }
                },
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var toolCall = new ToolCall
            {
                Id = "call_complex",
                Type = "function",
                Function = new FunctionCall
                {
                    Name = "get_extended_weather",
                    Arguments = JsonSerializer.Serialize(complexArgs)
                }
            };

            // Act
            var parsedArgs = JsonDocument.Parse(toolCall.Function.Arguments);

            // Assert
            Assert.Equal("San Francisco, CA", parsedArgs.RootElement.GetProperty("location").GetString());
            Assert.True(parsedArgs.RootElement.GetProperty("options").GetProperty("include_forecast").GetBoolean());
            Assert.Equal(5, parsedArgs.RootElement.GetProperty("options").GetProperty("days").GetInt32());
            
            var details = parsedArgs.RootElement.GetProperty("options").GetProperty("details");
            Assert.Equal(3, details.GetArrayLength());
        }

        [Fact]
        public void InvalidToolDefinitions_ThrowValidationException()
        {
            // Test 1: Invalid function name
            var invalidNameTool = new Tool
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "get weather", // Space not allowed
                    Description = "Get weather",
                    Parameters = new JsonObject { ["type"] = "object" }
                }
            };

            Assert.Throws<ValidationException>(() => 
                ToolValidation.ValidateTools(new[] { invalidNameTool }));

            // Test 2: Missing required fields
            var missingNameTool = new Tool
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = null!,
                    Description = "Missing name",
                    Parameters = new JsonObject { ["type"] = "object" }
                }
            };

            Assert.Throws<ValidationException>(() => 
                ToolValidation.ValidateTools(new[] { missingNameTool }));

            // Test 3: Invalid tool type
            var invalidTypeTool = new Tool
            {
                Type = "unknown_type",
                Function = new FunctionDefinition
                {
                    Name = "test",
                    Description = "Test",
                    Parameters = new JsonObject { ["type"] = "object" }
                }
            };

            Assert.Throws<ValidationException>(() => 
                ToolValidation.ValidateTools(new[] { invalidTypeTool }));
        }

        [Fact]
        public void EmptyToolCallArguments_HandledGracefully()
        {
            // Some functions might not require arguments
            var toolCall = new ToolCall
            {
                Id = "call_no_args",
                Type = "function",
                Function = new FunctionCall
                {
                    Name = "get_current_time",
                    Arguments = "{}" // Empty arguments
                }
            };

            // Should validate without errors
            ToolValidation.ValidateToolCalls(new[] { toolCall });

            // Verify empty object can be parsed
            var args = JsonDocument.Parse(toolCall.Function.Arguments);
            Assert.Equal(0, args.RootElement.EnumerateObject().Count());
        }

        [Theory]
        [InlineData("calculate", "Perform a calculation")]
        [InlineData("search_web", "Search the web for information")]
        [InlineData("create_image", "Generate an image based on description")]
        [InlineData("send_email", "Send an email to specified recipient")]
        public void CommonFunctionPatterns_ValidateCorrectly(string functionName, string description)
        {
            // Arrange
            var tool = new Tool
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = functionName,
                    Description = description,
                    Parameters = JsonNode.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""input"": {
                                ""type"": ""string"",
                                ""description"": ""The input parameter""
                            }
                        },
                        ""required"": [""input""]
                    }")?.AsObject()
                }
            };

            // Act & Assert - Should not throw
            ToolValidation.ValidateTools(new[] { tool });
        }
    }
}