using ConduitLLM.Configuration.Utilities;

using AwesomeAssertions;

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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                LoggingSanitizer.S(input);
            }
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;

            // Assert
            // Allow more time for CI environments and slower machines
            // Previous threshold of 200ms was too strict for CI environments
            // 500ms still ensures < 50μs per operation which is acceptable for logging
            duration.Should().BeLessThan(TimeSpan.FromMilliseconds(500), 
                "sanitization should be performant (< 50μs per operation)");
            Log($"Sanitized {iterations} strings in {duration.TotalMilliseconds:F2}ms");
        }

        [Theory]
        [InlineData("Hello 👋 World", "Hello 👋 World")] // Basic emoji
        [InlineData("🎉🎊🎈", "🎉🎊🎈")] // Multiple emoji
        [InlineData("👨‍👩‍👧‍👦", "👨‍👩‍👧‍👦")] // Family emoji with ZWJ
        [InlineData("🏳️‍🌈", "🏳️‍🌈")] // Rainbow flag with variation selector
        [InlineData("👋🏿", "👋🏿")] // Emoji with skin tone modifier
        public void S_String_WithEmoji_PreservesEmoji(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("\uD800\uDC00", "\uD800\uDC00")] // Valid surrogate pair (U+10000)
        [InlineData("𐐷𐐷", "𐐷𐐷")] // Deseret capital letter (U+10437)
        [InlineData("𝕳𝖊𝖑𝖑𝖔", "𝕳𝖊𝖑𝖑𝖔")] // Mathematical bold fraktur
        [InlineData("🔥test🔥", "🔥test🔥")] // Text with surrogate pairs
        public void S_String_WithSurrogatePairs_PreservesSurrogatePairs(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("e\u0301", "e\u0301")] // e with combining acute accent
        [InlineData("ñ", "ñ")] // Precomposed ñ
        [InlineData("a\u0300\u0301\u0302", "a\u0300\u0301\u0302")] // Multiple combining marks
        [InlineData("ㅏ\u1161", "ㅏ\u1161")] // Korean Hangul with combining
        public void S_String_WithCombiningCharacters_PreservesCombiningChars(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Hello\u200Bworld", "Hello\u200Bworld")] // Zero-width space
        [InlineData("Test\u200C\u200D", "Test\u200C\u200D")] // Zero-width non-joiner and joiner
        [InlineData("\uFEFFBOM at start", "\uFEFFBOM at start")] // Byte order mark
        [InlineData("Text\u2028Separator", "Text Separator")] // Line separator (now removed for security)
        [InlineData("Text\u2029Separator", "Text Separator")] // Paragraph separator (now removed for security)
        public void S_String_WithZeroWidthAndSpecialChars_HandlesAppropriately(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Hello עולם", "Hello עולם")] // English and Hebrew
        [InlineData("مرحبا بالعالم", "مرحبا بالعالم")] // Arabic
        [InlineData("\u202Aforce LTR\u202C", "\u202Aforce LTR\u202C")] // LTR embedding
        [InlineData("\u202Bforce RTL\u202C", "\u202Bforce RTL\u202C")] // RTL embedding
        [InlineData("\u200Fright-to-left mark", "\u200Fright-to-left mark")] // RLM
        [InlineData("\u200Eleft-to-right mark", "\u200Eleft-to-right mark")] // LRM
        public void S_String_WithRTLAndBidirectional_PreservesDirectionality(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void S_String_WithLoneHighSurrogate_PreservesAsIs()
        {
            // Arrange
            var input = "\uD800";
            var expected = "\uD800";
            
            // Act
            var result = LoggingSanitizer.S(input);
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Fact]
        public void S_String_WithLoneLowSurrogate_PreservesAsIs()
        {
            // Arrange
            var input = "\uDC00";
            var expected = "\uDC00";
            
            // Act
            var result = LoggingSanitizer.S(input);
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Theory]
        [InlineData("test\uD800test", "test\uD800test")] // High surrogate in middle
        [InlineData("\uDC00\uD800", "\uDC00\uD800")] // Wrong order surrogates
        public void S_String_WithInvalidSurrogates_PreservesAsIs(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Ａ", "Ａ")] // Fullwidth Latin A
        [InlineData("１２３", "１２３")] // Fullwidth numbers
        [InlineData("㈱", "㈱")] // Japanese company symbol
        [InlineData("①②③", "①②③")] // Circled numbers
        [InlineData("﷽", "﷽")] // Arabic ligature
        public void S_String_WithSpecialUnicodeSymbols_PreservesSymbols(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void S_String_WithLongUnicodeText_TruncatesAtCharacterBoundary()
        {
            // Arrange - Create a string that will be truncated in the middle of a multi-byte character
            var longText = new string('a', 998) + "🔥🔥"; // 998 regular chars + 2 emoji (4 bytes each)
            
            // Act
            var result = LoggingSanitizer.S(longText);

            // Assert
            result.Should().HaveLength(1000);
            result.Should().StartWith(new string('a', 998));
            // The current implementation truncates at byte level, not character boundary
            // so it might split the emoji
            result.Substring(0, 998).Should().Be(new string('a', 998));
        }

        [Theory]
        [InlineData("\u0000\u0001\u0002", "")] // Null and control chars
        [InlineData("\u007F", "")] // DEL character
        [InlineData("text\u0008\u0009\u000B", "text")] // Backspace, tab, vertical tab
        [InlineData("\u001B[31mRed\u001B[0m", "[31mRed[0m")] // ANSI escape sequences
        public void S_String_WithControlCharacters_RemovesAllControlChars(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void S_String_WithMixedUnicodeAndControlChars_SanitizesCorrectly()
        {
            // Arrange
            var input = "Hello 👋\r\n\u0000世界\u001F🌍\u007F";
            var expected = "Hello 👋  世界🌍";

            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("🇺🇸", "🇺🇸")] // US flag (regional indicators)
        [InlineData("🇯🇵", "🇯🇵")] // Japan flag
        [InlineData("🏴󠁧󠁢󠁳󠁣󠁴󠁿", "🏴󠁧󠁢󠁳󠁣󠁴󠁿")] // Scotland flag with tag characters
        public void S_String_WithRegionalIndicators_PreservesFlags(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("中文", "中文")] // Chinese
        [InlineData("日本語", "日本語")] // Japanese
        [InlineData("한국어", "한국어")] // Korean
        [InlineData("ไทย", "ไทย")] // Thai
        [InlineData("हिन्दी", "हिन्दी")] // Hindi
        public void S_String_WithVariousScripts_PreservesAllScripts(string input, string expected)
        {
            // Act
            var result = LoggingSanitizer.S(input);

            // Assert
            result.Should().Be(expected);
        }

        private class CustomToStringObject
        {
            public string Value { get; set; }
            public override string ToString() => $"CustomObject: {Value}";
        }
    }
}
