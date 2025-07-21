# Final Billing Architecture Recommendations

After analyzing 10+ provider pricing models, here's what we need to support and what we should skip.

## Executive Summary

**Good news**: We can support 80% of providers without major changes.

**Reality check**: Some providers have fundamentally different billing models that would require significant rework.

## What We Found

### Standard Patterns (Easy) ✅
- Token-based pricing (OpenAI, Anthropic, Groq)
- Per-image/audio/video pricing
- Quality multipliers (HD, resolution)
- Batch discounts (40-50% off)

### Moderate Complexity 🟡
- **Prompt caching** (Anthropic, Gemini) - different rates for cached tokens
- **Context tiers** (MiniMax) - price changes at 200K tokens
- **Search units** (Cohere) - 1 search = 1 query + 100 docs
- **Inference steps** (Fireworks) - per-step not per-image

### High Complexity 🔴
- **Subscriptions** (Cerebras) - $1,500/month with quotas
- **Hardware billing** (Replicate) - $0.000225/second GPU time
- **Storage fees** (Gemini) - $1/hour per 1M cached tokens
- **Multi-modal pricing** (Gemini) - audio costs 3.3x more than text

## Recommended Implementation Path

### Phase 1: Quick Wins (1 week)
Support these providers fully:
- ✅ OpenAI
- ✅ Anthropic (basic, no caching)
- ✅ Groq
- ✅ MiniMax (create two model entries for context tiers)

**Changes needed**:
```csharp
// Add to ModelCost entity
public string? ImageQualityMultipliers { get; set; }
public decimal? BatchProcessingMultiplier { get; set; }
```

### Phase 2: Common Extensions (2-3 weeks)
Add support for:
- ✅ Anthropic with prompt caching
- ✅ Cohere (search units)
- ✅ Fireworks (step-based pricing)
- ✅ Gemini (basic, no caching tiers)

**Changes needed**:
```csharp
// Add to ModelCost
public decimal? CachedInputCost { get; set; }
public decimal? CostPerSearchUnit { get; set; }
public decimal? CostPerInferenceStep { get; set; }

// Add to Usage
public int? CachedTokens { get; set; }
public int? SearchUnits { get; set; }
public int? InferenceSteps { get; set; }
```

### Phase 3: Defer Complex Cases
**Skip these for now**:
- ❌ Cerebras (subscription model - fundamentally different)
- ❌ Replicate hardware billing (use token-based models only)
- ❌ Gemini cache storage fees (too complex for v1)
- ❌ SambaNova (no public pricing)

## Critical Architecture Decisions

### 1. **Don't Over-Engineer**
The current architecture handles token-based pricing well. Don't break it trying to support edge cases.

### 2. **Use Provider Workarounds**
- **MiniMax context tiers**: Create two model entries (minimax-m1, minimax-m1-large)
- **Replicate**: Only support their token-based models, not hardware
- **Gemini caching**: Use simple average pricing, skip storage fees

### 3. **Add Fields Conservatively**
Only add fields we'll actually use:
```csharp
// YES - Clear use cases
public decimal? CachedInputCost { get; set; }
public decimal? BatchProcessingMultiplier { get; set; }

// NO - Too complex for v1
public string? SubscriptionTiers { get; set; }
public decimal? HardwareSecondsRate { get; set; }
```

## Migration Script for Phase 1

```sql
-- Simple additions that don't break anything
ALTER TABLE ModelCosts ADD COLUMN BatchProcessingMultiplier DECIMAL(18,4) NULL;
ALTER TABLE ModelCosts ADD COLUMN ImageQualityMultipliers TEXT NULL;

-- Phase 2 additions
ALTER TABLE ModelCosts ADD COLUMN CachedInputCost DECIMAL(18,8) NULL;
ALTER TABLE ModelCosts ADD COLUMN CostPerSearchUnit DECIMAL(18,8) NULL;
ALTER TABLE ModelCosts ADD COLUMN CostPerInferenceStep DECIMAL(18,8) NULL;
```

## What This Gets Us

### Supported at Launch
- ✅ OpenAI (100%)
- ✅ Anthropic (95% - basic caching)
- ✅ MiniMax (100% with workaround)
- ✅ Groq (100%)
- ✅ Cohere (90% - search units)
- ✅ Fireworks (90% - step pricing)
- ✅ Gemini (70% - basic pricing only)

### Not Supported
- ❌ Cerebras (subscription)
- ❌ Replicate (hardware billing)
- ❌ Complex caching tiers
- ❌ Time-based storage fees

## Implementation Priority

1. **Week 1**: Phase 1 changes + CSV imports for basic providers
2. **Week 2**: Phase 2 changes + test caching/search units
3. **Week 3**: Polish, documentation, edge cases

## Key Insight

**We don't need to support every pricing model to be successful.** 

Focus on:
1. Providers with standard token-based pricing
2. Common patterns (batching, caching, quality tiers)
3. Clean implementation over complex edge cases

The architecture is solid. Small extensions will cover most use cases without compromising the clean design.