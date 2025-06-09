# Hardcoded Models Status and Removal Plan

## Overview

While Conduit has a dynamic model configuration system through the `ModelProviderMapping` entity and database, several components still contain hardcoded model references. This document tracks the current status and provides a plan for complete removal.

## Current Status

### Components with Hardcoded Models

#### 1. ModelCapabilityDetector.cs
**Location**: `/ConduitLLM.Core/Services/ModelCapabilityDetector.cs`

**Hardcoded Models**:
```csharp
// Vision-capable models
- OpenAI: "gpt-4-vision", "gpt-4-turbo", "gpt-4v", "gpt-4o"
- Anthropic: "claude-3", "claude-3-opus", "claude-3-sonnet", "claude-3-haiku"
- Gemini: "gemini", "gemini-pro", "gemini-pro-vision"
- Bedrock: "claude-3", "claude-3-haiku", "claude-3-sonnet", "claude-3-opus"
- VertexAI: "gemini"
```

**Impact**: New vision-capable models require code changes.

#### 2. TiktokenCounter.cs
**Location**: `/ConduitLLM.Core/Services/TiktokenCounter.cs`

**Hardcoded Models**:
```csharp
// Tokenizer selection
- "gpt-3.5" → cl100k_base encoding
- "gpt-4" → cl100k_base encoding
- "davinci", "curie", "babbage", "ada" → p50k_base encoding
- "claude" → claude tokenizer
```

**Impact**: New models may not get correct token counting.

#### 3. AudioCapabilityDetector.cs
**Location**: `/ConduitLLM.Core/Services/AudioCapabilityDetector.cs`

**Hardcoded Models**:
```csharp
// Audio models and capabilities
- Transcription: "whisper-1"
- TTS: "tts-1", "tts-1-hd"
- Voices: "alloy", "echo", "fable", "onyx", "nova", "shimmer"
- Supported languages: List of 50+ languages
- Supported formats: mp3, opus, aac, flac, wav, pcm
```

**Impact**: New audio models or voices require code changes.

#### 4. ProviderDefaultModels.cs
**Location**: `/ConduitLLM.Configuration/ProviderDefaultModels.cs`

**Hardcoded Defaults**:
```csharp
public static class ProviderDefaultModels
{
    public const string DefaultTranscriptionModel = "whisper-1";
    public const string DefaultTTSModel = "tts-1";
    public const string DefaultRealtimeModel = "gpt-4o-realtime-preview";
    
    // Provider-specific defaults
    ElevenLabs: "conversational-v1"
    Ultravox: "ultravox-v2"
}
```

**Impact**: Cannot change defaults without recompilation.

#### 5. SQL Seed Data
**Location**: `/add-frontier-model-costs.sql`

**Hardcoded Data**: Model cost information (acceptable as configuration data).

## Proposed Solution

### Phase 1: Extend Model Configuration Schema

Add capability flags to the `ModelProviderMapping` entity:

```csharp
public class ModelProviderMapping
{
    // Existing properties...
    
    // New capability properties
    public bool SupportsVision { get; set; }
    public bool SupportsAudioTranscription { get; set; }
    public bool SupportsTextToSpeech { get; set; }
    public bool SupportsRealtimeAudio { get; set; }
    public string? TokenizerType { get; set; } // "cl100k_base", "p50k_base", "claude", etc.
    public string? SupportedVoices { get; set; } // JSON array
    public string? SupportedLanguages { get; set; } // JSON array
    public string? SupportedFormats { get; set; } // JSON array
    public bool IsDefault { get; set; } // For default model selection
}
```

### Phase 2: Create Model Capability Service

Replace hardcoded detectors with a database-driven service:

```csharp
public interface IModelCapabilityService
{
    Task<bool> SupportsVisionAsync(string model);
    Task<bool> SupportsAudioTranscriptionAsync(string model);
    Task<bool> SupportsTextToSpeechAsync(string model);
    Task<bool> SupportsRealtimeAudioAsync(string model);
    Task<string?> GetTokenizerTypeAsync(string model);
    Task<List<string>> GetSupportedVoicesAsync(string model);
    Task<List<string>> GetSupportedLanguagesAsync(string model);
    Task<List<string>> GetSupportedFormatsAsync(string model);
    Task<string?> GetDefaultModelAsync(string provider, string capability);
}

public class ModelCapabilityService : IModelCapabilityService
{
    private readonly IModelProviderMappingRepository _repository;
    private readonly ICacheService _cache;
    
    // Implementation with caching for performance
}
```

### Phase 3: Migration Steps

1. **Database Migration**:
   - Add new columns to ModelProviderMapping table
   - Create migration script
   - Seed existing model capabilities

2. **Service Implementation**:
   - Implement ModelCapabilityService
   - Add caching for performance
   - Add Admin API endpoints for capability management

3. **Replace Hardcoded References**:
   - Update ModelCapabilityDetector to use service
   - Update AudioCapabilityDetector to use service
   - Update TiktokenCounter to use service
   - Remove ProviderDefaultModels static class

4. **UI Updates**:
   - Add capability checkboxes to model configuration UI
   - Add voice/language/format configuration
   - Add default model selection

### Phase 4: Implementation Plan

#### Week 1: Database and API
- [ ] Create database migration for new columns
- [ ] Implement ModelCapabilityService
- [ ] Add Admin API endpoints
- [ ] Write unit tests

#### Week 2: Service Integration
- [ ] Update ModelCapabilityDetector
- [ ] Update AudioCapabilityDetector
- [ ] Update TiktokenCounter
- [ ] Integration testing

#### Week 3: UI and Migration
- [ ] Update WebUI model configuration
- [ ] Create data migration tool
- [ ] Migrate existing hardcoded data
- [ ] Remove hardcoded classes

#### Week 4: Testing and Documentation
- [ ] Comprehensive testing
- [ ] Update documentation
- [ ] Performance testing
- [ ] Deployment plan

## Benefits

1. **Dynamic Configuration**: Add new models without code changes
2. **Easier Maintenance**: All model info in one place
3. **Better Testing**: Can mock capabilities easily
4. **Improved Flexibility**: Per-deployment model configuration
5. **Reduced Deployment Risk**: No code changes for new models

## Risks and Mitigation

### Risk 1: Performance Impact
**Mitigation**: Implement aggressive caching with cache invalidation on changes.

### Risk 2: Migration Complexity
**Mitigation**: Keep hardcoded fallbacks during transition period.

### Risk 3: Configuration Errors
**Mitigation**: Add validation and provide configuration UI with sensible defaults.

## Success Criteria

1. All model capabilities configured via database
2. No hardcoded model names in code (except examples)
3. Admin UI for managing model capabilities
4. Performance within 5% of hardcoded version
5. Zero downtime migration

## Timeline

- **Estimated Duration**: 4 weeks
- **Priority**: Medium (system works with current limitation)
- **Prerequisites**: None
- **Dependencies**: Admin API, WebUI

## Next Steps

1. Review and approve this plan
2. Create detailed technical design
3. Estimate resource requirements
4. Schedule implementation sprint
5. Begin with database schema changes