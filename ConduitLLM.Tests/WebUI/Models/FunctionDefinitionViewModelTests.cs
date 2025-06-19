using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using ConduitLLM.WebUI.Models;
using Xunit;

namespace ConduitLLM.Tests.WebUI.Models
{
    /// <summary>
    /// Tests for FunctionDefinitionViewModel and related models.
    /// </summary>
    public class FunctionDefinitionViewModelTests
    {
        [Fact]
        public void FunctionDefinitionViewModel_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var model = new FunctionDefinitionViewModel();

            // Assert
            Assert.NotNull(model.Id);
            Assert.NotEmpty(model.Id);
            Assert.Equal(string.Empty, model.Name);
            Assert.Equal(string.Empty, model.Description);
            Assert.Equal("{}", model.ParametersJson);
            Assert.True(model.IsEnabled);
            Assert.Equal("custom", model.Category);
        }

        [Fact]
        public void GetParametersAsJson_WithValidJson_ReturnsJsonObject()
        {
            // Arrange
            var model = new FunctionDefinitionViewModel
            {
                ParametersJson = @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""location"": {
                            ""type"": ""string"",
                            ""description"": ""The city""
                        }
                    },
                    ""required"": [""location""]
                }"
            };

            // Act
            var result = model.GetParametersAsJson();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("object", result["type"]?.GetValue<string>());
            Assert.NotNull(result["properties"]);
            Assert.NotNull(result["required"]);
        }

        [Fact]
        public void GetParametersAsJson_WithInvalidJson_ReturnsNull()
        {
            // Arrange
            var model = new FunctionDefinitionViewModel
            {
                ParametersJson = "{ invalid json"
            };

            // Act
            var result = model.GetParametersAsJson();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetParametersAsJson_WithEmptyString_ReturnsNull()
        {
            // Arrange
            var model = new FunctionDefinitionViewModel
            {
                ParametersJson = ""
            };

            // Act
            var result = model.GetParametersAsJson();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FunctionCallState_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var state = new FunctionCallState();

            // Assert
            Assert.NotNull(state.PendingCalls);
            Assert.Empty(state.PendingCalls);
            Assert.NotNull(state.Results);
            Assert.Empty(state.Results);
            Assert.False(state.IsProcessing);
        }

        [Fact]
        public void FunctionExecutionResult_CanStoreSuccessResult()
        {
            // Arrange & Act
            var result = new FunctionExecutionResult
            {
                Success = true,
                Result = "{\"temperature\": 72}",
                Error = null,
                ExecutionTime = TimeSpan.FromMilliseconds(150)
            };

            // Assert
            Assert.True(result.Success);
            Assert.Equal("{\"temperature\": 72}", result.Result);
            Assert.Null(result.Error);
            Assert.Equal(150, result.ExecutionTime.TotalMilliseconds);
        }

        [Fact]
        public void FunctionExecutionResult_CanStoreErrorResult()
        {
            // Arrange & Act
            var result = new FunctionExecutionResult
            {
                Success = false,
                Result = "{}",
                Error = "Function timeout",
                ExecutionTime = TimeSpan.FromSeconds(30)
            };

            // Assert
            Assert.False(result.Success);
            Assert.Equal("{}", result.Result);
            Assert.Equal("Function timeout", result.Error);
            Assert.Equal(30, result.ExecutionTime.TotalSeconds);
        }

        [Fact]
        public void FunctionDefinitionViewModel_WithComplexParameters_HandlesCorrectly()
        {
            // Arrange
            var complexParams = new
            {
                type = "object",
                properties = new
                {
                    query = new
                    {
                        type = "string",
                        description = "Search query"
                    },
                    filters = new
                    {
                        type = "object",
                        properties = new
                        {
                            category = new { type = "string" },
                            minPrice = new { type = "number" },
                            maxPrice = new { type = "number" }
                        }
                    },
                    limit = new
                    {
                        type = "integer",
                        minimum = 1,
                        maximum = 100,
                        @default = 10
                    }
                },
                required = new[] { "query" }
            };

            var model = new FunctionDefinitionViewModel
            {
                Name = "search_products",
                Description = "Search for products with filters",
                ParametersJson = JsonSerializer.Serialize(complexParams, new JsonSerializerOptions { WriteIndented = true })
            };

            // Act
            var jsonParams = model.GetParametersAsJson();

            // Assert
            Assert.NotNull(jsonParams);
            Assert.Equal("object", jsonParams["type"]?.GetValue<string>());
            
            var properties = jsonParams["properties"];
            Assert.NotNull(properties);
            Assert.NotNull(properties["query"]);
            Assert.NotNull(properties["filters"]);
            Assert.NotNull(properties["limit"]);
            
            var limitProp = properties["limit"];
            Assert.NotNull(limitProp);
            Assert.Equal(1, limitProp!["minimum"]?.GetValue<int>());
            Assert.Equal(100, limitProp!["maximum"]?.GetValue<int>());
            Assert.Equal(10, limitProp!["default"]?.GetValue<int>());
        }

        [Theory]
        [InlineData("get_weather", true)]
        [InlineData("search-products", true)]
        [InlineData("calculate_sum", true)]
        [InlineData("API_KEY_123", true)]
        [InlineData("a", true)] // Single character is valid
        [InlineData("get weather", false)] // Space not allowed
        [InlineData("get.weather", false)] // Dot not allowed
        [InlineData("", false)] // Empty not allowed
        [InlineData("get/weather", false)] // Slash not allowed
        [InlineData("функция", false)] // Non-ASCII not allowed
        public void ValidateFunctionName_TestCases(string name, bool expectedValid)
        {
            // Arrange
            var regex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z0-9_-]{1,64}$");

            // Act
            var isValid = regex.IsMatch(name);

            // Assert
            Assert.Equal(expectedValid, isValid);
        }

        [Fact]
        public void FunctionDefinitionViewModel_Equality_BasedOnId()
        {
            // Arrange
            var id = Guid.NewGuid().ToString();
            var model1 = new FunctionDefinitionViewModel { Id = id, Name = "func1" };
            var model2 = new FunctionDefinitionViewModel { Id = id, Name = "func2" };
            var model3 = new FunctionDefinitionViewModel { Id = Guid.NewGuid().ToString(), Name = "func1" };

            // Act & Assert
            Assert.Equal(model1.Id, model2.Id);
            Assert.NotEqual(model1.Id, model3.Id);
        }

        [Fact]
        public void FunctionCallState_CanTrackMultipleResults()
        {
            // Arrange
            var state = new FunctionCallState();
            
            // Act
            state.Results["call_123"] = new FunctionExecutionResult 
            { 
                Success = true, 
                Result = "{\"temp\": 72}" 
            };
            
            state.Results["call_456"] = new FunctionExecutionResult 
            { 
                Success = false, 
                Error = "Timeout" 
            };

            // Assert
            Assert.Equal(2, state.Results.Count);
            Assert.True(state.Results["call_123"].Success);
            Assert.False(state.Results["call_456"].Success);
            Assert.Equal("Timeout", state.Results["call_456"].Error);
        }
    }
}