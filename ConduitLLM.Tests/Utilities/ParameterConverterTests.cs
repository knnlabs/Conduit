using System;
using System.Collections.Generic;
using ConduitLLM.Providers.Utilities;
using Xunit;

namespace ConduitLLM.Tests.Utilities
{
    public class ParameterConverterTests
    {
        #region ToFloat Tests

        [Fact]
        public void ToFloat_WithNullValue_ReturnsNull()
        {
            // Act
            var result = ParameterConverter.ToFloat(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ToFloat_WithValidValue_ReturnsFloat()
        {
            // Arrange
            double input = 1.5;

            // Act
            var result = ParameterConverter.ToFloat(input);

            // Assert
            Assert.Equal(1.5f, result);
        }

        [Fact]
        public void ToFloat_WithMaxValue_ReturnsFloatMaxValue()
        {
            // Arrange
            double input = double.MaxValue;

            // Act
            var result = ParameterConverter.ToFloat(input);

            // Assert
            Assert.Equal(float.MaxValue, result);
        }

        [Fact]
        public void ToFloat_WithMinValue_ReturnsFloatMinValue()
        {
            // Arrange
            double input = double.MinValue;

            // Act
            var result = ParameterConverter.ToFloat(input);

            // Assert
            Assert.Equal(float.MinValue, result);
        }

        #endregion

        #region ConvertLogitBias Tests

        [Fact]
        public void ConvertLogitBias_WithNullValue_ReturnsNull()
        {
            // Act
            var result = ParameterConverter.ConvertLogitBias(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ConvertLogitBias_WithValidDictionary_ReturnsConvertedDictionary()
        {
            // Arrange
            var input = new Dictionary<string, int>
            {
                { "token1", 10 },
                { "token2", -5 },
                { "token3", 0 }
            };

            // Act
            var result = ParameterConverter.ConvertLogitBias(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(10f, result["token1"]);
            Assert.Equal(-5f, result["token2"]);
            Assert.Equal(0f, result["token3"]);
        }

        [Fact]
        public void ConvertLogitBias_WithEmptyDictionary_ReturnsEmptyDictionary()
        {
            // Arrange
            var input = new Dictionary<string, int>();

            // Act
            var result = ParameterConverter.ConvertLogitBias(input);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region ConvertStopSequences Tests

        [Fact]
        public void ConvertStopSequences_WithNullValue_ReturnsNull()
        {
            // Act
            var result = ParameterConverter.ConvertStopSequences(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ConvertStopSequences_WithEmptyList_ReturnsNull()
        {
            // Arrange
            var input = new List<string>();

            // Act
            var result = ParameterConverter.ConvertStopSequences(input);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ConvertStopSequences_WithSingleItem_ReturnsString()
        {
            // Arrange
            var input = new List<string> { "stop1" };

            // Act
            var result = ParameterConverter.ConvertStopSequences(input);

            // Assert
            Assert.IsType<string>(result);
            Assert.Equal("stop1", result);
        }

        [Fact]
        public void ConvertStopSequences_WithMultipleItems_ReturnsStringList()
        {
            // Arrange
            var input = new List<string> { "stop1", "stop2", "stop3" };

            // Act
            var result = ParameterConverter.ConvertStopSequences(input);

            // Assert
            Assert.IsType<List<string>>(result);
            var resultList = (List<string>)result;
            Assert.Equal(3, resultList.Count);
            Assert.Equal("stop1", resultList[0]);
            Assert.Equal("stop2", resultList[1]);
            Assert.Equal("stop3", resultList[2]);
        }

        #endregion

        #region ToTemperature Tests

        [Fact]
        public void ToTemperature_WithNullValue_ReturnsNull()
        {
            // Act
            var result = ParameterConverter.ToTemperature(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ToTemperature_WithValidValue_ReturnsFloat()
        {
            // Arrange
            double input = 1.0;

            // Act
            var result = ParameterConverter.ToTemperature(input);

            // Assert
            Assert.Equal(1.0f, result);
        }

        [Fact]
        public void ToTemperature_WithValueAboveMax_ReturnsMaxValue()
        {
            // Arrange
            double input = 3.0;

            // Act
            var result = ParameterConverter.ToTemperature(input);

            // Assert
            Assert.Equal(2.0f, result);
        }

        [Fact]
        public void ToTemperature_WithValueBelowMin_ReturnsMinValue()
        {
            // Arrange
            double input = -1.0;

            // Act
            var result = ParameterConverter.ToTemperature(input);

            // Assert
            Assert.Equal(0.0f, result);
        }

        [Fact]
        public void ToTemperature_WithBoundaryValues_ReturnsCorrectValues()
        {
            // Test lower boundary
            var result1 = ParameterConverter.ToTemperature(0.0);
            Assert.Equal(0.0f, result1);

            // Test upper boundary
            var result2 = ParameterConverter.ToTemperature(2.0);
            Assert.Equal(2.0f, result2);
        }

        #endregion

        #region ToProbability Tests

        [Fact]
        public void ToProbability_WithNullValue_ReturnsNull()
        {
            // Act
            var result = ParameterConverter.ToProbability(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ToProbability_WithValidValue_ReturnsFloat()
        {
            // Arrange
            double input = 0.5;

            // Act
            var result = ParameterConverter.ToProbability(input);

            // Assert
            Assert.Equal(0.5f, result);
        }

        [Fact]
        public void ToProbability_WithDefaultBounds_ClampsCorrectly()
        {
            // Test above upper bound
            var result1 = ParameterConverter.ToProbability(3.0);
            Assert.Equal(2.0f, result1);

            // Test below lower bound
            var result2 = ParameterConverter.ToProbability(-3.0);
            Assert.Equal(-2.0f, result2);
        }

        [Fact]
        public void ToProbability_WithCustomBounds_ClampsCorrectly()
        {
            // Test custom bounds (0.0 to 1.0 for TopP)
            var result1 = ParameterConverter.ToProbability(1.5, 0.0, 1.0);
            Assert.Equal(1.0f, result1);

            var result2 = ParameterConverter.ToProbability(-0.5, 0.0, 1.0);
            Assert.Equal(0.0f, result2);

            var result3 = ParameterConverter.ToProbability(0.7, 0.0, 1.0);
            Assert.Equal(0.7f, result3);
        }

        [Fact]
        public void ToProbability_WithBoundaryValues_ReturnsCorrectValues()
        {
            // Test default boundaries
            var result1 = ParameterConverter.ToProbability(-2.0);
            Assert.Equal(-2.0f, result1);

            var result2 = ParameterConverter.ToProbability(2.0);
            Assert.Equal(2.0f, result2);

            // Test custom boundaries
            var result3 = ParameterConverter.ToProbability(0.0, 0.0, 1.0);
            Assert.Equal(0.0f, result3);

            var result4 = ParameterConverter.ToProbability(1.0, 0.0, 1.0);
            Assert.Equal(1.0f, result4);
        }

        #endregion

        #region Edge Cases and Precision Tests

        [Fact]
        public void ToFloat_WithVerySmallValue_MaintainsPrecision()
        {
            // Arrange
            double input = 0.000001;

            // Act
            var result = ParameterConverter.ToFloat(input);

            // Assert
            Assert.Equal(0.000001f, result.Value, 7); // 7 decimal places precision
        }

        [Fact]
        public void ToTemperature_WithVeryPreciseValue_MaintainsPrecision()
        {
            // Arrange
            double input = 1.234567;

            // Act
            var result = ParameterConverter.ToTemperature(input);

            // Assert
            Assert.Equal(1.234567f, result.Value, 6); // 6 decimal places precision
        }

        [Fact]
        public void ToProbability_WithVeryPreciseValue_MaintainsPrecision()
        {
            // Arrange
            double input = 0.123456789;

            // Act
            var result = ParameterConverter.ToProbability(input, 0.0, 1.0);

            // Assert
            Assert.Equal(0.123456789f, result.Value, 8); // 8 decimal places precision
        }

        #endregion
    }
}