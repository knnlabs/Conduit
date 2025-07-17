# Provider Parameter Mapping Matrix

This document shows which parameters from ChatCompletionRequest are currently mapped by each provider client implementation in ConduitLLM.

## Parameter Support Matrix

| Provider | Temperature | MaxTokens | TopP | N | Stream | Stop | User | PresencePenalty | FrequencyPenalty | LogitBias | Seed | TopK |
|----------|------------|-----------|------|---|--------|------|------|-----------------|------------------|-----------|------|------|
| **OpenAICompatibleClient** (base) | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **OpenAI** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Anthropic** | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ (StopSequences) | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| **Gemini** | ✅ | ✅ | ✅ | ✅ (CandidateCount) | ❌ | ✅ (StopSequences) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ (commented) |
| **Cohere** | ✅ | ✅ | ✅ (P) | ❌ | ✅ | ✅ (StopSequences) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Replicate** | ✅ | ✅ (max_length) | ✅ | ❌ | ❌ | ✅ (stop_sequences) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **MiniMax** | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Bedrock (Claude)** | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Bedrock (Llama)** | ✅ | ✅ (MaxGenLen) | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Bedrock (Titan)** | ✅ | ✅ (MaxTokenCount) | ✅ | ❌ | ❌ | ✅ (StopSequences) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Bedrock (Cohere)** | ✅ | ✅ | ✅ (P) | ❌ | ❌ | ✅ (StopSequences) | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ (K) |
| **Bedrock (AI21)** | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ (StopSequences) | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Mistral** | Inherits from OpenAICompatibleClient | | | | | | | | | | | |
| **Groq** | Inherits from OpenAICompatibleClient | | | | | | | | | | | |

## Key Findings

### Parameters Commonly Supported
1. **Temperature** - Supported by all providers
2. **MaxTokens** - Supported by all providers (sometimes with different names)
3. **TopP** - Supported by most providers (except base OpenAICompatibleClient)
4. **Stream** - Supported by most providers that offer streaming

### Parameters Rarely Supported
1. **N** (number of completions) - Only OpenAI and Gemini
2. **User** - Only OpenAI
3. **PresencePenalty** - Only OpenAI and Bedrock AI21
4. **FrequencyPenalty** - Only OpenAI and Bedrock AI21
5. **LogitBias** - Only OpenAI
6. **Seed** - Only OpenAI
7. **TopK** - Only Anthropic and Bedrock Cohere

### Implementation Notes

1. **OpenAICompatibleClient Base Class**: The base implementation in `MapToOpenAIRequest` only maps:
   - Model
   - Messages
   - MaxTokens
   - Temperature
   - Tools/ToolChoice
   - ResponseFormat
   - Stream

2. **Missing Parameters in Base**: The OpenAI models (`OpenAIModels.cs`) support many more parameters that aren't being mapped:
   - TopP
   - N
   - Stop
   - PresencePenalty
   - FrequencyPenalty
   - LogitBias
   - User
   - Seed

3. **Provider-Specific Naming**:
   - Anthropic uses `StopSequences` instead of `Stop`
   - Cohere uses `P` instead of `TopP`
   - Replicate uses `max_length` instead of `MaxTokens`
   - Bedrock varies by model (MaxGenLen, MaxTokenCount, etc.)

## Recommendations

1. **Update OpenAICompatibleClient**: The base class should map all parameters that OpenAI supports, since many providers using this base class (Mistral, Groq, etc.) likely support these parameters.

2. **Add Parameter Mapping Configuration**: Consider adding a configuration system that allows providers to specify which parameters they support and their naming conventions.

3. **Document Provider Capabilities**: Each provider should document which parameters they support in their class documentation.

4. **Standardize Stop Sequences**: Most providers support stop sequences but with different property names. Consider standardizing this in the mapping layer.