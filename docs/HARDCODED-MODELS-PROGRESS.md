# Hardcoded Models Removal - Progress Report

## Completed Work

### Critical Issues ✅
1. **OpenRouter hardcoded model override** - FIXED
   - Removed the entire workaround that was forcing model IDs
   
2. **FireworksClient hardcoded model override** - FIXED
   - Removed `request.Model = "nomic-embed-text"` direct assignment

### Phase 1: Infrastructure ✅
1. **Created configuration schema** (`ProviderDefaultModels.cs`)
   - Audio defaults (transcription, TTS)
   - Realtime defaults
   - Provider-specific defaults and overrides
   - Model aliasing support

2. **Updated base infrastructure**
   - BaseLLMClient now accepts ProviderDefaultModels
   - LLMClientFactory updated to get configuration (prep work done)
   - OpenAICompatibleClient updated to pass configuration

### Phase 2: Provider Updates (Partial)
1. **OpenAIClient** ✅ COMPLETE
   - Replaced `"whisper-1"` → `GetDefaultTranscriptionModel()`
   - Replaced `"tts-1"` → `GetDefaultTextToSpeechModel()`
   - Replaced `"gpt-4o-realtime-preview"` → `GetDefaultRealtimeModel()`
   - Added configuration helper methods with fallbacks

## Remaining Work

### Phase 2: Provider Updates (Continued)

1. **Update all provider constructors** (17 providers)
   - Each needs to accept ProviderDefaultModels parameter
   - Pass it to base class constructor
   - Currently only OpenAIClient is updated

2. **ElevenLabsClient** 
   - Replace `"eleven_monolingual_v1"` defaults
   - Replace `"eleven_conversational_v1"` defaults
   - Update constructor

3. **VertexAIClient**
   - Move hardcoded model aliasing to configuration
   - Update GetModelInfo method
   - Update constructor

4. **LLMClientFactory**
   - Fix hardcoded `"default-model-id"`
   - Update to pass defaultModels to all providers

5. **FireworksClient**
   - Move fallback model list to configuration
   - Update constructor

### Phase 3: Translator Updates

1. **OpenAIRealtimeTranslatorV2**
   - Replace `"gpt-4o-realtime-preview"` default
   - Update constructor to accept configuration

2. **ElevenLabsRealtimeTranslator**
   - Replace `"conversational-v1"` default
   - Update constructor to accept configuration

3. **UltravoxRealtimeTranslator**
   - Replace `"ultravox-v2"` default
   - Update constructor to accept configuration

### Phase 4: Testing & Documentation

1. **Create unit tests**
   - Test configuration-based defaults work
   - Test fallback behavior
   - Test provider-specific overrides

2. **Create integration tests**
   - Test full configuration loading
   - Test with Docker deployment

3. **Update documentation**
   - Configuration guide with examples
   - Migration guide for users
   - List all configurable defaults

## Technical Debt

### Constructor Parameter Order Issue
Currently, providers have inconsistent constructor signatures:
- Some need defaultModels as 5th parameter
- Some need it as 6th parameter (after providerName)
- Need to standardize this across all providers

### Suggested Approach
1. Update all providers to have consistent constructor signature
2. Make defaultModels optional (with null default)
3. Consider using a builder pattern for complex initialization

## Next Steps

1. Create a script to update all provider constructors systematically
2. Update ElevenLabsClient as the next provider (simpler than VertexAI)
3. Update translators after all providers are done
4. Write comprehensive tests
5. Update documentation

## Configuration Example

```json
{
  "ConduitSettings": {
    "DefaultModels": {
      "Audio": {
        "DefaultTranscriptionModel": "whisper-1",
        "DefaultTextToSpeechModel": "tts-1",
        "ProviderOverrides": {
          "openai": {
            "TranscriptionModel": "whisper-large-v3",
            "TextToSpeechModel": "tts-1-hd"
          },
          "elevenlabs": {
            "TextToSpeechModel": "eleven_turbo_v2"
          }
        }
      },
      "Realtime": {
        "DefaultRealtimeModel": "gpt-4o-realtime-preview",
        "ProviderOverrides": {
          "openai": "gpt-4o-realtime-preview-2024-12-17",
          "elevenlabs": "conversational-v2",
          "ultravox": "ultravox-v2-latest"
        }
      }
    }
  }
}
```

## Benefits So Far

1. **OpenAI users can now configure default models** without code changes
2. **No more forced model overrides** breaking user expectations  
3. **Backward compatible** - old deployments continue to work
4. **Foundation laid** for completing the remaining providers