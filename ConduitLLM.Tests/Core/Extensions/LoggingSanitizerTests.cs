using System;
using ConduitLLM.Core.Extensions;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Extensions
{
    [Trait("Category", "Unit")]
    [Trait("Phase", "1")]
    [Trait("Component", "Core")]
    public class LoggingSanitizerTests : TestBase
    {
        public LoggingSanitizerTests(ITestOutputHelper output) : base(output) { }

        [Theory]
        [InlineData("normal text", "normal text")]
        [InlineData("text with spaces", "text with spaces")]
        [InlineData("123456789", "123456789")]
        [InlineData("text-with-dashes", "text-with-dashes")]
        [InlineData("text_with_underscores", "text_with_underscores")]
        public void S_String_WithNormalText_ReturnsUnchanged(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("line1\r\nline2", "line1  line2")]
        [InlineData("line1\nline2", "line1 line2")]
        [InlineData("line1\rline2", "line1 line2")]
        [InlineData("multiple\r\n\r\nlines", "multiple    lines")]
        public void S_String_WithCRLF_RemovesLineBreaks(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void S_String_WithControlCharacters_RemovesControlChars()
        {
            // Test null character - using char value
            var input = "text" + (char)0 + "here";
            var result = LoggingSanitizer.S(input);
            result.Should().Be("texthere");
            
            // Test tab removal
            input = "before" + (char)9 + "after";
            result = LoggingSanitizer.S(input);
            result.Should().Be("beforeafter");
            
            // Test bell character
            input = "bell" + (char)7 + "test";
            result = LoggingSanitizer.S(input);
            result.Should().Be("belltest");
        }

        [Fact]
        public void S_String_WithLongText_TruncatesToMaxLength()
        {
            // Arrange
            var longText = new string('a', 1500);
            var expectedLength = 1000;

            // Act
            var result = LoggingSanitizer.S(longText);

            // Assert
            result.Should().HaveLength(expectedLength);
            result.Should().Be(new string('a', expectedLength));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void S_String_WithNullOrEmpty_ReturnsInput(string input)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(input);
        }


        [Theory]
        [InlineData("<script>alert('XSS')</script>", "<script>alert('XSS')</script>")]
        [InlineData("javascript:alert('XSS')", "javascript:alert('XSS')")]
        [InlineData("<img src=x onerror=alert('XSS')>", "<img src=x onerror=alert('XSS')>")]
        public void S_String_WithXSSAttempts_PreservesHtmlButRemovesCRLF(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("User logged in\r\n[INFO] Fake log entry", "User logged in  [INFO] Fake log entry")]
        [InlineData("Error\n2024-01-01 00:00:00 [CRITICAL] Injected", "Error 2024-01-01 00:00:00 [CRITICAL] Injected")]
        public void S_String_WithLogInjectionAttempts_PreventsCRLFInjection(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void S_Object_WithNull_ReturnsNull()
        {
            // Act
            var result = LoggingSanitizer.S((object)null);

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData(123, 123)]
        [InlineData("test\r\nstring", "test  string")]
        public void S_Object_WithVariousTypes_SanitizesStringRepresentation(object input, object expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected.ToString());
        }

        [Fact]
        public void S_Object_WithCustomToString_SanitizesOutput()
        {
            // Arrange
            var customObject = new CustomToStringObject { Value = "line1\r\nline2" };

            // Act
            var result = LoggingSanitizer.S(customObject);

            // Assert
            result.Should().Be("CustomObject: line1  line2");
        }

        [Theory]
        [InlineData(42)]
        [InlineData(-123)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void S_Int_ReturnsUnchanged(int value)
        {
            // Act
            var result = LoggingSanitizer.S(value);

            // Assert
            result.Should().Be(value);
        }

        [Theory]
        [InlineData(42L)]
        [InlineData(-123L)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        public void S_Long_ReturnsUnchanged(long value)
        {
            // Act
            var result = LoggingSanitizer.S(value);

            // Assert
            result.Should().Be(value);
        }

        [Theory]
        [InlineData(42.5)]
        [InlineData(-123.456)]
        [InlineData(0.0)]
        public void S_Decimal_ReturnsUnchanged(double value)
        {
            // Arrange
            var decimalValue = (decimal)value;

            // Act
            var result = LoggingSanitizer.S(decimalValue);

            // Assert
            result.Should().Be(decimalValue);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void S_Bool_ReturnsUnchanged(bool value)
        {
            // Act
            var result = LoggingSanitizer.S(value);

            // Assert
            result.Should().Be(value);
        }

        [Fact]
        public void S_DateTime_ReturnsUnchanged()
        {
            // Arrange
            var dateTime = DateTime.UtcNow;

            // Act
            var result = LoggingSanitizer.S(dateTime);

            // Assert
            result.Should().Be(dateTime);
        }

        [Fact]
        public void S_Guid_ReturnsUnchanged()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act
            var result = LoggingSanitizer.S(guid);

            // Assert
            result.Should().Be(guid);
        }

        [Fact]
        public void S_String_WithCombinedAttacks_SanitizesAllPatterns()
        {
            // Arrange
            var input = "Normal text\r\n'; DROP TABLE users; --\x00\x1F" + new string('x', 1100);
            
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().StartWith("Normal text  '; DROP TABLE users; --");
            result.Should().NotContain("\r");
            result.Should().NotContain("\n");
            result.Should().NotContain("\x00");
            result.Should().NotContain("\x1F");
            result.Should().HaveLength(1000);
        }

        [Trait("Category", "Performance")]
        [Fact]
        public void S_String_Performance_CompletesQuickly()
        {
            // Arrange
            var input = new string('a', 1000) + "\r\n\x00\x1F";
            var iterations = 10000;

            // Act
            var startTime = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                LoggingSanitizer.S(input);
            }
            var duration = DateTime.UtcNow - startTime;

            // Assert
            duration.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
            Log($"Sanitized {iterations} strings in {duration.TotalMilliseconds:F2}ms");
        }

        private class CustomToStringObject
        {
            public string Value { get; set; }
            public override string ToString() => $"CustomObject: {Value}";
        }
    }
}