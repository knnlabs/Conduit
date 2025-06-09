# Plan to Remove All Hardcoded Model References

## Overview
This document outlines the comprehensive plan to remove all hardcoded model references from the ConduitLLM codebase and replace them with a configuration-based system.

## Issues Identified

### Critical Issues (Direct Model Override)
1. **FireworksClient** - Line 198: `request.Model = "nomic-embed-text"` ✅ FIXED
2. **OpenRouterClient** - Multiple hardcoded overrides ✅ FIXED

### Medium Priority Issues (Hardcoded Defaults)
1. **OpenAIClient**:
   - Audio transcription: `"whisper-1"`
   - Text-to-speech: `"tts-1"`
   - Realtime: `"gpt-4o-realtime-preview"`

2. **ElevenLabsClient**:
   - TTS: `"eleven_monolingual_v1"`
   - Realtime: `"eleven_conversational_v1"`

3. **VertexAIClient**:
   - Model aliasing/remapping

4. **Realtime Translators**:
   - OpenAIRealtimeTranslatorV2: `"gpt-4o-realtime-preview"`
   - ElevenLabsRealtimeTranslator: `"conversational-v1"`
   - UltravoxRealtimeTranslator: `"ultravox-v2"`

5. **LLMClientFactory**:
   - Hardcoded `"default-model-id"`

### Low Priority Issues
1. **FireworksClient** - Hardcoded fallback model list

## Solution Architecture

### 1. Configuration Schema ✅ CREATED
Created `ProviderDefaultModels.cs` with:
- Audio defaults (transcription, TTS)
- Realtime defaults
- Provider-specific defaults
- Model aliasing support

### 2. Integration Approach

#### Option A: Constructor Injection (Recommended)
```csharp
public OpenAIClient(
    ProviderCredentials credentials,
    string modelId,
    ILogger<OpenAIClient> logger,
    IHttpClientFactory? httpClientFactory = null,
    ProviderDefaultModels? defaultModels = null)
    : base(credentials, modelId, logger, httpClientFactory, "OpenAI")
{
    _defaultModels = defaultModels ?? new ProviderDefaultModels();
}
```

#### Option B: Factory Pattern Enhancement
```csharp
// In LLMClientFactory
var defaultModels = _settings.DefaultModels;
var providerDefaults = defaultModels.ProviderDefaults.GetValueOrDefault(providerName);

// Pass to client constructor
return new OpenAIClient(credentials, modelId, logger, _httpClientFactory, defaultModels);
```

### 3. Implementation Steps

#### Phase 1: Infrastructure
1. ✅ Create configuration schema
2. ✅ Update ConduitSettings
3. Add default models parameter to BaseLLMClient
4. Update LLMClientFactory to pass configuration

#### Phase 2: Provider Updates
1. **OpenAIClient**:
   ```csharp
   // Instead of:
   content.Add(new StringContent(request.Model ?? "whisper-1"), "model");
   
   // Use:
   var defaultModel = _defaultModels?.Audio?.ProviderOverrides
       ?.GetValueOrDefault("openai")?.TranscriptionModel 
       ?? _defaultModels?.Audio?.DefaultTranscriptionModel 
       ?? "whisper-1";
   content.Add(new StringContent(request.Model ?? defaultModel), "model");
   ```

2. **ElevenLabsClient**:
   ```csharp
   // Instead of:
   var model = request.Model ?? "eleven_monolingual_v1";
   
   // Use:
   var defaultModel = _defaultModels?.Audio?.ProviderOverrides
       ?.GetValueOrDefault("elevenlabs")?.TextToSpeechModel 
       ?? "eleven_monolingual_v1";
   var model = request.Model ?? defaultModel;
   ```

3. **VertexAIClient**:
   ```csharp
   // Move aliasing to configuration
   var alias = _defaultModels?.ProviderDefaults
       ?.GetValueOrDefault("vertexai")?.ModelAliases
       ?.GetValueOrDefault(modelAlias) ?? modelAlias;
   ```

#### Phase 3: Translator Updates
Similar pattern for realtime translators using `_defaultModels.Realtime.ProviderOverrides`.

#### Phase 4: Testing
1. Update unit tests to inject test configurations
2. Add integration tests for configuration loading
3. Verify backward compatibility

### 4. Configuration Example
```json
{
  "ConduitSettings": {
    "DefaultModels": {
      "Audio": {
        "DefaultTranscriptionModel": "whisper-1",
        "DefaultTextToSpeechModel": "tts-1",
        "ProviderOverrides": {
          "openai": {
            "TranscriptionModel": "whisper-1",
            "TextToSpeechModel": "tts-1-hd"
          }
        }
      }
    }
  }
}
```

### 5. Benefits
1. **Flexibility**: Users can override defaults via configuration
2. **Maintainability**: No more hardcoded values scattered across code
3. **Consistency**: All defaults in one place
4. **Testability**: Easy to test with different configurations
5. **Future-proof**: Easy to add new providers and models

### 6. Migration Path
1. Deploy with backward-compatible defaults
2. Document configuration options
3. Provide migration guide for users
4. Eventually deprecate any legacy behavior

## Timeline
- Phase 1: ✅ Complete
- Phase 2: 2-3 days
- Phase 3: 1 day
- Phase 4: 1-2 days

Total estimated effort: 4-6 days