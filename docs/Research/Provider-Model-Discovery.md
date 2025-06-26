# Provider Model Discovery and Metadata API Research

This document provides comprehensive research on model discovery and metadata APIs for all LLM providers supported by Conduit, based on codebase analysis and API documentation research.

## Supported Providers in Conduit

Based on the LLMClientFactory analysis, Conduit supports the following providers:

### Primary Providers
- **OpenAI** (`openai`)
- **Anthropic** (`anthropic`) 
- **Google/Gemini** (`google`, `gemini`)
- **MiniMax** (`minimax`)
- **Replicate** (`replicate`)
- **Mistral** (`mistral`, `mistralai`)
- **Cohere** (`cohere`)
- **OpenRouter** (`openrouter`)

### Additional Providers
- **Azure OpenAI** (`azure`)
- **Vertex AI** (`vertexai`)
- **Groq** (`groq`)
- **Fireworks** (`fireworks`, `fireworksai`)
- **Bedrock** (`bedrock`)
- **HuggingFace** (`huggingface`)
- **SageMaker** (`sagemaker`)
- **Ollama** (`ollama`)
- **OpenAI Compatible** (`openai-compatible`)
- **Ultravox** (`ultravox`)
- **ElevenLabs** (`elevenlabs`)
- **Google Cloud Audio** (`googlecloud`, `gcp`)
- **AWS Transcribe** (`aws`, `awstranscribe`)

## Current Model Discovery Implementation

Conduit currently implements model discovery through:

1. **ProviderDiscoveryService** - Central service for discovering provider capabilities
2. **Pattern-based inference** - Uses `KnownModelPatterns` dictionary to infer capabilities from model names
3. **Fallback models** - Predefined lists of known models when API discovery fails
4. **Caching** - 24-hour cache for discovered capabilities
5. **Event-driven updates** - ModelCapabilitiesDiscovered events for cache invalidation

### Current Discovery Methods

Most providers inherit from `OpenAICompatibleClient` which implements:
- `GetModelsAsync()` - Attempts to call provider's `/models` endpoint
- Falls back to `ProviderFallbackModels` static lists if API call fails
- Throws `NotSupportedException` for providers without model listing support

## Provider-Specific Research

### 1. OpenAI

**Discovery Endpoint:** `GET https://api.openai.com/v1/models`

**Authentication:** Bearer token in Authorization header
```bash
curl https://api.openai.com/v1/models \
  -H "Authorization: Bearer $OPENAI_API_KEY"
```

**Available Metadata:**
- `id` - Model identifier
- `object` - Always "model"
- `created` - Unix timestamp
- `owned_by` - Organization that owns the model

**Limitations:**
- Limited metadata (no capabilities, context window, pricing)
- Model list filtered by API key access level
- No detailed model specifications

**Current Usage in Conduit:**
- ✅ Implemented via OpenAICompatibleClient
- ✅ Has fallback models defined
- ✅ Pattern-based capability inference works well

**Reliability:** ⭐⭐⭐⭐ (High - stable endpoint, reliable access)

---

### 2. Anthropic Claude

**Discovery Endpoint:** `GET https://api.anthropic.com/v1/models`

**Authentication:** Custom headers required
```bash
curl https://api.anthropic.com/v1/models \
  --header "x-api-key: $ANTHROPIC_API_KEY" \
  --header "anthropic-version: 2023-06-01"
```

**Available Metadata:**
- `id` - Model identifier  
- `display_name` - Human-readable model name
- `type` - Always "model"
- `created_at` - ISO 8601 timestamp
- Pagination fields (`first_id`, `has_more`, `last_id`)

**Limitations:**
- No capability or context window information
- Requires specific API version header
- Different authentication format than OpenAI

**Current Usage in Conduit:**
- ❌ Uses AnthropicClient (custom implementation)
- ❌ No model listing implementation found
- ✅ Has fallback models and patterns defined

**Reliability:** ⭐⭐⭐⭐ (High - new stable endpoint, good metadata)

---

### 3. Google Gemini

**Discovery Endpoint:** Uses Google's Discovery Document system
- Discovery Doc: `https://cloudaicompanion.googleapis.com/$discovery/rest?version=v1`
- Tutorial: `https://ai.google.dev/gemini-api/docs/get-started/tutorial?lang=rest`

