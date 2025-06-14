using System;
using Xunit;
using ConduitLLM.Core.Extensions;

namespace ConduitLLM.Tests.Core.Extensions
{
    public class LoggingSanitizerTests
    {
        [Theory]
        [InlineData("normal input", "normal input")]
        [InlineData("input\r\nwith\nnewlines", "input  with newlines")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void S_String_RemovesDangerousCharacters(string? input, string? expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void S_String_TruncatesLongInput()
        {
            // Arrange
            var longInput = new string('a', 1500);
            
            // Act
            var result = LoggingSanitizer.S(longInput);

            // Assert
            Assert.Equal(1000, result.Length);
            Assert.Equal(new string('a', 1000), result);
        }

        [Fact]
        public void S_Object_HandlesNull()
        {
            // Act
            var result = LoggingSanitizer.S((object)null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void S_Object_SanitizesStringRepresentation()
        {
            // Arrange
            var obj = new TestObject { Value = "test\r\nvalue" };
            
            // Act
            var result = LoggingSanitizer.S(obj);

            // Assert
            Assert.Equal("TestObject: test  value", result);
        }

        [Theory]
        [InlineData(123, 123)]
        [InlineData(-456, -456)]
        [InlineData(0, 0)]
        public void S_Int_PassesThrough(int input, int expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void S_PreventsLogInjection()
        {
            // Arrange - Attempted log injection
            var maliciousInput = "admin\r\n[ERROR] Fake error message\r\n[INFO] Another fake message";
            
            // Act
            var sanitized = LoggingSanitizer.S(maliciousInput);

            // Assert - Newlines removed, preventing injection
            Assert.DoesNotContain("\r", sanitized);
            Assert.DoesNotContain("\n", sanitized);
            Assert.Equal("admin  [ERROR] Fake error message  [INFO] Another fake message", sanitized);
        }

        private class TestObject
        {
            public string Value { get; set; } = "";
            public override string ToString() => $"TestObject: {Value}";
        }
    }
}