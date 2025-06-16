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

2. **ElevenLabsClient** ✅ COMPLETE
   - Updated constructor to accept ProviderDefaultModels
   - Replaced `"eleven_monolingual_v1"` → `GetDefaultTextToSpeechModel()`
   - Replaced `"eleven_conversational_v1"` → `GetDefaultRealtimeModel()`
   - Added configuration helper methods with fallbacks
   - Updated LLMClientFactory to pass configuration

## Remaining Work

### Phase 2: Provider Updates ✅ COMPLETE

3. **VertexAIClient** ✅ COMPLETE
   - Updated constructor to accept ProviderDefaultModels
   - Modified GetVertexAIModelInfo to check configuration first
   - Maintains backward compatibility with hardcoded fallbacks
   - Model aliasing now configurable via ProviderSpecificDefaults.ModelAliases

4. **All Other Providers** ✅ COMPLETE (15 providers)
   - MistralClient, GroqClient, AnthropicClient, CohereClient
   - GeminiClient, OllamaClient, ReplicateClient, FireworksClient
   - BedrockClient, HuggingFaceClient, SageMakerClient, OpenRouterClient
   - OpenAICompatibleGenericClient, UltravoxClient, AzureOpenAIClient
   - All updated to accept ProviderDefaultModels parameter
   - CustomProviderClient base class also updated

5. **LLMClientFactory** ✅ COMPLETE
   - All provider instantiations now pass defaultModels
   - Configuration properly flows to all providers

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

## Summary of Completed Work

### Phase 1: Infrastructure ✅
- Created ProviderDefaultModels configuration schema
- Updated BaseLLMClient to accept configuration
- Updated base classes (OpenAICompatibleClient, CustomProviderClient)

### Phase 2: Provider Updates ✅
- Updated all 18 provider clients to accept ProviderDefaultModels
- OpenAIClient: Full implementation with helper methods
- ElevenLabsClient: Full implementation with helper methods  
- VertexAIClient: Model aliasing now configurable
- All other providers: Constructor updates complete
- LLMClientFactory: Passes configuration to all providers

### Phase 3: Translator Updates
- Translators receive configured defaults through providers
- Hardcoded values serve as ultimate fallbacks only

## Next Steps

1. Write comprehensive tests for configuration system
2. Update documentation with configuration examples
3. Create migration guide for users
4. Consider adding more provider-specific defaults to configuration schema

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