**Authentication:** API key in URL parameter or Authorization header

**Available Metadata:**
- Full OpenAPI schema through Discovery Documents
- Model parameters and capabilities
- Request/response schemas

**Limitations:**
- Complex discovery system (not simple REST endpoint)
- Requires Google API client libraries for best experience
- Regional availability variations

**Current Usage in Conduit:**
- ✅ Implemented via GeminiClient (custom implementation)
- ❌ No model listing implementation found
- ✅ Has fallback models and patterns defined

**Reliability:** ⭐⭐⭐ (Medium - complex discovery system, good once implemented)

---

### 4. MiniMax

**Discovery Endpoint:** No standard models endpoint documented

**Authentication:** Bearer token
```bash
Authorization: Bearer <YOUR_API_KEY>
```

**Available Metadata:**
- Limited public documentation
- API host varies by region (`https://api.minimax.io` or `https://api.minimaxi.com`)

**Limitations:**
- No public models listing endpoint
- Region-specific API keys and hosts
- Limited English documentation

**Current Usage in Conduit:**
- ✅ Implemented via MiniMaxClient (custom implementation)  
- ❌ No model listing implementation found
- ✅ Has patterns defined for known models (abab6.5-chat, image-01, video-01)

**Reliability:** ⭐⭐ (Low - no discovery endpoint, limited docs)

---

### 5. Replicate

**Discovery Endpoint:** `QUERY https://api.replicate.com/v1/models`

**Authentication:** Bearer token
```bash
curl -s -X QUERY \
  -H "Authorization: Bearer $REPLICATE_API_TOKEN" \
  -H "Content-Type: text/plain" \
  -d "search_query" \
  https://api.replicate.com/v1/models
```

**Available Metadata:**
- `url` - Model URL on Replicate
- `owner` - Model owner
- `name` - Model name
- `description` - Model description
- `visibility` - Public/private status
- `github_url` - Associated repository
- `paper_url` - Research paper
- `license_url` - License information
- `run_count` - Usage statistics
- `cover_image_url` - Model image
- `default_example` - Example usage
- `latest_version` - Version info
- Full OpenAPI schema via individual model endpoints

**Limitations:**
- Uses non-standard QUERY HTTP method
- Search-based rather than simple listing
- Thousands of community models (overwhelming)

**Current Usage in Conduit:**
- ✅ Implemented via ReplicateClient (custom implementation)
- ❌ No model listing implementation found
- ✅ Has patterns for image generation models

**Reliability:** ⭐⭐⭐⭐ (High - rich metadata, comprehensive API)

---

### 6. Mistral

**Discovery Endpoint:** `GET https://api.mistral.ai/v1/models`

**Authentication:** Bearer token
```bash
curl https://api.mistral.ai/v1/models \
  -H "Authorization: Bearer $MISTRAL_API_KEY"
```

**Available Metadata:**
- Model identifiers and basic info
- Model lifecycle information (deprecation/retirement dates)
- Rate limiting and pricing tiers

**Limitations:**
- Limited capability metadata
- Requires payment activation for API access
- Model lifecycle complexity

**Current Usage in Conduit:**
- ✅ Implemented via MistralClient (extends OpenAICompatibleClient)
- ✅ Should have model listing via parent class
- ✅ Has fallback models defined

**Reliability:** ⭐⭐⭐⭐ (High - standard OpenAI-compatible endpoint)

---

### 7. Cohere

**Discovery Endpoint:** `GET https://api.cohere.ai/v1/models` (inferred from docs)

**Authentication:** Bearer token
```bash
Authorization: BEARER [API_KEY]
```

**Available Metadata:**
- Model families (Command, Aya, Rerank, Embed)
- Endpoint compatibility filtering
- Language support information
- Rate limiting tiers

**Limitations:**
- Different rate limits for trial vs production keys
- Endpoint-specific model filtering needed
- Limited trial usage (1,000 calls/month)

**Current Usage in Conduit:**
- ✅ Implemented via CohereClient (custom implementation)
- ❌ No model listing implementation found
- ❌ No fallback models defined

**Reliability:** ⭐⭐⭐ (Medium - good API, needs implementation)

---

### 8. OpenRouter

**Discovery Endpoint:** `GET https://openrouter.ai/api/v1/models`

