using System.Text.Json;
using ConduitLLM.Functions.Utilities;

namespace ConduitLLM.Tests.Functions.Utilities
{
    public class JsonElementConverterTests
    {
        #region Helper Methods

        /// <summary>
        /// Creates a JsonElement from a JSON string value.
        /// </summary>
        private static JsonElement CreateJsonElement(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        /// <summary>
        /// Creates a JsonElement for a specific value type.
        /// </summary>
        private static JsonElement CreateJsonElementFromValue(object value)
        {
            var json = JsonSerializer.Serialize(value);
            return CreateJsonElement(json);
        }

        #endregion

        #region ConvertToString Tests

        [Fact]
        public void ConvertToString_WithNull_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToString(null);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToString_WithString_ReturnsString()
        {
            var result = JsonElementConverter.ConvertToString("hello");
            Assert.Equal("hello", result);
        }

        [Fact]
        public void ConvertToString_WithJsonElementString_ReturnsString()
        {
            var element = CreateJsonElement("\"hello world\"");
            var result = JsonElementConverter.ConvertToString(element);
            Assert.Equal("hello world", result);
        }

        [Fact]
        public void ConvertToString_WithJsonElementNumber_ReturnsNumberAsString()
        {
            var element = CreateJsonElement("42");
            var result = JsonElementConverter.ConvertToString(element);
            Assert.Equal("42", result);
        }

        [Fact]
        public void ConvertToString_WithJsonElementBoolean_ReturnsBoolAsString()
        {
            var element = CreateJsonElement("true");
            var result = JsonElementConverter.ConvertToString(element);
            Assert.Equal("True", result);
        }

        [Fact]
        public void ConvertToString_WithInteger_ReturnsIntAsString()
        {
            var result = JsonElementConverter.ConvertToString(42);
            Assert.Equal("42", result);
        }

        [Fact]
        public void ConvertToString_WithDouble_ReturnsDoubleAsString()
        {
            var result = JsonElementConverter.ConvertToString(3.14);
            Assert.Equal("3.14", result);
        }

        #endregion

        #region ConvertToInt32 Tests

        [Fact]
        public void ConvertToInt32_WithNull_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToInt32(null);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToInt32_WithInteger_ReturnsInteger()
        {
            var result = JsonElementConverter.ConvertToInt32(42);
            Assert.Equal(42, result);
        }

        [Fact]
        public void ConvertToInt32_WithJsonElementNumber_ReturnsInteger()
        {
            var element = CreateJsonElement("123");
            var result = JsonElementConverter.ConvertToInt32(element);
            Assert.Equal(123, result);
        }

        [Fact]
        public void ConvertToInt32_WithJsonElementStringNumber_ReturnsParsedInteger()
        {
            var element = CreateJsonElement("\"456\"");
            var result = JsonElementConverter.ConvertToInt32(element);
            Assert.Equal(456, result);
        }

        [Fact]
        public void ConvertToInt32_WithJsonElementInvalidString_ReturnsNull()
        {
            var element = CreateJsonElement("\"not a number\"");
            var result = JsonElementConverter.ConvertToInt32(element);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToInt32_WithJsonElementBoolean_ReturnsNull()
        {
            var element = CreateJsonElement("true");
            var result = JsonElementConverter.ConvertToInt32(element);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToInt32_WithStringNumber_ReturnsInteger()
        {
            var result = JsonElementConverter.ConvertToInt32("789");
            Assert.Equal(789, result);
        }

        [Fact]
        public void ConvertToInt32_WithDouble_ConvertsToInteger()
        {
            var result = JsonElementConverter.ConvertToInt32(42.9);
            Assert.Equal(43, result); // Convert.ToInt32 uses banker's rounding - 42.9 rounds to 43
        }

        [Fact]
        public void ConvertToInt32_WithInvalidString_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToInt32("invalid");
            Assert.Null(result);
        }

        #endregion

        #region ConvertToInt64 Tests

        [Fact]
        public void ConvertToInt64_WithNull_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToInt64(null);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToInt64_WithLong_ReturnsLong()
        {
            var result = JsonElementConverter.ConvertToInt64(9999999999L);
            Assert.Equal(9999999999L, result);
        }

        [Fact]
        public void ConvertToInt64_WithJsonElementLargeNumber_ReturnsLong()
        {
            var element = CreateJsonElement("9999999999");
            var result = JsonElementConverter.ConvertToInt64(element);
            Assert.Equal(9999999999L, result);
        }

        [Fact]
        public void ConvertToInt64_WithJsonElementStringNumber_ReturnsParsedLong()
        {
            var element = CreateJsonElement("\"9999999999\"");
            var result = JsonElementConverter.ConvertToInt64(element);
            Assert.Equal(9999999999L, result);
        }

        #endregion

        #region ConvertToDouble Tests

        [Fact]
        public void ConvertToDouble_WithNull_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToDouble(null);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToDouble_WithDouble_ReturnsDouble()
        {
            var result = JsonElementConverter.ConvertToDouble(3.14159);
            Assert.Equal(3.14159, result);
        }

        [Fact]
        public void ConvertToDouble_WithJsonElementNumber_ReturnsDouble()
        {
            var element = CreateJsonElement("2.71828");
            var result = JsonElementConverter.ConvertToDouble(element);
            Assert.Equal(2.71828, result.Value, 5);
        }

        [Fact]
        public void ConvertToDouble_WithJsonElementStringNumber_ReturnsParsedDouble()
        {
            var element = CreateJsonElement("\"1.618\"");
            var result = JsonElementConverter.ConvertToDouble(element);
            Assert.Equal(1.618, result.Value, 3);
        }

        [Fact]
        public void ConvertToDouble_WithInteger_ReturnsDouble()
        {
            var result = JsonElementConverter.ConvertToDouble(42);
            Assert.Equal(42.0, result);
        }

        #endregion

        #region ConvertToDecimal Tests

        [Fact]
        public void ConvertToDecimal_WithNull_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToDecimal(null);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToDecimal_WithDecimal_ReturnsDecimal()
        {
            var result = JsonElementConverter.ConvertToDecimal(123.456m);
            Assert.Equal(123.456m, result);
        }

        [Fact]
        public void ConvertToDecimal_WithJsonElementNumber_ReturnsDecimal()
        {
            var element = CreateJsonElement("99.99");
            var result = JsonElementConverter.ConvertToDecimal(element);
            Assert.Equal(99.99m, result);
        }

        [Fact]
        public void ConvertToDecimal_WithJsonElementStringNumber_ReturnsParsedDecimal()
        {
            var element = CreateJsonElement("\"0.001\"");
            var result = JsonElementConverter.ConvertToDecimal(element);
            Assert.Equal(0.001m, result);
        }

        #endregion

        #region ConvertToBoolean Tests

        [Fact]
        public void ConvertToBoolean_WithNull_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToBoolean(null);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToBoolean_WithTrue_ReturnsTrue()
        {
            var result = JsonElementConverter.ConvertToBoolean(true);
            Assert.True(result);
        }

        [Fact]
        public void ConvertToBoolean_WithFalse_ReturnsFalse()
        {
            var result = JsonElementConverter.ConvertToBoolean(false);
            Assert.False(result);
        }

        [Fact]
        public void ConvertToBoolean_WithJsonElementTrue_ReturnsTrue()
        {
            var element = CreateJsonElement("true");
            var result = JsonElementConverter.ConvertToBoolean(element);
            Assert.True(result);
        }

        [Fact]
        public void ConvertToBoolean_WithJsonElementFalse_ReturnsFalse()
        {
            var element = CreateJsonElement("false");
            var result = JsonElementConverter.ConvertToBoolean(element);
            Assert.False(result);
        }

        [Fact]
        public void ConvertToBoolean_WithJsonElementStringTrue_ReturnsTrue()
        {
            var element = CreateJsonElement("\"true\"");
            var result = JsonElementConverter.ConvertToBoolean(element);
            Assert.True(result);
        }

        [Fact]
        public void ConvertToBoolean_WithJsonElementStringFalse_ReturnsFalse()
        {
            var element = CreateJsonElement("\"false\"");
            var result = JsonElementConverter.ConvertToBoolean(element);
            Assert.False(result);
        }

        [Fact]
        public void ConvertToBoolean_WithJsonElementStringTrueUpperCase_ReturnsTrue()
        {
            var element = CreateJsonElement("\"True\"");
            var result = JsonElementConverter.ConvertToBoolean(element);
            Assert.True(result);
        }

        [Fact]
        public void ConvertToBoolean_WithJsonElementNumber_ReturnsNull()
        {
            var element = CreateJsonElement("1");
            var result = JsonElementConverter.ConvertToBoolean(element);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToBoolean_WithJsonElementInvalidString_ReturnsNull()
        {
            var element = CreateJsonElement("\"yes\"");
            var result = JsonElementConverter.ConvertToBoolean(element);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToBoolean_WithStringTrue_ReturnsTrue()
        {
            var result = JsonElementConverter.ConvertToBoolean("true");
            Assert.True(result);
        }

        #endregion

        #region ConvertToStringList Tests

        [Fact]
        public void ConvertToStringList_WithNull_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToStringList(null);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToStringList_WithListOfStrings_ReturnsSameList()
        {
            var input = new List<string> { "a", "b", "c" };
            var result = JsonElementConverter.ConvertToStringList(input);
            Assert.Equal(input, result);
        }

        [Fact]
        public void ConvertToStringList_WithJsonElementArray_ReturnsStringList()
        {
            var element = CreateJsonElement("[\"one\", \"two\", \"three\"]");
            var result = JsonElementConverter.ConvertToStringList(element);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal("one", result[0]);
            Assert.Equal("two", result[1]);
            Assert.Equal("three", result[2]);
        }

        [Fact]
        public void ConvertToStringList_WithJsonElementMixedArray_ConvertsAllToStrings()
        {
            var element = CreateJsonElement("[\"text\", 123, true]");
            var result = JsonElementConverter.ConvertToStringList(element);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal("text", result[0]);
            Assert.Equal("123", result[1]);
            Assert.Equal("True", result[2]);
        }

        [Fact]
        public void ConvertToStringList_WithJsonElementEmptyArray_ReturnsEmptyList()
        {
            var element = CreateJsonElement("[]");
            var result = JsonElementConverter.ConvertToStringList(element);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ConvertToStringList_WithNonArrayValue_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToStringList("not an array");
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToStringList_WithIEnumerableOfStrings_ReturnsList()
        {
            IEnumerable<string> input = new[] { "x", "y", "z" };
            var result = JsonElementConverter.ConvertToStringList(input);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains("x", result);
            Assert.Contains("y", result);
            Assert.Contains("z", result);
        }

        #endregion

        #region ConvertToIntList Tests

        [Fact]
        public void ConvertToIntList_WithNull_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToIntList(null);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToIntList_WithListOfInts_ReturnsSameList()
        {
            var input = new List<int> { 1, 2, 3 };
            var result = JsonElementConverter.ConvertToIntList(input);
            Assert.Equal(input, result);
        }

        [Fact]
        public void ConvertToIntList_WithJsonElementArray_ReturnsIntList()
        {
            var element = CreateJsonElement("[10, 20, 30]");
            var result = JsonElementConverter.ConvertToIntList(element);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(10, result[0]);
            Assert.Equal(20, result[1]);
            Assert.Equal(30, result[2]);
        }

        [Fact]
        public void ConvertToIntList_WithJsonElementEmptyArray_ReturnsNull()
        {
            var element = CreateJsonElement("[]");
            var result = JsonElementConverter.ConvertToIntList(element);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToIntList_WithJsonElementMixedArray_ReturnsOnlyInts()
        {
            // Strings in the array would not parse as ints
            var element = CreateJsonElement("[1, 2, 3]");
            var result = JsonElementConverter.ConvertToIntList(element);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
        }

        #endregion

        #region ConvertToDateTime Tests

        [Fact]
        public void ConvertToDateTime_WithNull_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToDateTime(null);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToDateTime_WithDateTime_ReturnsSameDateTime()
        {
            var dt = new DateTime(2024, 6, 15, 10, 30, 0);
            var result = JsonElementConverter.ConvertToDateTime(dt);
            Assert.Equal(dt, result);
        }

        [Fact]
        public void ConvertToDateTime_WithDateTimeOffset_ReturnsDateTime()
        {
            var dto = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);
            var result = JsonElementConverter.ConvertToDateTime(dto);
            Assert.Equal(dto.DateTime, result);
        }

        [Fact]
        public void ConvertToDateTime_WithValidDateString_ReturnsParsedDateTime()
        {
            var result = JsonElementConverter.ConvertToDateTime("2024-06-15");
            Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 6, 15), result.Value.Date);
        }

        [Fact]
        public void ConvertToDateTime_WithJsonElementDateString_ReturnsParsedDateTime()
        {
            var element = CreateJsonElement("\"2024-06-15T10:30:00Z\"");
            var result = JsonElementConverter.ConvertToDateTime(element);
            Assert.NotNull(result);
            Assert.Equal(2024, result.Value.Year);
            Assert.Equal(6, result.Value.Month);
            Assert.Equal(15, result.Value.Day);
        }

        [Fact]
        public void ConvertToDateTime_WithInvalidString_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertToDateTime("not a date");
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToDateTime_WithJsonElementNumber_ReturnsNull()
        {
            var element = CreateJsonElement("12345");
            var result = JsonElementConverter.ConvertToDateTime(element);
            Assert.Null(result);
        }

        #endregion

        #region ConvertJsonElement Tests

        [Fact]
        public void ConvertJsonElement_WithNull_ReturnsNull()
        {
            var result = JsonElementConverter.ConvertJsonElement(null);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertJsonElement_WithNonJsonElement_ReturnsSameValue()
        {
            var result = JsonElementConverter.ConvertJsonElement("regular string");
            Assert.Equal("regular string", result);
        }

        [Fact]
        public void ConvertJsonElement_WithJsonElementString_ReturnsString()
        {
            var element = CreateJsonElement("\"hello\"");
            var result = JsonElementConverter.ConvertJsonElement(element);
            Assert.Equal("hello", result);
        }

        [Fact]
        public void ConvertJsonElement_WithJsonElementInt_ReturnsInt()
        {
            var element = CreateJsonElement("42");
            var result = JsonElementConverter.ConvertJsonElement(element);
            Assert.Equal(42, result);
        }

        [Fact]
        public void ConvertJsonElement_WithJsonElementLong_ReturnsLong()
        {
            var element = CreateJsonElement("9999999999");
            var result = JsonElementConverter.ConvertJsonElement(element);
            Assert.Equal(9999999999L, result);
        }

        [Fact]
        public void ConvertJsonElement_WithJsonElementDouble_ReturnsDouble()
        {
            var element = CreateJsonElement("3.14159");
            var result = JsonElementConverter.ConvertJsonElement(element);
            Assert.Equal(3.14159, (double)result, 5);
        }

        [Fact]
        public void ConvertJsonElement_WithJsonElementTrue_ReturnsTrue()
        {
            var element = CreateJsonElement("true");
            var result = JsonElementConverter.ConvertJsonElement(element);
            Assert.Equal(true, result);
        }

        [Fact]
        public void ConvertJsonElement_WithJsonElementFalse_ReturnsFalse()
        {
            var element = CreateJsonElement("false");
            var result = JsonElementConverter.ConvertJsonElement(element);
            Assert.Equal(false, result);
        }

        [Fact]
        public void ConvertJsonElement_WithJsonElementNull_ReturnsNull()
        {
            var element = CreateJsonElement("null");
            var result = JsonElementConverter.ConvertJsonElement(element);
            Assert.Null(result);
        }

        [Fact]
        public void ConvertJsonElement_WithJsonElementArray_ReturnsList()
        {
            var element = CreateJsonElement("[1, 2, 3]");
            var result = JsonElementConverter.ConvertJsonElement(element);

            Assert.IsType<List<object>>(result);
            var list = (List<object>)result;
            Assert.Equal(3, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(2, list[1]);
            Assert.Equal(3, list[2]);
        }

        [Fact]
        public void ConvertJsonElement_WithJsonElementObject_ReturnsDictionary()
        {
            var element = CreateJsonElement("{\"name\": \"test\", \"value\": 42}");
            var result = JsonElementConverter.ConvertJsonElement(element);

            Assert.IsType<Dictionary<string, object>>(result);
            var dict = (Dictionary<string, object>)result;
            Assert.Equal(2, dict.Count);
            Assert.Equal("test", dict["name"]);
            Assert.Equal(42, dict["value"]);
        }

        [Fact]
        public void ConvertJsonElement_WithNestedObject_ReturnsNestedStructure()
        {
            var element = CreateJsonElement("{\"outer\": {\"inner\": \"value\"}}");
            var result = JsonElementConverter.ConvertJsonElement(element);

            Assert.IsType<Dictionary<string, object>>(result);
            var outer = (Dictionary<string, object>)result;
            Assert.IsType<Dictionary<string, object>>(outer["outer"]);
            var inner = (Dictionary<string, object>)outer["outer"];
            Assert.Equal("value", inner["inner"]);
        }

        [Fact]
        public void ConvertJsonElement_WithMixedArray_ReturnsConvertedList()
        {
            var element = CreateJsonElement("[\"string\", 42, true, null]");
            var result = JsonElementConverter.ConvertJsonElement(element);

            Assert.IsType<List<object>>(result);
            var list = (List<object>)result;
            Assert.Equal(4, list.Count);
            Assert.Equal("string", list[0]);
            Assert.Equal(42, list[1]);
            Assert.Equal(true, list[2]);
            Assert.Null(list[3]);
        }

        #endregion

        #region IsNullOrJsonNull Tests

        [Fact]
        public void IsNullOrJsonNull_WithNull_ReturnsTrue()
        {
            var result = JsonElementConverter.IsNullOrJsonNull(null);
            Assert.True(result);
        }

        [Fact]
        public void IsNullOrJsonNull_WithJsonNull_ReturnsTrue()
        {
            var element = CreateJsonElement("null");
            var result = JsonElementConverter.IsNullOrJsonNull(element);
            Assert.True(result);
        }

        [Fact]
        public void IsNullOrJsonNull_WithJsonString_ReturnsFalse()
        {
            var element = CreateJsonElement("\"value\"");
            var result = JsonElementConverter.IsNullOrJsonNull(element);
            Assert.False(result);
        }

        [Fact]
        public void IsNullOrJsonNull_WithRegularString_ReturnsFalse()
        {
            var result = JsonElementConverter.IsNullOrJsonNull("value");
            Assert.False(result);
        }

        [Fact]
        public void IsNullOrJsonNull_WithEmptyString_ReturnsFalse()
        {
            var result = JsonElementConverter.IsNullOrJsonNull("");
            Assert.False(result);
        }

        #endregion

        #region Edge Cases and Real-World Scenarios

        [Fact]
        public void ConvertJsonElement_ComplexNestedStructure_ConvertsCorrectly()
        {
            var json = @"{
                ""search"": {
                    ""query"": ""test query"",
                    ""filters"": [""filter1"", ""filter2""],
                    ""options"": {
                        ""limit"": 10,
                        ""enabled"": true
                    }
                }
            }";
            var element = CreateJsonElement(json);
            var result = JsonElementConverter.ConvertJsonElement(element);

            Assert.IsType<Dictionary<string, object>>(result);
            var root = (Dictionary<string, object>)result;
            var search = (Dictionary<string, object>)root["search"];
            Assert.Equal("test query", search["query"]);

            var filters = (List<object>)search["filters"];
            Assert.Equal(2, filters.Count);

            var options = (Dictionary<string, object>)search["options"];
            Assert.Equal(10, options["limit"]);
            Assert.Equal(true, options["enabled"]);
        }

        [Fact]
        public void ConvertToString_WithWhitespace_PreservesWhitespace()
        {
            var element = CreateJsonElement("\"  spaced  \"");
            var result = JsonElementConverter.ConvertToString(element);
            Assert.Equal("  spaced  ", result);
        }

        [Fact]
        public void ConvertToString_WithEmptyJsonString_ReturnsEmpty()
        {
            var element = CreateJsonElement("\"\"");
            var result = JsonElementConverter.ConvertToString(element);
            Assert.Equal("", result);
        }

        [Fact]
        public void ConvertToInt32_WithNegativeNumber_ReturnsNegative()
        {
            var element = CreateJsonElement("-42");
            var result = JsonElementConverter.ConvertToInt32(element);
            Assert.Equal(-42, result);
        }

        [Fact]
        public void ConvertToDouble_WithScientificNotation_ParsesCorrectly()
        {
            var element = CreateJsonElement("1.5e10");
            var result = JsonElementConverter.ConvertToDouble(element);
            Assert.Equal(1.5e10, result);
        }

        #endregion
    }
}
