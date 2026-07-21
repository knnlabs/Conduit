using System.Text.Json;
using ConduitLLM.Gateway.Models;
using ConduitLLM.Gateway.Utilities;

namespace ConduitLLM.Tests.Http.Utilities
{
    public class FunctionExecutionSerializerTests
    {
        #region SerializeFunctionExecutionResults Tests

        [Fact]
        public void SerializeFunctionExecutionResults_WithNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                FunctionExecutionSerializer.SerializeFunctionExecutionResults(null!));
        }

        [Fact]
        public void SerializeFunctionExecutionResults_WithEmptyList_ReturnsValidJson()
        {
            // Arrange
            var results = new List<FunctionExecutionResultForLogging>();

            // Act
            var json = FunctionExecutionSerializer.SerializeFunctionExecutionResults(results);

            // Assert
            Assert.NotNull(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("chat_with_functions", root.GetProperty("type").GetString());
            Assert.Equal(0, root.GetProperty("functionCallCount").GetInt32());
            Assert.Equal(0m, root.GetProperty("totalCost").GetDecimal());
            Assert.Equal(0, root.GetProperty("successCount").GetInt32());
            Assert.Equal(0, root.GetProperty("failedCount").GetInt32());
            Assert.Equal(0, root.GetProperty("functionCalls").GetArrayLength());
        }

        [Fact]
        public void SerializeFunctionExecutionResults_WithSingleCompletedResult_SerializesCorrectly()
        {
            // Arrange
            var executionId = Guid.NewGuid();
            var results = new List<FunctionExecutionResultForLogging>
            {
                new FunctionExecutionResultForLogging
                {
                    ToolCallId = "call_123",
                    FunctionName = "get_weather",
                    Status = "completed",
                    Cost = 0.005m,
                    ErrorMessage = null,
                    FunctionExecutionId = executionId
                }
            };

            // Act
            var json = FunctionExecutionSerializer.SerializeFunctionExecutionResults(results);

            // Assert
            Assert.NotNull(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("chat_with_functions", root.GetProperty("type").GetString());
            Assert.Equal(1, root.GetProperty("functionCallCount").GetInt32());
            Assert.Equal(0.005m, root.GetProperty("totalCost").GetDecimal());
            Assert.Equal(1, root.GetProperty("successCount").GetInt32());
            Assert.Equal(0, root.GetProperty("failedCount").GetInt32());

            var functionCalls = root.GetProperty("functionCalls");
            Assert.Equal(1, functionCalls.GetArrayLength());

            var call = functionCalls[0];
            Assert.Equal("call_123", call.GetProperty("toolCallId").GetString());
            Assert.Equal("get_weather", call.GetProperty("functionName").GetString());
            Assert.Equal("completed", call.GetProperty("status").GetString());
            Assert.Equal(0.005m, call.GetProperty("cost").GetDecimal());
            Assert.Equal(JsonValueKind.Null, call.GetProperty("errorMessage").ValueKind);
            Assert.Equal(executionId.ToString(), call.GetProperty("functionExecutionId").GetString());
        }

        [Fact]
        public void SerializeFunctionExecutionResults_WithSingleFailedResult_SerializesCorrectly()
        {
            // Arrange
            var executionId = Guid.NewGuid();
            var results = new List<FunctionExecutionResultForLogging>
            {
                new FunctionExecutionResultForLogging
                {
                    ToolCallId = "call_456",
                    FunctionName = "search_web",
                    Status = "failed",
                    Cost = 0.001m,
                    ErrorMessage = "Rate limit exceeded",
                    FunctionExecutionId = executionId
                }
            };

            // Act
            var json = FunctionExecutionSerializer.SerializeFunctionExecutionResults(results);

            // Assert
            Assert.NotNull(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(1, root.GetProperty("functionCallCount").GetInt32());
            Assert.Equal(0.001m, root.GetProperty("totalCost").GetDecimal());
            Assert.Equal(0, root.GetProperty("successCount").GetInt32());
            Assert.Equal(1, root.GetProperty("failedCount").GetInt32());

            var call = root.GetProperty("functionCalls")[0];
            Assert.Equal("failed", call.GetProperty("status").GetString());
            Assert.Equal("Rate limit exceeded", call.GetProperty("errorMessage").GetString());
        }

        [Fact]
        public void SerializeFunctionExecutionResults_WithMixedResults_CalculatesCountsCorrectly()
        {
            // Arrange
            var results = new List<FunctionExecutionResultForLogging>
            {
                new FunctionExecutionResultForLogging
                {
                    ToolCallId = "call_1",
                    FunctionName = "func1",
                    Status = "completed",
                    Cost = 0.01m,
                    FunctionExecutionId = Guid.NewGuid()
                },
                new FunctionExecutionResultForLogging
                {
                    ToolCallId = "call_2",
                    FunctionName = "func2",
                    Status = "completed",
                    Cost = 0.02m,
                    FunctionExecutionId = Guid.NewGuid()
                },
                new FunctionExecutionResultForLogging
                {
                    ToolCallId = "call_3",
                    FunctionName = "func3",
                    Status = "failed",
                    Cost = 0.005m,
                    ErrorMessage = "Timeout",
                    FunctionExecutionId = Guid.NewGuid()
                },
                new FunctionExecutionResultForLogging
                {
                    ToolCallId = "call_4",
                    FunctionName = "func4",
                    Status = "completed",
                    Cost = null, // No cost
                    FunctionExecutionId = Guid.NewGuid()
                }
            };

            // Act
            var json = FunctionExecutionSerializer.SerializeFunctionExecutionResults(results);

            // Assert
            Assert.NotNull(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(4, root.GetProperty("functionCallCount").GetInt32());
            Assert.Equal(0.035m, root.GetProperty("totalCost").GetDecimal()); // 0.01 + 0.02 + 0.005 + 0 = 0.035
            Assert.Equal(3, root.GetProperty("successCount").GetInt32());
            Assert.Equal(1, root.GetProperty("failedCount").GetInt32());
            Assert.Equal(4, root.GetProperty("functionCalls").GetArrayLength());
        }

        [Fact]
        public void SerializeFunctionExecutionResults_WithNullFields_SerializesNullsCorrectly()
        {
            // Arrange
            var results = new List<FunctionExecutionResultForLogging>
            {
                new FunctionExecutionResultForLogging
                {
                    ToolCallId = null,
                    FunctionName = null,
                    Status = null,
                    Cost = null,
                    ErrorMessage = null,
                    FunctionExecutionId = null
                }
            };

            // Act
            var json = FunctionExecutionSerializer.SerializeFunctionExecutionResults(results);

            // Assert
            Assert.NotNull(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // null status doesn't match "completed" or "failed"
            Assert.Equal(0, root.GetProperty("successCount").GetInt32());
            Assert.Equal(0, root.GetProperty("failedCount").GetInt32());

            var call = root.GetProperty("functionCalls")[0];
            Assert.Equal(JsonValueKind.Null, call.GetProperty("toolCallId").ValueKind);
            Assert.Equal(JsonValueKind.Null, call.GetProperty("functionName").ValueKind);
            Assert.Equal(JsonValueKind.Null, call.GetProperty("status").ValueKind);
            Assert.Equal(JsonValueKind.Null, call.GetProperty("cost").ValueKind);
            Assert.Equal(JsonValueKind.Null, call.GetProperty("errorMessage").ValueKind);
            Assert.Equal(JsonValueKind.Null, call.GetProperty("functionExecutionId").ValueKind);
        }

        [Fact]
        public void SerializeFunctionExecutionResults_ReturnsNonIndentedJson()
        {
            // Arrange
            var results = new List<FunctionExecutionResultForLogging>
            {
                new FunctionExecutionResultForLogging
                {
                    ToolCallId = "call_1",
                    FunctionName = "test",
                    Status = "completed"
                }
            };

            // Act
            var json = FunctionExecutionSerializer.SerializeFunctionExecutionResults(results);

            // Assert
            Assert.DoesNotContain("\n", json);
            Assert.DoesNotContain("  ", json); // No indentation
        }

        [Fact]
        public void SerializeFunctionExecutionResults_WithZeroCosts_CalculatesTotalCorrectly()
        {
            // Arrange
            var results = new List<FunctionExecutionResultForLogging>
            {
                new FunctionExecutionResultForLogging { Status = "completed", Cost = 0m },
                new FunctionExecutionResultForLogging { Status = "completed", Cost = 0m },
                new FunctionExecutionResultForLogging { Status = "completed", Cost = null }
            };

            // Act
            var json = FunctionExecutionSerializer.SerializeFunctionExecutionResults(results);

            // Assert
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(0m, doc.RootElement.GetProperty("totalCost").GetDecimal());
        }

        [Fact]
        public void SerializeFunctionExecutionResults_WithLargeCost_PreservesPrecision()
        {
            // Arrange
            var results = new List<FunctionExecutionResultForLogging>
            {
                new FunctionExecutionResultForLogging
                {
                    Status = "completed",
                    Cost = 123.456789m
                }
            };

            // Act
            var json = FunctionExecutionSerializer.SerializeFunctionExecutionResults(results);

            // Assert
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(123.456789m, doc.RootElement.GetProperty("totalCost").GetDecimal());
        }

        #endregion

        #region DeserializeFunctionExecutionMetadata Tests

        [Fact]
        public void DeserializeFunctionExecutionMetadata_WithNull_ReturnsNull()
        {
            var result = FunctionExecutionSerializer.DeserializeFunctionExecutionMetadata(null);
            Assert.Null(result);
        }

        [Fact]
        public void DeserializeFunctionExecutionMetadata_WithEmptyString_ReturnsNull()
        {
            var result = FunctionExecutionSerializer.DeserializeFunctionExecutionMetadata("");
            Assert.Null(result);
        }

        [Fact]
        public void DeserializeFunctionExecutionMetadata_WithWhitespace_ReturnsNull()
        {
            var result = FunctionExecutionSerializer.DeserializeFunctionExecutionMetadata("   ");
            Assert.Null(result);
        }

        [Fact]
        public void DeserializeFunctionExecutionMetadata_WithInvalidJson_ReturnsNull()
        {
            var result = FunctionExecutionSerializer.DeserializeFunctionExecutionMetadata("{ invalid json }");
            Assert.Null(result);
        }

        [Fact]
        public void DeserializeFunctionExecutionMetadata_WithValidJson_ReturnsMetadata()
        {
            // Arrange
            var json = @"{
                ""type"": ""chat_with_functions"",
                ""functionCallCount"": 2,
                ""totalCost"": 0.015,
                ""successCount"": 1,
                ""failedCount"": 1
            }";

            // Act
            var result = FunctionExecutionSerializer.DeserializeFunctionExecutionMetadata(json);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("chat_with_functions", result.Type);
            Assert.Equal(2, result.FunctionCallCount);
            Assert.Equal(0.015m, result.TotalCost);
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(1, result.FailedCount);
        }

        #endregion

        #region Roundtrip Tests

        [Fact]
        public void SerializeAndDeserialize_RoundTrip_PreservesStructure()
        {
            // Arrange
            var executionId = Guid.NewGuid();
            var results = new List<FunctionExecutionResultForLogging>
            {
                new FunctionExecutionResultForLogging
                {
                    ToolCallId = "call_roundtrip",
                    FunctionName = "test_function",
                    Status = "completed",
                    Cost = 0.123m,
                    FunctionExecutionId = executionId
                }
            };

            // Act
            var json = FunctionExecutionSerializer.SerializeFunctionExecutionResults(results);
            var deserialized = FunctionExecutionSerializer.DeserializeFunctionExecutionMetadata(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("chat_with_functions", deserialized.Type);
            Assert.Equal(1, deserialized.FunctionCallCount);
            Assert.Equal(0.123m, deserialized.TotalCost);
            Assert.Equal(1, deserialized.SuccessCount);
            Assert.Equal(0, deserialized.FailedCount);
        }

        #endregion
    }
}
