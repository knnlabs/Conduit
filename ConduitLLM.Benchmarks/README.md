# ConduitLLM Benchmarks

This project contains performance benchmarks for critical paths in the ConduitLLM system using BenchmarkDotNet.

## Running Benchmarks

### Run all benchmarks:
```bash
dotnet run -c Release
```

### Run specific benchmark class:
```bash
dotnet run -c Release -- --filter "ConduitLLM.Benchmarks.CostCalculationBenchmarks.*"
```

### Run with specific runtime:
```bash
dotnet run -c Release -- --runtimes net9.0
```

### Export results:
```bash
dotnet run -c Release -- --exporters json html
```

## Benchmark Categories

### 1. Cost Calculation Benchmarks
- Simple usage cost calculation
- Complex usage with images and video
- Embedding-only calculations
- Audio cost calculations (transcription, TTS, realtime)
- Batch processing performance

### 2. Caching Benchmarks
- Memory cache set/get operations
- Cache miss scenarios
- Concurrent read performance
- Various data sizes (1KB to 10MB)
- AudioStreamCache operations

### 3. Logging Sanitizer Benchmarks
- Clean string pass-through
- String with control characters
- Unicode and emoji handling
- Long string truncation
- Worst-case scenario (all patterns match)
- Primitive type pass-through

## Interpreting Results

BenchmarkDotNet provides:
- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of measurements
- **Median**: Middle value of measurements
- **Allocated**: Memory allocated per operation

### Key Metrics to Watch

1. **Cost Calculations**
   - Should complete in microseconds for simple cases
   - Complex calculations should stay under 1ms
   - Batch processing should scale linearly

2. **Caching**
   - Memory cache hits should be sub-microsecond
   - Cache misses should be fast (checking both caches)
   - Concurrent reads should scale well

3. **Sanitization**
   - Clean strings should have minimal overhead
   - Complex sanitization should stay under 100μs
   - Memory allocation should be minimal

## Adding New Benchmarks

1. Create a new class with `[MemoryDiagnoser]` attribute
2. Add `[Benchmark]` to methods you want to measure
3. Use `[GlobalSetup]` for initialization
4. Use `[Arguments]` for parameterized benchmarks
5. Mark baseline with `[Benchmark(Baseline = true)]`

## CI/CD Integration

To run benchmarks in CI and detect performance regressions:

```bash
# Run and save baseline
dotnet run -c Release -- --exporters json --artifacts baseline

# Run and compare
dotnet run -c Release -- --exporters json --artifacts current
# Then compare baseline/results.json with current/results.json
```