**Authentication:** Bearer token
```bash
curl https://openrouter.ai/api/v1/models \
  -H "Authorization: Bearer $OPENROUTER_API_KEY"
```

**Available Metadata:**
- Comprehensive model metadata (best of all providers)
- `supported_parameters` - Union of all provider parameters
- Pricing information (USD per token/request)
- Provider information for each model
- Context windows and capabilities
- Real-time availability status

**Limitations:**
- Meta-provider (routes to other providers)
- Parameter support varies by underlying provider
- Model availability can change

**Current Usage in Conduit:**
- ✅ Implemented via OpenRouterClient (extends OpenAICompatibleClient)
- ✅ Should have model listing via parent class
- ✅ Has extensive fallback models with provider prefixes
- ✅ Has comprehensive pattern matching for provider/model format

**Reliability:** ⭐⭐⭐⭐⭐ (Excellent - best metadata, most reliable discovery)

---

## Provider Discovery API Summary

| Provider | Discovery Endpoint | Auth Method | Metadata Quality | Reliability | Current Status |
|----------|-------------------|-------------|------------------|-------------|----------------|
| OpenAI | `/v1/models` | Bearer | ⭐⭐ | ⭐⭐⭐⭐ | ✅ Implemented |
| Anthropic | `/v1/models` | Custom headers | ⭐⭐⭐ | ⭐⭐⭐⭐ | ❌ Not implemented |
| Google | Discovery Docs | API key | ⭐⭐⭐⭐ | ⭐⭐⭐ | ❌ Not implemented |
| MiniMax | None | Bearer | ⭐ | ⭐⭐ | ❌ No endpoint |
| Replicate | `/v1/models` (QUERY) | Bearer | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ❌ Not implemented |
| Mistral | `/v1/models` | Bearer | ⭐⭐⭐ | ⭐⭐⭐⭐ | ✅ Implemented |
| Cohere | `/v1/models` | Bearer | ⭐⭐⭐ | ⭐⭐⭐ | ❌ Not implemented |
| OpenRouter | `/v1/models` | Bearer | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ✅ Implemented |

## Recommendations for Implementation

### High Priority (Reliable APIs with Good Metadata)

1. **Anthropic** - Implement custom ListModelsAsync in AnthropicClient
   - Stable endpoint with good metadata
   - Need to handle custom authentication headers
   
2. **Replicate** - Implement search-based discovery in ReplicateClient  
   - Excellent metadata including schemas
   - Handle non-standard QUERY method and search filtering

3. **Cohere** - Implement model listing in CohereClient
   - Good API with endpoint-specific filtering
   - Add fallback models to ProviderFallbackModels

### Medium Priority (Good APIs, Implementation Complexity)

4. **Google Gemini** - Implement Discovery Document parsing
   - Excellent metadata but complex implementation
   - Consider using Google's client libraries
   - May require significant development effort

### Low Priority (Limited Discovery Value)

5. **MiniMax** - Keep current pattern-based approach
   - No discovery endpoint available
   - Current patterns work for known models
   - Monitor for future API updates

### Implementation Approach

1. **Extend existing clients** with provider-specific ListModelsAsync implementations
2. **Update ProviderDiscoveryService** to handle custom authentication requirements
3. **Add comprehensive fallback models** for providers without discovery
4. **Enhance caching strategy** to handle different refresh rates per provider
5. **Add metadata enrichment** by combining discovery data with known patterns

### Code Changes Required

1. **Anthropic**: Override ListModelsAsync in AnthropicClient with custom headers
2. **Replicate**: Override ListModelsAsync in ReplicateClient with QUERY method
3. **Cohere**: Override ListModelsAsync in CohereClient
4. **Google**: Consider Discovery Document integration or manual model list
5. **Update fallback lists** with latest models from each provider
6. **Add error handling** for provider-specific authentication issues

## Conclusion

OpenRouter provides the best model discovery experience with comprehensive metadata, while OpenAI and Mistral offer reliable but basic discovery. Anthropic's new models endpoint looks promising for implementation. Replicate offers excellent metadata but requires custom HTTP method handling. The current pattern-based approach works well as a fallback and should be maintained for reliability.

The recommended approach is to implement discovery for high-reliability providers first (Anthropic, Replicate, Cohere) while maintaining robust fallback mechanisms for all providers.