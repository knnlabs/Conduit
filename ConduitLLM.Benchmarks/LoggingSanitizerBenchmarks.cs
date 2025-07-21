using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using ConduitLLM.Core.Extensions;

namespace ConduitLLM.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class LoggingSanitizerBenchmarks
    {
        private readonly string _cleanString = "This is a clean string with no special characters";
        private readonly string _dirtyString = "This has\r\nline breaks\x00null chars\x1Fcontrol chars";
        private readonly string _unicodeString = "Hello 👋 World 🌍 with emojis 🎉 and text\u2028separator\u2029chars";
        private readonly string _longString = new('a', 1500); // Longer than max length
        private readonly string _complexString = "User input: 'test\\r\\n'; DROP TABLE users; --\\x00\\u2028" + new string('x', 1100);
        
        [Benchmark(Baseline = true)]
        public string? Sanitize_CleanString()
        {
            return LoggingSanitizer.S(_cleanString);
        }
        
        [Benchmark]
        public string? Sanitize_DirtyString()
        {
            return LoggingSanitizer.S(_dirtyString);
        }
        
        [Benchmark]
        public string? Sanitize_UnicodeString()
        {
            return LoggingSanitizer.S(_unicodeString);
        }
        
        [Benchmark]
        public string? Sanitize_LongString()
        {
            return LoggingSanitizer.S(_longString);
        }
        
        [Benchmark]
        public string? Sanitize_ComplexString()
        {
            return LoggingSanitizer.S(_complexString);
        }
        
        [Benchmark]
        public string? Sanitize_NullString()
        {
            return LoggingSanitizer.S((string?)null);
        }
        
        [Benchmark]
        public string? Sanitize_EmptyString()
        {
            return LoggingSanitizer.S(string.Empty);
        }
        
        // Test object sanitization
        [Benchmark]
        public object? Sanitize_ObjectWithDirtyToString()
        {
            var obj = new CustomObject { Value = _dirtyString };
            return LoggingSanitizer.S(obj);
        }
        
        // Test primitive type pass-through performance
        [Benchmark]
        public int Sanitize_Int()
        {
            return LoggingSanitizer.S(42);
        }
        
        [Benchmark]
        public decimal Sanitize_Decimal()
        {
            return LoggingSanitizer.S(123.45m);
        }
        
        [Benchmark]
        public DateTime Sanitize_DateTime()
        {
            return LoggingSanitizer.S(DateTime.UtcNow);
        }
        
        // Batch processing
        [Benchmark]
        [Arguments(100)]
        [Arguments(1000)]
        [Arguments(10000)]
        public void Sanitize_BatchStrings(int count)
        {
            for (int i = 0; i < count; i++)
            {
                LoggingSanitizer.S(_dirtyString);
            }
        }
        
        // Worst case: all patterns match
        [Benchmark]
        public string? Sanitize_WorstCase()
        {
            var worstCase = "Line1\r\nLine2\nLine3\r" + 
                           "\x00\x01\x02\x03\x04\x05\x06\x07\x08\x09\x0A\x0B\x0C\x0D\x0E\x0F" +
                           "\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1A\x1B\x1C\x1D\x1E\x1F\x7F" +
                           "\u2028Line\u2029Separator" +
                           new string('x', 1000);
            return LoggingSanitizer.S(worstCase);
        }
        
        private class CustomObject
        {
            public string Value { get; set; } = string.Empty;
            public override string ToString() => $"CustomObject: {Value}";
        }
    }
}