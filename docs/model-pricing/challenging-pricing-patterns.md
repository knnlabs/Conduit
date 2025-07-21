# Challenging Pricing Patterns Summary

**Last Updated**: 2025-07-19

## Overview

This document summarizes the most challenging pricing patterns found across various AI model providers that would require architectural changes to Conduit's pricing system.

## 1. Subscription-Based Tiers with Token Quotas (Cerebras)

### Challenge
Cerebras offers a "Growth Tier" with fixed monthly subscription fees that include:
- Token quotas per minute and per day
- Request rate limits
- Different pricing tiers ($1,500 to $10,000/month)
- No per-token pricing in this tier

### Example
- Tier 1: $1,500/month = 300k input/30k output tokens per minute, 70M tokens per day
- Tier 5: $10,000/month = 1.45M input/145k output tokens per minute, 680M tokens per day

### Architectural Impact
- Need to track monthly subscription status
- Track usage against quotas (minute/daily limits)
- Different billing model than pay-per-use
- Requires quota management and enforcement

## 2. Search Unit Pricing (Cohere)

### Challenge
Cohere's Rerank model uses "search units" instead of tokens:
- $2.00 per 1K searches
- 1 search unit = 1 query with up to 100 documents
- Documents >500 tokens are split into chunks, each counting as a document

### Architectural Impact
- Need new unit type beyond tokens/images/audio
- Complex calculation logic for document chunking
- Different metering system required

## 3. Time-Based Hardware Pricing (Replicate)

### Challenge
Replicate uses two completely different pricing models:
1. **Hardware-based**: Pay per second of compute time
   - CPU: $0.000025-$0.0001/second
   - GPU: $0.000225-$0.01120/second (varies by GPU type)
2. **Output-based**: Pay per generated unit
   - Images: $0.04/image or $3.00/1000 images
   - Videos: $0.25-$0.50/second of video

### Architectural Impact
- Need to support both time-based and output-based billing
- Hardware tier selection affects pricing
- Must track compute time, not just API calls
- Different cost calculation for same model based on hardware

## 4. Context Caching with Storage Costs (Gemini)

### Challenge
Gemini offers context caching with complex pricing:
- **Read costs**: 25-75% discount on input tokens
- **Storage costs**: $1.00-$4.50 per 1M tokens per hour
- Different rates based on context size thresholds

### Example
- Gemini 2.5 Pro standard: $1.25/1M tokens
- With caching read: $0.31/1M tokens
- Plus storage: $4.50/1M tokens per hour

### Architectural Impact
- Need to track cached context separately
- Time-based storage billing (hourly)
- Multiple pricing tiers for same model
- Cache management and expiration

## 5. Multi-Modal Differential Pricing (Gemini)

### Challenge
Gemini prices different input types differently for the same model:
- Text/image/video: $0.30/1M tokens
- Audio: $1.00/1M tokens
- Live API text: $0.50/1M tokens
- Live API audio/video: $3.00/1M tokens

### Architectural Impact
- Need to track input type, not just tokens
- Different pricing for same model based on modality
- API type (standard vs live) affects pricing

## 6. Step-Based Image Generation (Fireworks)

### Challenge
Fireworks charges by inference steps for most image models:
- Non-Flux models: $0.00013 per step
- FLUX.1[dev]: $0.0005 per step
- FLUX.1[schnell]: $0.00035 per step
- But Flux Kontext models: flat rate per image

### Architectural Impact
- Need to track number of inference steps
- Different billing units for different models in same category
- Mixed pricing models within image generation

## 7. Batch Processing Discounts (Multiple Providers)

### Challenge
- **Fireworks**: 40% discount for batch API
- **Gemini**: 50% discount for batch mode
- **Groq**: 50% discount with different SLAs

### Architectural Impact
- Need to differentiate batch vs real-time requests
- Different pricing for same model/tokens
- SLA tracking for batch processing

## 8. Free Tier Quotas (Multiple Providers)

### Challenge
Several providers offer free quotas before charging:
- **Gemini**: 500 RPD free for grounding
- **Groq**: Unspecified free tier
- **SambaNova**: Free tier for testing

### Architectural Impact
- Need quota tracking per account
- Transition from free to paid usage
- Different rate limits for free vs paid

## 9. Character-Based Pricing (Groq TTS)

### Challenge
Groq's PlayAI Dialog TTS model charges per character, not token:
- $50.00 per 1M characters
- Different unit than all other text models

### Architectural Impact
- Need character counting for TTS
- Different metering for same provider

## 10. Opaque Pricing (SambaNova)

### Challenge
SambaNova doesn't publish pricing publicly:
- Requires platform signup to view prices
- May have custom pricing per customer
- No standard rate card

### Architectural Impact
- Need ability to handle custom/negotiated pricing
- Per-customer pricing overrides
- Dynamic pricing updates

## Recommendations for Architecture Changes

1. **Flexible Unit System**: Support tokens, characters, images, videos, seconds, steps, search units
2. **Multi-Tier Pricing**: Support subscription tiers with quotas alongside pay-per-use
3. **Time-Based Billing**: Add support for hourly storage costs and compute time
4. **Quota Management**: Track and enforce free tier quotas and subscription limits
5. **Batch vs Real-Time**: Different pricing paths for same model
6. **Multi-Modal Awareness**: Track input type for differential pricing
7. **Context Management**: Support for cached context with separate pricing
8. **Custom Pricing**: Override mechanism for negotiated rates