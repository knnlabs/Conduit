using System.Text.Json;
using ConduitLLM.Core.Converters;
using AwesomeAssertions;
using Xunit;

namespace ConduitLLM.Tests.Core.Converters;

public class UtcDateTimeConverterTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public UtcDateTimeConverterTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            Converters = { new UtcDateTimeConverter(), new NullableUtcDateTimeConverter() }
        };
    }

    [Fact]
    public void Serialize_UtcDateTime_ShouldIncludeZSuffix()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var testObject = new { CreatedAt = dateTime };

        // Act
        var json = JsonSerializer.Serialize(testObject, _jsonOptions);

        // Assert
        json.Should().Contain("2024-01-15T10:30:00Z");
    }

    [Fact]
    public void Serialize_LocalDateTime_ShouldConvertToUtcWithZSuffix()
    {
        // Arrange
        var localTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Local);
        var expectedUtc = localTime.ToUniversalTime();
        var testObject = new { CreatedAt = localTime };

        // Act
        var json = JsonSerializer.Serialize(testObject, _jsonOptions);
        var result = JsonSerializer.Deserialize<TestDto>(json, _jsonOptions);

        // Assert
        json.Should().Contain("Z"); // Should have UTC marker
        result.Should().NotBeNull();
        result!.CreatedAt.Should().Be(expectedUtc);
        result.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Serialize_UnspecifiedDateTime_ShouldTreatAsUtcWithZSuffix()
    {
        // Arrange
        var unspecifiedTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Unspecified);
        var testObject = new { CreatedAt = unspecifiedTime };

        // Act
        var json = JsonSerializer.Serialize(testObject, _jsonOptions);

        // Assert
        json.Should().Contain("2024-01-15T10:30:00Z");
    }

    [Fact]
    public void Deserialize_DateTimeWithZ_ShouldParseAsUtc()
    {
        // Arrange
        var json = """{"CreatedAt":"2024-01-15T10:30:00Z"}""";

        // Act
        var result = JsonSerializer.Deserialize<TestDto>(json, _jsonOptions);

        // Assert
        result.Should().NotBeNull();
        result!.CreatedAt.Year.Should().Be(2024);
        result.CreatedAt.Month.Should().Be(1);
        result.CreatedAt.Day.Should().Be(15);
        result.CreatedAt.Hour.Should().Be(10);
        result.CreatedAt.Minute.Should().Be(30);
        result.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Deserialize_DateTimeWithoutZ_ShouldTreatAsUtc()
    {
        // Arrange
        var json = """{"CreatedAt":"2024-01-15T10:30:00"}""";

        // Act
        var result = JsonSerializer.Deserialize<TestDto>(json, _jsonOptions);

        // Assert
        result.Should().NotBeNull();
        result!.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Deserialize_DateTimeWithMilliseconds_ShouldPreserveMilliseconds()
    {
        // Arrange
        var json = """{"CreatedAt":"2024-01-15T10:30:00.123Z"}""";

        // Act
        var result = JsonSerializer.Deserialize<TestDto>(json, _jsonOptions);

        // Assert
        result.Should().NotBeNull();
        result!.CreatedAt.Millisecond.Should().Be(123);
        result.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void RoundTrip_DateTime_ShouldPreserveValue()
    {
        // Arrange
        var original = new TestDto
        {
            CreatedAt = new DateTime(2024, 1, 15, 10, 30, 45, 123, DateTimeKind.Utc)
        };

        // Act
        var json = JsonSerializer.Serialize(original, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<TestDto>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CreatedAt.Should().Be(original.CreatedAt);
        deserialized.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Serialize_NullableDateTime_Null_ShouldSerializeAsNull()
    {
        // Arrange
        var testObject = new { ExpiresAt = (DateTime?)null };

        // Act
        var json = JsonSerializer.Serialize(testObject, _jsonOptions);

        // Assert
        json.Should().Contain("\"ExpiresAt\":null");
    }

    [Fact]
    public void Serialize_NullableDateTime_WithValue_ShouldIncludeZSuffix()
    {
        // Arrange
        var testObject = new { ExpiresAt = (DateTime?)new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc) };

        // Act
        var json = JsonSerializer.Serialize(testObject, _jsonOptions);

        // Assert
        json.Should().Contain("2024-01-15T10:30:00Z");
    }

    [Fact]
    public void Deserialize_NullableDateTime_Null_ShouldReturnNull()
    {
        // Arrange
        var json = """{"ExpiresAt":null}""";

        // Act
        var result = JsonSerializer.Deserialize<TestDtoWithNullable>(json, _jsonOptions);

        // Assert
        result.Should().NotBeNull();
        result!.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Deserialize_NullableDateTime_WithValue_ShouldParseAsUtc()
    {
        // Arrange
        var json = """{"ExpiresAt":"2024-01-15T10:30:00Z"}""";

        // Act
        var result = JsonSerializer.Deserialize<TestDtoWithNullable>(json, _jsonOptions);

        // Assert
        result.Should().NotBeNull();
        result!.ExpiresAt.Should().NotBeNull();
        result.ExpiresAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void IntegrationWithCamelCase_ShouldRespectNamingPolicy()
    {
        // Arrange
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new UtcDateTimeConverter() }
        };
        var testObject = new TestDto
        {
            CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        // Act
        var json = JsonSerializer.Serialize(testObject, options);

        // Assert
        json.Should().Contain("\"createdAt\""); // camelCase property name
        json.Should().Contain("2024-01-15T10:30:00Z"); // UTC date format
    }

    [Fact]
    public void IntegrationWithSnakeCase_ShouldRespectNamingPolicy()
    {
        // Arrange
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new UtcDateTimeConverter() }
        };
        var testObject = new TestDto
        {
            CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        // Act
        var json = JsonSerializer.Serialize(testObject, options);

        // Assert
        json.Should().Contain("\"created_at\""); // snake_case property name
        json.Should().Contain("2024-01-15T10:30:00Z"); // UTC date format
    }

    [Fact]
    public void Deserialize_EmptyString_ShouldThrowJsonException()
    {
        // Arrange
        var json = """{"CreatedAt":""}""";

        // Act & Assert
        var act = () => JsonSerializer.Deserialize<TestDto>(json, _jsonOptions);
        act.Should().Throw<JsonException>()
            .WithMessage("*Cannot parse empty string as DateTime*");
    }

    [Fact]
    public void Deserialize_InvalidFormat_ShouldThrowJsonException()
    {
        // Arrange
        var json = """{"CreatedAt":"not-a-date"}""";

        // Act & Assert
        var act = () => JsonSerializer.Deserialize<TestDto>(json, _jsonOptions);
        act.Should().Throw<JsonException>()
            .WithMessage("*Unable to parse*");
    }

    [Fact]
    public void Serialize_DateTimeMinValue_ShouldSerializeCorrectly()
    {
        // Arrange
        var testObject = new TestDto { CreatedAt = DateTime.MinValue };

        // Act
        var json = JsonSerializer.Serialize(testObject, _jsonOptions);
        var result = JsonSerializer.Deserialize<TestDto>(json, _jsonOptions);

        // Assert
        result.Should().NotBeNull();
        result!.CreatedAt.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void Serialize_DateTimeMaxValue_ShouldSerializeCorrectly()
    {
        // Arrange
        var testObject = new TestDto { CreatedAt = DateTime.MaxValue };

        // Act
        var json = JsonSerializer.Serialize(testObject, _jsonOptions);
        var result = JsonSerializer.Deserialize<TestDto>(json, _jsonOptions);

        // Assert
        result.Should().NotBeNull();
        result!.CreatedAt.Should().Be(DateTime.MaxValue);
    }

    // Test DTOs
    private class TestDto
    {
        public DateTime CreatedAt { get; set; }
    }

    private class TestDtoWithNullable
    {
        public DateTime? ExpiresAt { get; set; }
    }
}
