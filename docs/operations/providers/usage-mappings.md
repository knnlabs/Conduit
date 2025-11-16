# Provider Usage Mappings

This document describes how different providers map their usage metrics to the Conduit Usage model.

## Overview

The `Usage` model in Conduit provides a unified structure for tracking resource consumption across different AI providers. Each provider reports usage differently, so proper mapping is essential for accurate cost calculation.

## Token-Based Usage

### OpenAI / Azure OpenAI
```csharp
usage.PromptTokens = response.Usage.PromptTokens;
usage.CompletionTokens = response.Usage.CompletionTokens;
usage.TotalTokens = response.Usage.TotalTokens;
```

### Anthropic (Claude)
```csharp
usage.PromptTokens = response.Usage.InputTokens;
usage.CompletionTokens = response.Usage.OutputTokens;
usage.TotalTokens = usage.PromptTokens + usage.CompletionTokens;

// Cached tokens (if using prompt caching)
usage.CachedInputTokens = response.Usage.CacheReadInputTokens;
usage.CachedWriteTokens = response.Usage.CacheCreationInputTokens;
```

### Google (Gemini/Vertex AI)
```csharp
usage.PromptTokens = response.UsageMetadata.PromptTokenCount;
usage.CompletionTokens = response.UsageMetadata.CandidatesTokenCount;
usage.TotalTokens = response.UsageMetadata.TotalTokenCount;

// Cached tokens (if using context caching)
usage.CachedInputTokens = response.UsageMetadata.CachedContentTokenCount;
```

### Cohere
```csharp
// For chat/generation
usage.PromptTokens = response.Meta.Tokens.InputTokens;
usage.CompletionTokens = response.Meta.Tokens.OutputTokens;
usage.TotalTokens = usage.PromptTokens + usage.CompletionTokens;

// For reranking
usage.SearchUnits = CalculateSearchUnits(request.Query, request.Documents);
usage.SearchMetadata = new SearchUsageMetadata
{
    QueryCount = 1,
    DocumentCount = request.Documents.Count,
    ChunkedDocumentCount = CountChunkedDocuments(request.Documents)
};
```

## Image Generation Usage

### OpenAI (DALL-E)
```csharp
usage.ImageCount = response.Data.Count;
usage.ImageQuality = request.Quality; // "standard" or "hd"
```

### Replicate
```csharp
usage.ImageCount = response.Output.Count;
usage.InferenceSteps = request.NumInferenceSteps ?? modelDefaults.InferenceSteps;
```

### Fireworks
```csharp
usage.ImageCount = 1;
usage.InferenceSteps = response.Steps ?? request.Steps ?? GetDefaultSteps(model);
usage.ImageQuality = request.Quality;
```

## Audio Usage

### OpenAI (Whisper/TTS)
```csharp
// For transcription (Whisper)
usage.AudioDurationSeconds = CalculateAudioDuration(audioFile);

// For text-to-speech
usage.AudioDurationSeconds = CalculateGeneratedAudioDuration(response.Audio);
```

### ElevenLabs
```csharp
usage.AudioDurationSeconds = response.AudioDurationSeconds;
// Alternative: character-based billing
usage.Metadata = new Dictionary<string, object>
{
    ["character_count"] = request.Text.Length
};
```

## Video Generation Usage

### Replicate (Various Models)
```csharp
usage.VideoDurationSeconds = response.Metadata.DurationSeconds;
usage.VideoResolution = $"{response.Metadata.Width}x{response.Metadata.Height}";
```

### RunwayML
```csharp
usage.VideoDurationSeconds = request.Duration ?? 4.0; // Default 4 seconds
usage.VideoResolution = request.Resolution ?? "1280x768";
```

## Batch Processing

When processing batch requests, set the batch flag:
```csharp
usage.IsBatch = true; // Enables batch pricing discounts
```

## Metadata Usage

Use the metadata dictionary for provider-specific information:

```csharp
usage.Metadata = new Dictionary<string, object>
{
    // Cache information
    ["cache_ttl"] = 3600,
    ["cache_hit_rate"] = 0.85,
    
    // Provider-specific details
    ["provider"] = "anthropic",
    ["model_version"] = "2024-02-01",
    
    // Request details
    ["request_id"] = response.Id,
    ["region"] = "us-east-1",
    
    // Performance metrics
    ["latency_ms"] = 1234,
    ["queue_time_ms"] = 56
};
```

## Search/Rerank Units Calculation

For providers that charge by search units:

```csharp
private int CalculateSearchUnits(string query, List<string> documents)
{
    // 1 search unit = 1 query + up to 100 documents
    var totalDocs = documents.Count;
    
    // Count documents that need chunking (>500 tokens)
    var chunkedDocs = documents.Count(d => CountTokens(d) > 500);
    var effectiveDocs = totalDocs + chunkedDocs;
    
    // Calculate units (round up)
    return (int)Math.Ceiling(effectiveDocs / 100.0);
}
```

## Best Practices

1. **Always validate usage data** before storing or using for cost calculation
2. **Handle null values gracefully** - not all providers report all metrics
3. **Use metadata for debugging** - store request IDs, timestamps, etc.
4. **Document provider quirks** - some providers have unique billing rules
5. **Test edge cases** - zero tokens, failed requests, partial completions

## Provider-Specific Notes

### Anthropic
- Cached tokens are billed differently than regular tokens
- Cache write happens on first use of a prompt
- Cache reads are significantly cheaper

### Cohere
- Rerank charges per search unit, not per token
- Documents over 500 tokens are split and charged as multiple documents

### Fireworks
- Different models have different default inference steps
- Step count directly affects both quality and cost

### Google
- Context caching similar to Anthropic but with different pricing
- Some models support cached input but not cached write

## Future Considerations

As new providers and pricing models emerge, consider:
- Function/tool usage tracking
- Fine-tuned model surcharges
- Training token tracking
- Embedding dimension-based pricing
- Real-time vs batch pricing differences