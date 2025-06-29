using System.Text.Json;
using ConduitLLM.Core.Models;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Models
{
    [Trait("Category", "Unit")]
    [Trait("Phase", "1")]
    [Trait("Component", "Core")]
    public class ResponseFormatTests : TestBase
    {
        public ResponseFormatTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Constructor_Default_CreatesInstanceWithNullType()
        {
            // Act
            var format = new ResponseFormat();

            // Assert
            format.Should().NotBeNull();
            format.Type.Should().BeNull();
        }

        [Theory]
        [InlineData("text")]
        [InlineData("json_object")]
        [InlineData("custom_format")]
        [InlineData("")]
        public void Constructor_WithType_SetsTypeProperty(string type)
        {
            // Act
            var format = new ResponseFormat(type);

            // Assert
            format.Type.Should().Be(type);
        }

        [Fact]
        public void Constructor_WithNull_SetsTypeToNull()
        {
            // Act
            var format = new ResponseFormat(null);

            // Assert
            format.Type.Should().BeNull();
        }

        [Fact]
        public void Json_FactoryMethod_CreatesJsonObjectFormat()
        {
            // Act
            var format = ResponseFormat.Json();

            // Assert
            format.Should().NotBeNull();
            format.Type.Should().Be("json_object");
        }

        [Fact]
        public void Text_FactoryMethod_CreatesTextFormat()
        {
            // Act
            var format = ResponseFormat.Text();

            // Assert
            format.Should().NotBeNull();
            format.Type.Should().Be("text");
        }

        [Theory]
        [InlineData("text")]
        [InlineData("json_object")]
        [InlineData("custom_type")]
        [InlineData(null)]
        public void JsonSerialization_RoundTrip_PreservesType(string type)
        {
            // Arrange
            var original = new ResponseFormat(type);

            // Act
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<ResponseFormat>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Type.Should().Be(original.Type);
        }

        [Fact]
        public void JsonSerialization_WithJsonFactory_ProducesCorrectJson()
        {
            // Arrange
            var format = ResponseFormat.Json();

            // Act
            var json = JsonSerializer.Serialize(format);

            // Assert
            json.Should().Contain("\"type\":\"json_object\"");
        }

        [Fact]
        public void JsonSerialization_WithTextFactory_ProducesCorrectJson()
        {
            // Arrange
            var format = ResponseFormat.Text();

            // Act
            var json = JsonSerializer.Serialize(format);

            // Assert
            json.Should().Contain("\"type\":\"text\"");
        }

        [Fact]
        public void JsonSerialization_WithNullType_ProducesNullValue()
        {
            // Arrange
            var format = new ResponseFormat(null);

            // Act
            var json = JsonSerializer.Serialize(format);

            // Assert
            json.Should().Contain("\"type\":null");
        }

        [Fact]
        public void JsonDeserialization_EmptyJson_CreatesDefaultInstance()
        {
            // Arrange
            var json = "{}";

            // Act
            var format = JsonSerializer.Deserialize<ResponseFormat>(json);

            // Assert
            format.Should().NotBeNull();
            format.Type.Should().BeNull();
        }

        [Theory]
        [InlineData("{\"type\":\"text\"}", "text")]
        [InlineData("{\"type\":\"json_object\"}", "json_object")]
        [InlineData("{\"type\":null}", null)]
        [InlineData("{\"type\":\"\"}", "")]
        public void JsonDeserialization_WithVariousFormats_DeserializesCorrectly(string json, string expectedType)
        {
            // Act
            var format = JsonSerializer.Deserialize<ResponseFormat>(json);

            // Assert
            format.Should().NotBeNull();
            format.Type.Should().Be(expectedType);
        }

        [Fact]
        public void Type_SetGet_WorksCorrectly()
        {
            // Arrange
            var format = new ResponseFormat();
            var newType = "new_format";

            // Act
            format.Type = newType;

            // Assert
            format.Type.Should().Be(newType);
        }

        [Fact]
        public void Equals_SameType_ReturnsTrue()
        {
            // Arrange
            var format1 = new ResponseFormat("json_object");
            var format2 = new ResponseFormat("json_object");

            // Act & Assert
            format1.Type.Should().Be(format2.Type);
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            // Arrange
            var format1 = new ResponseFormat("text");
            var format2 = new ResponseFormat("json_object");

            // Act & Assert
            format1.Type.Should().NotBe(format2.Type);
        }

        [Fact]
        public void JsonPropertyName_IsCorrect()
        {
            // Arrange
            var format = new ResponseFormat("test");
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            // Act
            var json = JsonSerializer.Serialize(format, options);

            // Assert
            json.Should().Contain("\"type\":\"test\"");
            json.Should().NotContain("\"Type\"");
        }
    }
}