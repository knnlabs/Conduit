# Multi-Provider Tokenization System

**Last Updated**: 2025-11-17
**Related Issue**: [#857 - Epic: Modernize Tokenizer Support for Multi-Provider Token Counting](https://github.com/knnlabs/Conduit/issues/857)

## Overview

The multi-provider tokenization system provides accurate token counting across different LLM providers, replacing the previous OpenAI-only approach with a flexible, extensible architecture that supports multiple tokenizer types.

## Problem Statement

Previously, Conduit relied heavily on TiktokenSharp for token counting, which only works for OpenAI models. Non-OpenAI models (Claude, Gemini, LLaMA-based models, etc.) used crude character-based estimation (dividing by 4), leading to:

- **Inaccurate cost calculations**
- **Potential revenue loss** from undercharging
- **Poor user experience** with unreliable context window management

## Architecture

### Components

```
┌─────────────────────────────────────────────────────────────┐
│                    TokenCounterFactory                       │
│  - Selects appropriate counter based on TokenizerType       │
│  - Caches counter instances for performance                 │
│  - Provides fallback mechanisms                             │
└──────────────────┬──────────────────────────────────────────┘
                   │
       ┌───────────┼───────────┬───────────────┐
       │           │           │               │
       ▼           ▼           ▼               ▼
┌─────────────┐ ┌──────────────┐ ┌──────────┐ ┌─────────────┐
│  Tiktoken   │ │   Fallback   │ │  LLaMA   │ │   Future    │
│   Counter   │ │   Counter    │ │  Counter │ │  Counters   │
│  (OpenAI)   │ │ (Universal)  │ │ (TBD)    │ │  (Claude,   │
│             │ │              │ │          │ │   Gemini)   │
└─────────────┘ └──────────────┘ └──────────┘ └─────────────┘
```

### 1. TokenCounterFactory

**Location**: `Shared/ConduitLLM.Core/Services/TokenCounterFactory.cs`

The factory implements the Factory pattern to select the most appropriate token counter based on a model's `TokenizerType`.

**Key Features**:
- Model-specific counter selection
- Instance caching for performance
- Automatic fallback to safe defaults
- Extensible design for adding new tokenizers

**Usage**:
```csharp
// Get counter for a specific model
var factory = serviceProvider.GetRequiredService<TokenCounterFactory>();
var counter = await factory.GetCounterForModelAsync("gpt-4");
var tokenCount = await counter.EstimateTokenCountAsync("gpt-4", messages);

// Or get counter by tokenizer type directly
var llamaCounter = factory.GetCounterForTokenizerType(TokenizerType.LLaMA3);
```

### 2. TiktokenCounter (OpenAI Models)

**Location**: `Shared/ConduitLLM.Core/Services/TiktokenCounter.cs`

Handles OpenAI models using TiktokenSharp library.

**Supported Tokenizers**:
- `Cl100KBase` - GPT-3.5-turbo, GPT-4, GPT-4-turbo
- `O200KBase` - GPT-4o, GPT-4o-mini
- `P50KBase` - Older GPT-3 models
- `P50KEdit` - Edit models
- `R50KBase` - Codex models

**Characteristics**:
- ✅ Highly accurate (uses official tokenizer)
- ✅ Fast (cached encodings)
- ✅ Handles multimodal content (text + images)

### 3. FallbackTokenCounter (Universal Estimator)

**Location**: `Shared/ConduitLLM.Core/Services/FallbackTokenCounter.cs`

Provides improved token estimation for models without specific tokenizer support using empirically-determined character-to-token ratios.

**Model-Specific Ratios**:
```csharp
TokenizerType.Claude          → 3.8 chars/token
TokenizerType.Claude3         → 3.8 chars/token
TokenizerType.Gemini          → 3.5 chars/token
TokenizerType.LLaMA3          → 4.0 chars/token
TokenizerType.Mistral         → 4.0 chars/token
TokenizerType.Cohere          → 3.8 chars/token
Default                       → 4.0 chars/token
```

**Characteristics**:
- ✅ Better than previous fixed 4:1 ratio
- ✅ Model-family aware
- ✅ Conservative estimates (uses ceiling)
- ⚠️ Not as accurate as native tokenizers

### 4. LlamaTokenCounter (With Auto-Download)

**Location**: `Shared/ConduitLLM.Core/Services/LlamaTokenCounter.cs`

Native LLaMA tokenizer implementation using Microsoft.ML.Tokenizers with automatic model file downloading.

**Supported Models**:
- LLaMA 2 (SentencePiece, ~500 KB)
- LLaMA 3 (Tiktoken/BPE, ~2.18 MB)
- LLaMA 3.1 (Tiktoken/BPE, ~2.18 MB)

**Characteristics**:
- ✅ **Automatic downloading** from HuggingFace (configurable)
- ✅ **Local caching** for offline usage
- ✅ **Graceful fallback** to character-based estimation if download fails
- ✅ **Production-ready** with retry logic and error handling
- ⚠️ First-run latency (2-4 seconds for initial download)

**How It Works**:
1. First request for LLaMA model triggers tokenizer check
2. If not cached: Downloads ~2 MB file from HuggingFace (once)
3. Caches locally in `~/.local/share/Conduit/tokenizers` (or custom location)
4. Subsequent requests use cached file (instant)
5. If download fails: Falls back to FallbackTokenCounter estimation

### 5. TokenizerModelLoader (Auto-Download Service)

**Location**: `Shared/ConduitLLM.Core/Services/TokenizerModelLoader.cs`

Service that handles automatic downloading and caching of tokenizer model files.

**Features**:
- **Automatic downloading** from HuggingFace Hub
- **Intelligent caching** to avoid repeated downloads
- **Retry logic** with exponential backoff (2s, 4s, 8s)
- **Graceful degradation** if HuggingFace is unavailable
- **Configurable** via `TokenizationOptions`

**File Sizes**:
- LLaMA 2: ~500 KB (SentencePiece format)
- LLaMA 3/3.1: ~2.18 MB (Tiktoken format)

**Configuration**:
See `docs/claude/tokenization-configuration-example.json` for full options.

```json
{
  "ConduitLLM": {
    "Tokenization": {
      "AutoDownloadTokenizers": true,    // Enable auto-download
      "CacheDirectory": null,             // Use default location
      "DownloadTimeoutMs": 30000,        // 30 second timeout
      "RetryAttempts": 3,                // Retry 3 times
      "FallbackOnDownloadFailure": true  // Don't crash if download fails
    }
  }
}
```

**Environment Variables** (override appsettings.json):
```bash
# Disable auto-download in air-gapped environments
CONDUITLLM__TOKENIZATION__AUTODOWNLOADTOKENIZERS=false

# Custom cache location (useful for Docker volumes)
CONDUITLLM__TOKENIZATION__CACHEDIRECTORY=/app/tokenizers
```

## Token Counting Flow

```
┌─────────────────────────────────────────────────────────────┐
│  1. UsageEstimationService needs token count for a model    │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│  2. Uses ITokenCounter (injected via DI)                    │
│     - Currently defaults to FallbackTokenCounter            │
│     - Or use TokenCounterFactory for model-specific counter │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│  3. FallbackTokenCounter gets TokenizerType from            │
│     IModelCapabilityService (database-backed)               │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│  4. Applies appropriate chars/token ratio                   │
│     - Claude: 3.8 chars/token                               │
│     - Gemini: 3.5 chars/token                               │
│     - LLaMA: 4.0 chars/token                                │
│     - OpenAI: Uses TiktokenCounter instead                  │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│  5. Returns conservative token estimate                     │
│     - Uses Math.Ceiling to avoid underestimation            │
│     - Adds message overhead (4 tokens/message)              │
│     - Adds reply priming tokens (3 tokens)                  │
└─────────────────────────────────────────────────────────────┘
```

## TokenizerType Enum

**Location**: `Shared/ConduitLLM.Configuration/Entities/TokenizerType.cs`

The enum defines all supported tokenizer types:

```csharp
public enum TokenizerType
{
    None = 0,

    // OpenAI tokenizers
    Cl100KBase,      // GPT-3.5-turbo, GPT-4, GPT-4-turbo
    P50KBase,        // Older GPT-3 models
    P50KEdit,        // Edit models
    R50KBase,        // Codex models
    O200KBase,       // GPT-4o, GPT-4o-mini

    // Anthropic tokenizers
    Claude,          // Claude 2 and earlier
    Claude3,         // Claude 3 (Haiku, Sonnet, Opus)

    // Google tokenizers
    Gemini,          // Gemini models
    PaLM,            // PaLM and PaLM 2 models

    // Meta tokenizers
    LLaMA,           // LLaMA 1
    LLaMA2,          // LLaMA 2
    LLaMA3,          // LLaMA 3

    // Mistral tokenizers
    Mistral,         // Mistral models

    // Cohere tokenizers
    Cohere,          // Command and other Cohere models

    // Others
    O200KHarmony,    // GPT-OSS models
    Kimi,            // Kimi K2 models
    Groq,            // Groq-specific
    Cerebras,        // Cerebras-specific
    MiniMax,         // MiniMax models

    // Generic tokenizers
    SentencePiece,   // Used by various models
    BPE,             // Byte Pair Encoding (generic)
    WordPiece,       // Used by BERT-style models
    Tiktoken         // OpenAI's open-source tokenizer library
}
```

## Database Integration

Models in the database have a `TokenizerType` field that specifies which tokenizer to use:

```sql
-- Model table
CREATE TABLE "Models" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(255) NOT NULL,
    "TokenizerType" INTEGER NOT NULL,  -- Maps to TokenizerType enum
    ...
);

-- ModelSeries table (inherited by models)
CREATE TABLE "ModelSeries" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(255) NOT NULL,
    "TokenizerType" INTEGER NOT NULL,  -- Default for series
    ...
);
```

**Configuration Flow**:
1. Admin configures model via WebAdmin UI or Admin API
2. Sets `TokenizerType` for model or model series
3. Value stored in database as integer (enum value)
4. `DatabaseModelCapabilityService` retrieves tokenizer type
5. `TokenCounterFactory` or `FallbackTokenCounter` uses it for estimation

## Usage Examples

### Example 1: Automatic Token Counting

```csharp
// Service injection
public class MyService
{
    private readonly ITokenCounter _tokenCounter;

    public MyService(ITokenCounter tokenCounter)
    {
        _tokenCounter = tokenCounter;
    }

    public async Task<int> GetTokenCount(string model, List<Message> messages)
    {
        // Automatically uses appropriate counter based on model's TokenizerType
        return await _tokenCounter.EstimateTokenCountAsync(model, messages);
    }
}
```

### Example 2: Factory-Based Token Counting

```csharp
// For advanced scenarios requiring model-specific counters
public class AdvancedService
{
    private readonly TokenCounterFactory _factory;

    public AdvancedService(TokenCounterFactory factory)
    {
        _factory = factory;
    }

    public async Task<Dictionary<string, int>> GetTokenCountsForMultipleModels(
        List<string> models,
        List<Message> messages)
    {
        var results = new Dictionary<string, int>();

        foreach (var model in models)
        {
            var counter = await _factory.GetCounterForModelAsync(model);
            var tokens = await counter.EstimateTokenCountAsync(model, messages);
            results[model] = tokens;
        }

        return results;
    }
}
```

### Example 3: Direct Counter Type Selection

```csharp
// When you know the exact tokenizer type
public class SpecificService
{
    private readonly TokenCounterFactory _factory;

    public SpecificService(TokenCounterFactory factory)
    {
        _factory = factory;
    }

    public async Task<int> GetOpenAITokenCount(List<Message> messages)
    {
        // Force use of TiktokenCounter for OpenAI models
        var counter = _factory.GetCounterForTokenizerType(TokenizerType.Cl100KBase);
        return await counter.EstimateTokenCountAsync("gpt-4", messages);
    }
}
```

## Conservative Buffering

The `UsageEstimationService` applies a 10% conservative buffer to all estimates to prevent undercharging:

```csharp
private const double ConservativeBufferPercentage = 0.10;

var bufferedTokens = (int)Math.Ceiling(estimatedTokens * (1 + ConservativeBufferPercentage));
```

This ensures that:
- ✅ We never undercharge customers
- ✅ Slight overestimates are acceptable for billing
- ✅ Covers variance in tokenization methods

## Performance Optimizations

### 1. Counter Instance Caching

The factory caches counter instances per `TokenizerType`:

```csharp
private readonly Dictionary<TokenizerType, ITokenCounter> _counterCache = new();
```

**Benefits**:
- Avoids repeated counter initialization
- Reuses tokenizer resources
- Thread-safe with locking

### 2. Tokenizer Caching (TiktokenCounter)

OpenAI tokenizers are cached by encoding name:

```csharp
private static readonly Dictionary<string, TikToken> _encodings = new();
```

**Benefits**:
- Expensive encoding creation done once
- Shared across all instances
- Significant performance improvement

## Future Enhancements

### Phase 1: ✅ Infrastructure (COMPLETED)
- ✅ `TokenizerType` enum with 27+ types
- ✅ Database schema updates
- ✅ `IModelCapabilityService` integration
- ✅ `TokenCounterFactory` pattern

### Phase 2: ✅ OpenAI Support (COMPLETED)
- ✅ `TiktokenCounter` for all OpenAI encodings
- ✅ Multimodal content support
- ✅ Caching for performance

### Phase 3: ✅ Universal Fallback (COMPLETED)
- ✅ `FallbackTokenCounter` with model-specific ratios
- ✅ Better than fixed 4:1 estimation
- ✅ Conservative buffering

### Phase 4: ⏳ Native LLaMA Support (IN PROGRESS)
- ⏳ Complete `LlamaTokenCounter` implementation
- ⏳ Tokenizer model file management
- ⏳ Microsoft.ML.Tokenizers integration
- ⏳ Support for Groq, Cerebras, Meta models

### Phase 5: 🔲 Native Claude Support (PLANNED)
- 🔲 Add `LLMSharp.Anthropic.Tokenizer` package
- 🔲 Implement `ClaudeTokenCounter`
- 🔲 Accurate counting for Anthropic models

### Phase 6: 🔲 Native Gemini Support (PLANNED)
- 🔲 Add Google tokenizer library
- 🔲 Implement `GeminiTokenCounter`
- 🔲 Accurate counting for Google models

### Phase 7: 🔲 Advanced Features (PLANNED)
- 🔲 Async tokenizer loading
- 🔲 Distributed caching (Redis)
- 🔲 Telemetry and metrics
- 🔲 A/B testing different estimation methods

## Migration from Old System

The new system is **backward compatible**:

1. **Existing Code**: Works unchanged
   - `ITokenCounter` still injected via DI
   - Now provides better estimates via `FallbackTokenCounter`

2. **OpenAI Models**: Zero impact
   - Still use `TiktokenCounter`
   - Same accuracy as before

3. **Non-OpenAI Models**: **Improved accuracy**
   - Previously: Fixed 4:1 ratio for all models
   - Now: Model-family-specific ratios
   - Example: Claude uses 3.8:1 instead of 4:1

## Testing

### Unit Tests

Create tests for each counter implementation:

```csharp
[Fact]
public async Task FallbackTokenCounter_ClaudeModel_UsesCorrectRatio()
{
    // Arrange
    var mockCapabilityService = CreateMockCapabilityService("Claude3");
    var counter = new FallbackTokenCounter(logger, mockCapabilityService);
    var text = "Hello, world!"; // 13 characters

    // Act
    var tokens = await counter.EstimateTokenCountAsync("claude-3-sonnet", text);

    // Assert
    // 13 chars / 3.8 chars per token = 3.42 → rounds up to 4
    Assert.Equal(4, tokens);
}
```

### Integration Tests

Test the factory pattern:

```csharp
[Fact]
public async Task TokenCounterFactory_OpenAIModel_ReturnsTiktokenCounter()
{
    // Arrange
    var factory = CreateTokenCounterFactory();

    // Act
    var counter = await factory.GetCounterForModelAsync("gpt-4");

    // Assert
    Assert.IsType<TiktokenCounter>(counter);
}
```

## Troubleshooting

### Issue: Token counts seem inaccurate

**Diagnosis**:
1. Check model's `TokenizerType` in database
2. Verify appropriate counter is being used
3. Check logs for warnings about fallback usage

**Solution**:
```sql
-- Update model's tokenizer type
UPDATE "Models"
SET "TokenizerType" = 14  -- Claude3
WHERE "Name" = 'claude-3-sonnet-20240229';
```

### Issue: Performance degradation

**Diagnosis**:
1. Check if counters are being cached
2. Monitor tokenizer creation frequency

**Solution**:
- Ensure `TokenCounterFactory` is registered as Scoped
- Verify counter instances are reused

### Issue: Null reference exceptions

**Diagnosis**:
1. `IModelCapabilityService` not registered
2. Database connection issues

**Solution**:
```csharp
// Ensure capability service is registered
services.TryAddScoped<IModelCapabilityService, DatabaseModelCapabilityService>();
```

## References

- [Issue #857](https://github.com/knnlabs/Conduit/issues/857) - Original epic
- [Microsoft.ML.Tokenizers Documentation](https://www.nuget.org/packages/Microsoft.ML.Tokenizers)
- [TiktokenSharp Documentation](https://github.com/aiqinxuancai/TiktokenSharp)
- [OpenAI Tokenizer](https://platform.openai.com/tokenizer)
- [Claude Tokenization](https://docs.anthropic.com/claude/reference/tokens)

## Related Documentation

- [Provider Multi-Instance Architecture](provider-multi-instance.md)
- [Model Cost Mapping](model-cost-mapping.md)
- [Provider Models](provider-models.md)
- [Database Migration Guide](database-migration-guide.md)
