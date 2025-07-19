# Comprehensive Pricing Patterns Analysis

Based on analysis of 10+ provider pricing models. This document identifies patterns that challenge our current architecture.

## Current Architecture Assumptions

Our billing system assumes:
- **Unit**: Tokens, images, audio minutes, or video seconds
- **Pricing**: Fixed rate per unit (with optional multipliers)
- **Billing**: Pay-as-you-go per request
- **Calculation**: Post-request based on usage

## New Pricing Patterns Found

### 1. **Subscription Models** (Cerebras)
```
$1,500/month = 19.6M tokens/minute limit
$10,000/month = 118M tokens/minute limit
```
**Challenge**: Fixed monthly cost with usage quotas, not per-token billing

### 2. **Non-Token Units** 
- **Search Units** (Cohere): 1 search = 1 query + 100 documents
- **Inference Steps** (Fireworks): $0.0005 per step, not per image
- **Characters** (Multiple TTS): Per 1M characters, not tokens
- **Hardware Time** (Replicate): $0.000225/second GPU time

### 3. **Multi-Tier Pricing Within Same Model**
```
Gemini 1.5 Pro:
- Standard: $1.25/1M tokens
- Context Cache (<60 min): $0.3125/1M tokens + $1.00/hour storage
- Context Cache (>60 min): $0.15625/1M tokens + $1.00/hour storage
```

### 4. **Input Type Affects Pricing** (Gemini)
Same model, different costs:
- Text input: $0.30/1M tokens
- Audio input: $1.00/1M tokens (3.3x more)
- Live API: $2.50/1M tokens (8.3x more)

### 5. **Time-Based Components**
- **Storage Fees**: $1-4.50/1M tokens/hour (Gemini caching)
- **Hardware Billing**: Per-second GPU usage (Replicate)
- **Batch Windows**: 24-hour to 7-day processing affects price

### 6. **Complex Discount Structures**
- Volume discounts (not currently supported)
- Free tier quotas before billing starts
- Different rates for batch vs real-time (40-50% discount)

## Pricing Complexity Matrix

| Provider | Token | Time | Hardware | Steps | Search | Subscription | Multi-Modal |
|----------|-------|------|----------|-------|--------|--------------|-------------|
| OpenAI | ✓ | - | - | - | - | - | - |
| Anthropic | ✓ | - | - | - | - | - | - |
| Gemini | ✓ | ✓ | - | - | - | - | ✓ |
| Replicate | ✓ | ✓ | ✓ | - | - | - | - |
| Fireworks | ✓ | - | - | ✓ | - | - | - |
| Cohere | ✓ | - | - | - | ✓ | - | - |
| Cerebras | - | - | - | - | - | ✓ | - |
| Groq | ✓ | - | - | - | - | - | - |
| SambaNova | ? | ? | ? | ? | ? | ? | ? |

## Architecture Impact Assessment

### Can Handle Now ✅
1. **Basic multipliers** (quality, resolution)
2. **Batch discounts** (simple multiplier)
3. **Character-based pricing** (convert to tokens)
4. **Per-step pricing** (treat as per-image with metadata)

### Requires Moderate Changes 🟡
1. **Context caching** (add cached token fields)
2. **Multi-modal pricing** (add input type field)
3. **Search units** (new unit type)
4. **Free tier tracking** (quota management)

### Requires Major Changes 🔴
1. **Subscription billing** (completely different model)
2. **Time-based storage fees** (ongoing charges)
3. **Hardware-based billing** (per-second tracking)
4. **Dynamic pricing** (SambaNova custom rates)

## Recommended Approach

### Phase 1: Core Extensions (Support 80% of providers)
```csharp
public class ModelCost
{
    // Existing fields...
    
    // New unit types
    public decimal? CostPerSearchUnit { get; set; }
    public decimal? CostPerInferenceStep { get; set; }
    
    // Multi-modal support
    public string? InputTypeMultipliers { get; set; } // JSON
    
    // Caching support
    public decimal? CachedInputCost { get; set; }
    public decimal? CacheStorageCostPerHour { get; set; }
}
```

### Phase 2: Usage Tracking Enhancement
```csharp
public class Usage
{
    // Existing fields...
    
    // New tracking
    public int? SearchUnits { get; set; }
    public int? InferenceSteps { get; set; }
    public string? InputType { get; set; } // "text", "audio", "video"
    public decimal? HardwareSeconds { get; set; }
    public bool IsCached { get; set; }
}
```

### Phase 3: Billing Model Abstraction
```csharp
public interface IBillingModel
{
    Task<decimal> CalculateCostAsync(Usage usage, ModelCost modelCost);
}

public class TokenBasedBilling : IBillingModel { }
public class HardwareBasedBilling : IBillingModel { }
public class SubscriptionBilling : IBillingModel { }
```

## Critical Decisions Needed

### 1. **Unit System Flexibility**
Should we:
- A) Add specific fields for each unit type (search, steps, etc.)
- B) Create generic unit system with metadata
- C) Support only common units, skip edge cases

### 2. **Subscription Support**
Should we:
- A) Skip subscription providers (Cerebras)
- B) Build quota tracking system
- C) Partner with subscription providers differently

### 3. **Time-Based Billing**
Should we:
- A) Skip providers with storage fees (complex Gemini tiers)
- B) Build time-tracking infrastructure
- C) Simplify to average costs

### 4. **Hardware Billing**
Should we:
- A) Support only token-based Replicate models
- B) Build hardware time tracking
- C) Convert hardware time to equivalent tokens

## Provider Support Matrix

### Full Support Possible ✅
- OpenAI (standard token pricing)
- Anthropic (with caching extension)
- Groq (simple token pricing)
- MiniMax (with context tiers)

### Partial Support Possible 🟡
- Gemini (skip complex caching tiers)
- Cohere (needs search unit support)
- Fireworks (needs step-based pricing)
- Replicate (token models only, skip hardware)

### Complex Support 🔴
- Cerebras (subscription model)
- SambaNova (custom pricing)

## Recommendation

**Don't try to support everything at launch.** Instead:

1. **Phase 1**: Support standard patterns (80% of use cases)
   - Token-based pricing with multipliers
   - Basic batch discounts
   - Simple unit conversions

2. **Phase 2**: Add common extensions
   - Prompt caching
   - Search units
   - Input type multipliers

3. **Phase 3**: Evaluate need for complex models
   - Subscription billing
   - Hardware-based pricing
   - Time-based storage fees

This approach avoids over-engineering while covering most provider models.