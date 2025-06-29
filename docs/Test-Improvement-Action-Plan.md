# Test Improvement Action Plan

## Immediate Fixes (Before Phase 3)

### 1. Fix Behavioral Inconsistencies

#### CorrelationContextService - Null Baggage Handling
**Current Test Expectation**: Null values included in headers
**Actual Behavior**: Null values skipped
**Action**: Determine correct behavior and align test

```csharp
// Option A: Include null values (update implementation)
if (value == null) 
{
    baggage.Add($"{key}=");
}

// Option B: Skip null values (update test)
// Remove expectation of null value in headers
```

#### CancellableTaskRegistry - Task Lifecycle
**Issue**: Tasks immediately removed when cancelled
**Question**: Should cancelled tasks be queryable briefly?
**Action**: Add grace period or query method for recent cancellations

### 2. Refactor Internal Type Dependencies

#### AudioStreamCache - TtsCacheEntry
**Problem**: Tests use reflection to create internal types
**Solution**: 
```csharp
// Option 1: Make TtsCacheEntry public
public class TtsCacheEntry { ... }

// Option 2: Add factory method
public interface IAudioStreamCacheTestHelper 
{
    object CreateCacheEntry(TextToSpeechResponse response);
}
```

### 3. Add Critical Edge Cases

#### LoggingSanitizer
```csharp
[Theory]
[InlineData("Hello\u200BWorld", "HelloWorld")] // Zero-width space
[InlineData("Test\u202EReversed", "TestReversed")] // Right-to-left override
[InlineData("Normal\uFEFFText", "NormalText")] // Byte order mark
public void SanitizeForLogging_WithUnicodeControlCharacters_RemovesThem(string input, string expected)
{
    // Test implementation
}
```

#### AudioEncryptionService
```csharp
[Fact]
public async Task EncryptAudioAsync_WithConcurrentOperations_HandlesCorrectly()
{
    // Arrange
    var tasks = new List<Task<EncryptedAudioData>>();
    var audioData = GenerateTestAudio();
    
    // Act
    for (int i = 0; i < 10; i++)
    {
        tasks.Add(_service.EncryptAudioAsync(audioData));
    }
    var results = await Task.WhenAll(tasks);
    
    // Assert
    results.Should().OnlyHaveUniqueItems(r => r.IV);
}
```

### 4. Performance Benchmarks

Create `ConduitLLM.Tests.Performance` project:
```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class TokenCounterBenchmarks
{
    private TiktokenCounter _counter;
    private string _smallText = "Hello world";
    private string _largeText; // 10KB text
    
    [GlobalSetup]
    public void Setup()
    {
        _counter = new TiktokenCounter();
        _largeText = GenerateLargeText();
    }
    
    [Benchmark]
    public int CountTokens_SmallText() => _counter.Count(_smallText, "gpt-4");
    
    [Benchmark]
    public int CountTokens_LargeText() => _counter.Count(_largeText, "gpt-4");
}
```

## Phase 3 Test Strategy Adjustments

### 1. Behavior-First Testing
```csharp
// ❌ Don't test implementation
_mock.Verify(x => x.CalculateCost(It.IsAny<Usage>()), Times.Once);

// ✅ Test behavior
var cost = await service.CalculateTotalCost(usage);
cost.Should().Be(expectedCost);
```

### 2. Integration Test Categories
```csharp
[Trait("Category", "Integration")]
[Trait("RequiresRedis", "true")]
public class AudioStreamCacheIntegrationTests
{
    // Tests with real Redis instance
}
```

### 3. Contract Tests
```csharp
public class OpenAIContractTests
{
    [Fact]
    public async Task ChatCompletion_ResponseFormat_MatchesContract()
    {
        // Verify our models match OpenAI's actual API
    }
}
```

## Testing Checklist for Each Component

- [ ] Happy path scenarios
- [ ] Edge cases (null, empty, boundaries)
- [ ] Error scenarios
- [ ] Concurrent access
- [ ] Performance benchmarks
- [ ] Integration points
- [ ] Configuration validation
- [ ] Security considerations

## Monitoring Test Quality

### 1. Mutation Testing
```bash
dotnet tool install -g dotnet-stryker
dotnet stryker
```

### 2. Code Coverage with Branch Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:Include="[ConduitLLM.*]*" /p:Exclude="[*.Tests]*"
```

### 3. Test Effectiveness Metrics
- Defect detection rate
- False positive rate
- Test execution time
- Flakiness score

## Timeline

1. **Week 1**: Fix behavioral inconsistencies and refactor internal dependencies
2. **Week 2**: Add edge cases and performance benchmarks
3. **Week 3**: Begin Phase 3 with new testing approach
4. **Ongoing**: Monitor and improve test quality metrics