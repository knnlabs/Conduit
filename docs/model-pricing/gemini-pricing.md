# Google Gemini Model Pricing

**Last Updated**: 2025-07-17  
**Source**: Gemini Developer API Pricing Documentation

## Text Models

### Gemini 2.5 Pro
**State-of-the-art multipurpose model, excels at coding and complex reasoning tasks**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard (<= 200k tokens) | $1.25 | $10.00 |
| Standard (> 200k tokens) | $2.50 | $15.00 |
| Context Caching - Read (<= 200k) | $0.31 | - |
| Context Caching - Read (> 200k) | $0.625 | - |
| Context Caching - Storage | $4.50 / 1M tokens per hour | - |

### Gemini 2.5 Flash
**First hybrid reasoning model with 1M token context window and thinking budgets**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard (text/image/video) | $0.30 | $2.50 |
| Standard (audio) | $1.00 | - |
| Context Caching - Read (text/image/video) | $0.075 | - |
| Context Caching - Read (audio) | $0.25 | - |
| Context Caching - Storage | $1.00 / 1M tokens per hour | - |
| Live API (text) | $0.50 | $2.00 |
| Live API (audio/image/video) | $3.00 | $12.00 |

### Gemini 2.5 Flash-Lite Preview
**Smallest and most cost effective model, built for at scale usage**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard (text/image/video) | $0.10 | $0.40 |
| Standard (audio) | $0.50 | - |
| Context Caching - Read (text/image/video) | $0.025 | - |
| Context Caching - Read (audio) | $0.125 | - |
| Context Caching - Storage | $1.00 / 1M tokens per hour | - |

### Gemini 2.5 Flash Native Audio
**Native audio models optimized for higher quality audio outputs**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard (text) | $0.50 | $2.00 |
| Standard (audio/video) | $3.00 | $12.00 |

### Gemini 2.0 Flash
**Most balanced multimodal model with great performance across all tasks**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard (text/image/video) | $0.10 | $0.40 |
| Standard (audio) | $0.70 | - |
| Context Caching - Read (text/image/video) | $0.025 / 1M tokens | - |
| Context Caching - Read (audio) | $0.175 / 1M tokens | - |
| Context Caching - Storage | $1.00 / 1M tokens per hour | - |
| Live API (text) | $0.35 | $1.50 |
| Live API (audio/image/video) | $2.10 | $8.50 |

### Gemini 2.0 Flash-Lite
**Smallest and most cost effective model, built for at scale usage**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard | $0.075 | $0.30 |

### Gemini 1.5 Flash
**Fastest multimodal model with 1M token context window**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard (<= 128k tokens) | $0.075 | $0.30 |
| Standard (> 128k tokens) | $0.15 | $0.60 |
| Context Caching - Read (<= 128k) | $0.01875 | - |
| Context Caching - Read (> 128k) | $0.0375 | - |
| Context Caching - Storage | $1.00 per hour | - |

### Gemini 1.5 Flash-8B
**Smallest model for lower intelligence use cases**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard (<= 128k tokens) | $0.0375 | $0.15 |
| Standard (> 128k tokens) | $0.075 | $0.30 |
| Context Caching - Read (<= 128k) | $0.01 | - |
| Context Caching - Read (> 128k) | $0.02 | - |
| Context Caching - Storage | $0.25 per hour | - |

### Gemini 1.5 Pro
**Highest intelligence Gemini 1.5 series model with 2M token context window**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard (<= 128k tokens) | $1.25 | $5.00 |
| Standard (> 128k tokens) | $2.50 | $10.00 |
| Context Caching - Read (<= 128k) | $0.3125 | - |
| Context Caching - Read (> 128k) | $0.625 | - |
| Context Caching - Storage | $4.50 per hour | - |

## Audio Models

### Gemini 2.5 Flash Preview TTS
**Text-to-speech audio model optimized for price-performance**

| Model | Use Case | Input Cost | Output Cost |
|-------|----------|------------|-------------|
| Gemini 2.5 Flash Preview TTS | Text-to-Speech | $0.50 per 1M tokens (text) | $10.00 per 1M tokens (audio) |

### Gemini 2.5 Pro Preview TTS
**Text-to-speech audio model optimized for powerful, low-latency speech generation**

| Model | Use Case | Input Cost | Output Cost |
|-------|----------|------------|-------------|
| Gemini 2.5 Pro Preview TTS | Text-to-Speech | $1.00 per 1M tokens (text) | $20.00 per 1M tokens (audio) |

## Image Generation

### Imagen 4 Preview
**Latest image generation model with better text rendering and overall quality**

| Quality | Cost per Image |
|---------|----------------|
| Standard | $0.04 |
| Ultra | $0.06 |

### Imagen 3
**State-of-the-art image generation model**

| Model | Cost per Image |
|-------|----------------|
| Imagen 3 | $0.03 |

## Video Generation

### Veo 3 Preview
**Latest video generation model**

| Model | Cost per Second |
|-------|-----------------|
| Video with audio (default) | $0.75 |
| Video without audio | $0.50 |

### Veo 2
**State-of-the-art video generation model**

| Model | Cost per Second |
|-------|-----------------|
| Veo 2 | $0.35 |

## Embeddings

### Gemini Embedding
**Newest embeddings model with higher rate limits**

| Model | Cost (per 1M tokens) |
|-------|---------------------|
| Gemini Embedding | $0.15 |

## Open Models

### Gemma 3
**Lightweight, state-of-the-art, open model**

| Model | Input | Output |
|-------|-------|--------|
| Gemma 3 | Free of charge | Not available on paid tier |

### Gemma 3n
**Open model built for efficient performance on everyday devices**

| Model | Input | Output |
|-------|-------|--------|
| Gemma 3n | Free of charge | Not available on paid tier |

## Additional Features

### Grounding with Google Search
- Free tier: Free of charge, up to 500 RPD (limit shared with Flash-Lite RPD)
- Paid tier: 1,500 RPD (free), then $35 / 1,000 requests

### Batch Mode
Batch Mode is designed to process large volumes of requests asynchronously. Requests submitted using this mode is 50% of the price of interactive (non-batch mode) requests.

## Notes

1. **Pricing Currency**: All prices are in USD
2. **Token Counting**: Varies by language and model
3. **Context Windows**: 
   - Gemini 2.5 models support up to 1M+ tokens
   - Gemini 1.5 Flash: 1M tokens
   - Gemini 1.5 Pro: 2M tokens
4. **Free Tier**: Available through the API service with lower rate limits
5. **Context Caching**: Available for select models to reduce costs for repeated context
6. **Preview Models**: May change before becoming stable and have more restrictive rate limits
7. **Dynamic Retrieval**: Only requests containing at least one grounding support URL are charged
8. **Image Generation**: Output images up to 1024x1024px consume 1290 tokens (~$0.039 per image)

## CSV Import Format

When importing to Conduit, use the following CSV columns:
- Model Pattern (e.g., "gemini-2.5-pro", "gemini-2.5-flash")
- Provider (always "Google")
- Model Type (chat/embedding/image/video/audio)
- Input Cost (per 1K tokens) - divide the per-1M price by 1000
- Output Cost (per 1K tokens) - divide the per-1M price by 1000
- Embedding Cost (per 1K tokens) - for embedding models
- Image Cost (per image) - for image generation models
- Video Cost (per second) - for video generation models
- Audio Cost (per minute) - for TTS models
- Priority (typically 10)
- Active (Yes/No)
- Description

Note: Context caching and grounding costs are not included in the standard CSV import as they require special handling.