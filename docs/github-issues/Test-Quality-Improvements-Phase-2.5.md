# Test Quality Improvements - Phase 2.5

## Summary
Based on the Phase 1 & 2 unit test retrospective, we need to implement critical test quality improvements before proceeding with Phase 3. This includes fixing behavioral inconsistencies, adding edge cases, and establishing performance benchmarks.

## Background
We've completed 203 unit tests across Phase 1 and 2 with 100% pass rate. However, the retrospective identified several areas where test quality can be improved:
- Some tests verify implementation details rather than behavior
- Edge case coverage is incomplete (Unicode, concurrency, malformed data)
- No performance benchmarks exist
- Internal type dependencies force complex test workarounds

## Tasks

### Week 1: Immediate Fixes

#### Behavioral Consistency
- [ ] Fix CorrelationContext null baggage handling - determine if nulls should be propagated
- [ ] Add grace period to CancellableTaskRegistry for cancelled task queries
- [ ] Document expected behaviors for ambiguous cases

#### Refactor Internal Dependencies
- [ ] Make AudioStreamCache.TtsCacheEntry public or add factory interface
- [ ] Remove reflection usage in AudioStreamCacheTests
- [ ] Add testability interfaces where needed

#### Critical Edge Cases
- [ ] Add Unicode control character tests to LoggingSanitizer
  - Zero-width spaces
  - Right-to-left overrides
  - Byte order marks
- [ ] Add concurrent operation tests to AudioEncryptionService
- [ ] Add malformed JSON/XML tests to ResponseFormat
- [ ] Add race condition tests to CancellableTaskRegistry

### Week 2: Infrastructure & Benchmarks

#### Performance Benchmark Project
- [ ] Create ConduitLLM.Tests.Performance project
- [ ] Add BenchmarkDotNet package
- [ ] Implement benchmarks for:
  - [ ] TiktokenCounter with various text sizes
  - [ ] AudioEncryptionService throughput
  - [ ] AudioStreamCache hit/miss performance
  - [ ] WebhookCircuitBreaker overhead
  - [ ] LoggingSanitizer with large inputs

#### Test Quality Metrics
- [ ] Install and configure Stryker.NET for mutation testing
- [ ] Set up branch coverage reporting
- [ ] Create test flakiness detection
- [ ] Add test effectiveness dashboard

#### Integration Test Framework
- [ ] Create ConduitLLM.Tests.Integration project
- [ ] Add Docker Compose for test dependencies (Redis, RabbitMQ)
- [ ] Create base classes for integration tests
- [ ] Add test categories for filtered execution

## Acceptance Criteria
1. All behavioral inconsistencies resolved with clear documentation
2. No tests use reflection to access internal types
3. Unicode and concurrency edge cases have comprehensive coverage
4. Performance benchmarks established for all critical paths
5. Mutation testing score > 80%
6. Integration test framework ready for use

## Technical Details

### Example Unicode Edge Case Test
```csharp
[Theory]
[InlineData("Hello\u200BWorld", "HelloWorld")] // Zero-width space
[InlineData("Test\u202EReversed", "TestReversed")] // Right-to-left override
[InlineData("Normal\uFEFFText", "NormalText")] // Byte order mark
public void SanitizeForLogging_WithUnicodeControlCharacters_RemovesThem(string input, string expected)
{
    var result = LoggingSanitizer.SanitizeForLogging(input);
    result.Should().Be(expected);
}
```

### Example Concurrent Operation Test
```csharp
[Fact]
public async Task EncryptAudioAsync_WithConcurrentOperations_ProducesUniqueResults()
{
    // Arrange
    var audioData = GenerateTestAudio();
    var tasks = new List<Task<EncryptedAudioData>>();
    
    // Act
    for (int i = 0; i < 10; i++)
    {
        tasks.Add(_service.EncryptAudioAsync(audioData));
    }
    var results = await Task.WhenAll(tasks);
    
    // Assert
    results.Should().OnlyHaveUniqueItems(r => r.IV);
    results.Should().OnlyHaveUniqueItems(r => r.AuthTag);
}
```

### Example Performance Benchmark
```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class TokenCounterBenchmarks
{
    private TiktokenCounter _counter;
    private string _smallText = "Hello world";
    private string _mediumText; // 1KB
    private string _largeText;   // 100KB
    
    [GlobalSetup]
    public void Setup()
    {
        _counter = new TiktokenCounter();
        _mediumText = GenerateText(1024);
        _largeText = GenerateText(102400);
    }
    
    [Benchmark]
    public int CountTokens_Small() => _counter.Count(_smallText, "gpt-4");
    
    [Benchmark]
    public int CountTokens_Medium() => _counter.Count(_mediumText, "gpt-4");
    
    [Benchmark]
    public int CountTokens_Large() => _counter.Count(_largeText, "gpt-4");
}
```

## Dependencies
- BenchmarkDotNet 0.13.12
- Stryker.NET 4.0.0
- Docker & Docker Compose
- Testcontainers.Redis
- Testcontainers.RabbitMq

## Estimated Effort
- **Total**: 2 weeks
- **Week 1**: Immediate fixes and edge cases
- **Week 2**: Infrastructure and benchmarks

## Priority
High - These improvements directly impact code quality and maintainability

## Labels
- enhancement
- testing
- technical-debt
- performance

## Related Issues
- #232: Epic - Comprehensive Unit Test Implementation
- #231: Test Portfolio Improvement Plan