using System.Text.Json;
using ConduitLLM.Gateway.Middleware;
using Microsoft.AspNetCore.Http;

namespace ConduitLLM.Tests.Middleware
{
    public class UsageExtractorTests
    {
        [Theory]
        [InlineData("/v1/chat/completions", "chat")]
        [InlineData("/api/chat/completions", "chat")]
        [InlineData("/CHAT/COMPLETIONS", "chat")]
        [InlineData("/v1/completions", "completion")]
        [InlineData("/v1/embeddings", "embedding")]
        [InlineData("/v1/images/generations", "image")]
        [InlineData("/v1/audio/transcriptions", "transcription")]
        [InlineData("/v1/audio/speech", "tts")]
        [InlineData("/v1/conduit/videos/generations", "video")]
        [InlineData("/v1/conduit/functions/execute", "function")]
        [InlineData("/api/functions/execute/123", "function")]
        [InlineData("/unknown/path", "other")]
        [InlineData("", "other")]
        public void DetermineRequestType_WithVariousPaths_ReturnsCorrectType(string path, string expectedType)
        {
            var result = UsageExtractor.DetermineRequestType(new PathString(path));

            Assert.Equal(expectedType, result);
        }

        [Fact]
        public void DetermineRequestType_WithNullPath_ReturnsOther()
        {
            var result = UsageExtractor.DetermineRequestType(new PathString(null));

            Assert.Equal("other", result);
        }

        [Fact]
        public void DetermineRequestType_ChatCompletionsTakesPriorityOverCompletions()
        {
            var result = UsageExtractor.DetermineRequestType(new PathString("/v1/chat/completions"));

            Assert.Equal("chat", result);
        }

        [Fact]
        public void SerializeChatToolCalls_WithNull_ReturnsNull()
        {
            var result = UsageExtractor.SerializeChatToolCalls(null);

            Assert.Null(result);
        }

        [Fact]
        public void SerializeChatToolCalls_WithEmptyToolCalls_ReturnsNull()
        {
            var data = new ChatToolCallData { ToolCalls = new List<ChatToolCallItem>() };

            var result = UsageExtractor.SerializeChatToolCalls(data);

            Assert.Null(result);
        }

        [Fact]
        public void SerializeChatToolCalls_WithValidData_ReturnsValidJson()
        {
            var data = new ChatToolCallData
            {
                ToolCalls = new List<ChatToolCallItem>
                {
                    new()
                    {
                        Id = "call_123",
                        Type = "function",
                        FunctionName = "test_function",
                        HasArguments = true
                    }
                }
            };

            var result = UsageExtractor.SerializeChatToolCalls(data);

            Assert.NotNull(result);
            using var document = JsonDocument.Parse(result);
            var root = document.RootElement;
            Assert.Equal("chat_with_tools", root.GetProperty("type").GetString());
            Assert.Equal(1, root.GetProperty("toolCallCount").GetInt32());

            var toolCall = Assert.Single(root.GetProperty("toolCalls").EnumerateArray());
            Assert.Equal("call_123", toolCall.GetProperty("id").GetString());
            Assert.Equal("function", toolCall.GetProperty("type").GetString());
            Assert.Equal("test_function", toolCall.GetProperty("functionName").GetString());
            Assert.True(toolCall.GetProperty("hasArguments").GetBoolean());
        }

        [Fact]
        public void SerializeChatToolCalls_WithMultipleToolCalls_ReturnsCorrectCount()
        {
            var data = new ChatToolCallData
            {
                ToolCalls = new List<ChatToolCallItem>
                {
                    new() { Id = "call_1", FunctionName = "func1" },
                    new() { Id = "call_2", FunctionName = "func2" },
                    new() { Id = "call_3", FunctionName = "func3" }
                }
            };

            var result = UsageExtractor.SerializeChatToolCalls(data);

            Assert.NotNull(result);
            using var document = JsonDocument.Parse(result);
            Assert.Equal(3, document.RootElement.GetProperty("toolCallCount").GetInt32());
            Assert.Equal(3, document.RootElement.GetProperty("toolCalls").GetArrayLength());
        }

        [Fact]
        public void SerializeChatToolCalls_ReturnsNonIndentedJson()
        {
            var data = new ChatToolCallData
            {
                ToolCalls = new List<ChatToolCallItem>
                {
                    new() { Id = "call_1", FunctionName = "func1" }
                }
            };

            var result = UsageExtractor.SerializeChatToolCalls(data);

            Assert.NotNull(result);
            Assert.DoesNotContain("\n", result);
        }

        [Fact]
        public void SerializeChatToolCalls_WithNullFields_IncludesNullsInJson()
        {
            var data = new ChatToolCallData
            {
                ToolCalls = new List<ChatToolCallItem>
                {
                    new()
                    {
                        FunctionName = "test"
                    }
                }
            };

            var result = UsageExtractor.SerializeChatToolCalls(data);

            Assert.NotNull(result);
            using var document = JsonDocument.Parse(result);
            var toolCall = document.RootElement.GetProperty("toolCalls")[0];
            Assert.Equal(JsonValueKind.Null, toolCall.GetProperty("id").ValueKind);
            Assert.Equal(JsonValueKind.Null, toolCall.GetProperty("type").ValueKind);
            Assert.Equal("test", toolCall.GetProperty("functionName").GetString());
            Assert.False(toolCall.GetProperty("hasArguments").GetBoolean());
        }
    }
}
