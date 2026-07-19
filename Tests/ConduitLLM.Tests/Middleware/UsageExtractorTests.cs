using System.Text.Json;
using ConduitLLM.Gateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Middleware
{
    public class UsageExtractorTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public UsageExtractorTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public void ExtractUsage_AnthropicTokens_MarksCachedTokensAsExcludedFromPrompt()
        {
            using var document = JsonDocument.Parse("""
                {
                    "input_tokens": 500,
                    "output_tokens": 25,
                    "cache_read_input_tokens": 10000
                }
                """);

            var usage = UsageExtractor.ExtractUsage(document.RootElement, _mockLogger.Object);

            Assert.NotNull(usage);
            Assert.Equal(500, usage.PromptTokens);
            Assert.Equal(10000, usage.CachedInputTokens);
            Assert.False(usage.CachedInputTokensIncludedInPrompt);
        }

        #region DetermineRequestType Tests

        [Theory]
        [InlineData("/v1/chat/completions", "chat")]
        [InlineData("/api/chat/completions", "chat")]
        [InlineData("/CHAT/COMPLETIONS", "chat")]
        [InlineData("/v1/completions", "completion")]
        [InlineData("/v1/embeddings", "embedding")]
        [InlineData("/v1/images/generations", "image")]
        [InlineData("/v1/audio/transcriptions", "transcription")]
        [InlineData("/v1/audio/speech", "tts")]
        [InlineData("/v1/videos/generations", "video")]
        [InlineData("/v1/functions/execute", "function")]
        [InlineData("/api/functions/execute/123", "function")]
        [InlineData("/unknown/path", "other")]
        [InlineData("", "other")]
        public void DetermineRequestType_WithVariousPaths_ReturnsCorrectType(string path, string expectedType)
        {
            // Arrange
            var pathString = new PathString(path);

            // Act
            var result = UsageExtractor.DetermineRequestType(pathString);

            // Assert
            Assert.Equal(expectedType, result);
        }

        [Fact]
        public void DetermineRequestType_WithNullPath_ReturnsOther()
        {
            // Arrange
            var pathString = new PathString(null);

            // Act
            var result = UsageExtractor.DetermineRequestType(pathString);

            // Assert
            Assert.Equal("other", result);
        }

        [Fact]
        public void DetermineRequestType_ChatCompletionsTakesPriorityOverCompletions()
        {
            // Arrange - /chat/completions should return "chat", not "completion"
            var pathString = new PathString("/v1/chat/completions");

            // Act
            var result = UsageExtractor.DetermineRequestType(pathString);

            // Assert
            Assert.Equal("chat", result);
        }

        #endregion

        #region ExtractChatToolCalls Tests

        [Fact]
        public void ExtractChatToolCalls_WithModernToolCallsFormat_ExtractsCorrectly()
        {
            // Arrange
            var responseBody = @"{
                ""choices"": [{
                    ""message"": {
                        ""tool_calls"": [
                            {
                                ""id"": ""call_abc123"",
                                ""type"": ""function"",
                                ""function"": {
                                    ""name"": ""get_weather"",
                                    ""arguments"": ""{\""location\"": \""Boston\""}""
                                }
                            }
                        ]
                    }
                }]
            }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.ToolCalls);
            var toolCall = result.ToolCalls[0];
            Assert.Equal("call_abc123", toolCall.Id);
            Assert.Equal("function", toolCall.Type);
            Assert.Equal("get_weather", toolCall.FunctionName);
            Assert.True(toolCall.HasArguments);
        }

        [Fact]
        public void ExtractChatToolCalls_WithMultipleToolCalls_ExtractsAll()
        {
            // Arrange
            var responseBody = @"{
                ""choices"": [{
                    ""message"": {
                        ""tool_calls"": [
                            {
                                ""id"": ""call_1"",
                                ""type"": ""function"",
                                ""function"": {
                                    ""name"": ""get_weather"",
                                    ""arguments"": ""{}""
                                }
                            },
                            {
                                ""id"": ""call_2"",
                                ""type"": ""function"",
                                ""function"": {
                                    ""name"": ""get_time"",
                                    ""arguments"": ""{}""
                                }
                            },
                            {
                                ""id"": ""call_3"",
                                ""type"": ""function"",
                                ""function"": {
                                    ""name"": ""search_web""
                                }
                            }
                        ]
                    }
                }]
            }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.ToolCalls.Count);
            Assert.Equal("get_weather", result.ToolCalls[0].FunctionName);
            Assert.Equal("get_time", result.ToolCalls[1].FunctionName);
            Assert.Equal("search_web", result.ToolCalls[2].FunctionName);
            Assert.True(result.ToolCalls[0].HasArguments);
            Assert.True(result.ToolCalls[1].HasArguments);
            Assert.False(result.ToolCalls[2].HasArguments); // No arguments property
        }

        [Fact]
        public void ExtractChatToolCalls_WithLegacyFunctionCallFormat_ExtractsCorrectly()
        {
            // Arrange - Legacy format used by older OpenAI models
            var responseBody = @"{
                ""choices"": [{
                    ""message"": {
                        ""function_call"": {
                            ""name"": ""get_current_weather"",
                            ""arguments"": ""{\""location\"": \""San Francisco\""}""
                        }
                    }
                }]
            }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.ToolCalls);
            var toolCall = result.ToolCalls[0];
            Assert.Null(toolCall.Id); // Legacy format doesn't have ID
            Assert.Equal("function", toolCall.Type);
            Assert.Equal("get_current_weather", toolCall.FunctionName);
            Assert.True(toolCall.HasArguments);
        }

        [Fact]
        public void ExtractChatToolCalls_WithBothFormatsInDifferentChoices_ExtractsAll()
        {
            // Arrange - Edge case where both formats might appear
            var responseBody = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""tool_calls"": [{
                                ""id"": ""call_modern"",
                                ""type"": ""function"",
                                ""function"": { ""name"": ""modern_function"" }
                            }]
                        }
                    },
                    {
                        ""message"": {
                            ""function_call"": {
                                ""name"": ""legacy_function""
                            }
                        }
                    }
                ]
            }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.ToolCalls.Count);
            Assert.Contains(result.ToolCalls, tc => tc.FunctionName == "modern_function");
            Assert.Contains(result.ToolCalls, tc => tc.FunctionName == "legacy_function");
        }

        [Fact]
        public void ExtractChatToolCalls_WithNoToolCalls_ReturnsNull()
        {
            // Arrange - Standard text response without tool calls
            var responseBody = @"{
                ""choices"": [{
                    ""message"": {
                        ""content"": ""Hello, how can I help you today?""
                    }
                }]
            }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractChatToolCalls_WithEmptyChoicesArray_ReturnsNull()
        {
            // Arrange
            var responseBody = @"{ ""choices"": [] }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractChatToolCalls_WithNoChoicesProperty_ReturnsNull()
        {
            // Arrange
            var responseBody = @"{ ""id"": ""chatcmpl-123"", ""object"": ""chat.completion"" }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractChatToolCalls_WithMalformedJson_ReturnsNull()
        {
            // Arrange
            var responseBody = "{ invalid json }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.Null(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void ExtractChatToolCalls_WithEmptyString_ReturnsNull()
        {
            // Arrange
            var responseBody = "";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractChatToolCalls_WithEmptyToolCallsArray_ReturnsNull()
        {
            // Arrange
            var responseBody = @"{
                ""choices"": [{
                    ""message"": {
                        ""tool_calls"": []
                    }
                }]
            }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractChatToolCalls_WithMissingMessageProperty_ReturnsNull()
        {
            // Arrange
            var responseBody = @"{
                ""choices"": [{
                    ""index"": 0,
                    ""finish_reason"": ""stop""
                }]
            }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractChatToolCalls_WithPartialToolCallData_ExtractsAvailableFields()
        {
            // Arrange - Tool call with only some fields populated
            var responseBody = @"{
                ""choices"": [{
                    ""message"": {
                        ""tool_calls"": [{
                            ""type"": ""function"",
                            ""function"": {
                                ""name"": ""minimal_function""
                            }
                        }]
                    }
                }]
            }";

            // Act
            var result = UsageExtractor.ExtractChatToolCalls(responseBody, _mockLogger.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.ToolCalls);
            var toolCall = result.ToolCalls[0];
            Assert.Null(toolCall.Id); // No ID provided
            Assert.Equal("function", toolCall.Type);
            Assert.Equal("minimal_function", toolCall.FunctionName);
            Assert.False(toolCall.HasArguments); // No arguments property
        }

        #endregion

        #region SerializeChatToolCalls Tests

        [Fact]
        public void SerializeChatToolCalls_WithNull_ReturnsNull()
        {
            // Act
            var result = UsageExtractor.SerializeChatToolCalls(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SerializeChatToolCalls_WithEmptyToolCalls_ReturnsNull()
        {
            // Arrange
            var data = new ChatToolCallData { ToolCalls = new List<ChatToolCallItem>() };

            // Act
            var result = UsageExtractor.SerializeChatToolCalls(data);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SerializeChatToolCalls_WithValidData_ReturnsValidJson()
        {
            // Arrange
            var data = new ChatToolCallData
            {
                ToolCalls = new List<ChatToolCallItem>
                {
                    new ChatToolCallItem
                    {
                        Id = "call_123",
                        Type = "function",
                        FunctionName = "test_function",
                        HasArguments = true
                    }
                }
            };

            // Act
            var result = UsageExtractor.SerializeChatToolCalls(data);

            // Assert
            Assert.NotNull(result);

            // Parse and verify the JSON structure
            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;

            Assert.Equal("chat_with_tools", root.GetProperty("type").GetString());
            Assert.Equal(1, root.GetProperty("toolCallCount").GetInt32());

            var toolCalls = root.GetProperty("toolCalls");
            Assert.Single(toolCalls.EnumerateArray());

            var toolCall = toolCalls[0];
            Assert.Equal("call_123", toolCall.GetProperty("id").GetString());
            Assert.Equal("function", toolCall.GetProperty("type").GetString());
            Assert.Equal("test_function", toolCall.GetProperty("functionName").GetString());
            Assert.True(toolCall.GetProperty("hasArguments").GetBoolean());
        }

        [Fact]
        public void SerializeChatToolCalls_WithMultipleToolCalls_ReturnsCorrectCount()
        {
            // Arrange
            var data = new ChatToolCallData
            {
                ToolCalls = new List<ChatToolCallItem>
                {
                    new ChatToolCallItem { Id = "call_1", FunctionName = "func1" },
                    new ChatToolCallItem { Id = "call_2", FunctionName = "func2" },
                    new ChatToolCallItem { Id = "call_3", FunctionName = "func3" }
                }
            };

            // Act
            var result = UsageExtractor.SerializeChatToolCalls(data);

            // Assert
            Assert.NotNull(result);

            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;

            Assert.Equal(3, root.GetProperty("toolCallCount").GetInt32());
            Assert.Equal(3, root.GetProperty("toolCalls").GetArrayLength());
        }

        [Fact]
        public void SerializeChatToolCalls_ReturnsNonIndentedJson()
        {
            // Arrange
            var data = new ChatToolCallData
            {
                ToolCalls = new List<ChatToolCallItem>
                {
                    new ChatToolCallItem { Id = "call_1", FunctionName = "func1" }
                }
            };

            // Act
            var result = UsageExtractor.SerializeChatToolCalls(data);

            // Assert
            Assert.NotNull(result);
            Assert.DoesNotContain("\n", result); // Should not have newlines (not indented)
        }

        [Fact]
        public void SerializeChatToolCalls_WithNullFields_IncludesNullsInJson()
        {
            // Arrange
            var data = new ChatToolCallData
            {
                ToolCalls = new List<ChatToolCallItem>
                {
                    new ChatToolCallItem
                    {
                        Id = null,
                        Type = null,
                        FunctionName = "test",
                        HasArguments = false
                    }
                }
            };

            // Act
            var result = UsageExtractor.SerializeChatToolCalls(data);

            // Assert
            Assert.NotNull(result);

            using var doc = JsonDocument.Parse(result);
            var toolCall = doc.RootElement.GetProperty("toolCalls")[0];

            Assert.Equal(JsonValueKind.Null, toolCall.GetProperty("id").ValueKind);
            Assert.Equal(JsonValueKind.Null, toolCall.GetProperty("type").ValueKind);
            Assert.Equal("test", toolCall.GetProperty("functionName").GetString());
            Assert.False(toolCall.GetProperty("hasArguments").GetBoolean());
        }

        #endregion

        #region ExtractChatToolCalls and SerializeChatToolCalls Integration

        [Fact]
        public void ExtractAndSerialize_RoundTrip_PreservesData()
        {
            // Arrange
            var originalResponse = @"{
                ""choices"": [{
                    ""message"": {
                        ""tool_calls"": [
                            {
                                ""id"": ""call_roundtrip"",
                                ""type"": ""function"",
                                ""function"": {
                                    ""name"": ""roundtrip_function"",
                                    ""arguments"": ""{\""key\"": \""value\""}""
                                }
                            }
                        ]
                    }
                }]
            }";

            // Act
            var extracted = UsageExtractor.ExtractChatToolCalls(originalResponse, _mockLogger.Object);
            var serialized = UsageExtractor.SerializeChatToolCalls(extracted);

            // Assert
            Assert.NotNull(serialized);

            using var doc = JsonDocument.Parse(serialized);
            var toolCall = doc.RootElement.GetProperty("toolCalls")[0];

            Assert.Equal("call_roundtrip", toolCall.GetProperty("id").GetString());
            Assert.Equal("function", toolCall.GetProperty("type").GetString());
            Assert.Equal("roundtrip_function", toolCall.GetProperty("functionName").GetString());
            Assert.True(toolCall.GetProperty("hasArguments").GetBoolean());
        }

        #endregion
    }
}
