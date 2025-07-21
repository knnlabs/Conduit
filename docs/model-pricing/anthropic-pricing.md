# Anthropic Model Pricing

**Last Updated**: 2025-01-19  
**Source**: Anthropic Pricing Page

## Latest Models

### Claude Opus 4
**Most intelligent model for complex tasks**

| Pricing Type | Input | Output |
|-------------|-------|--------|
| Standard | $15 / MTok | $75 / MTok |
| Prompt Caching - Write | $18.75 / MTok | - |
| Prompt Caching - Read | $1.50 / MTok | - |

### Claude Sonnet 4
**Optimal balance of intelligence, cost, and speed**

| Pricing Type | Input | Output |
|-------------|-------|--------|
| Standard | $3 / MTok | $15 / MTok |
| Prompt Caching - Write | $3.75 / MTok | - |
| Prompt Caching - Read | $0.30 / MTok | - |

### Claude Haiku 3.5
**Fastest, most cost-effective model**

| Pricing Type | Input | Output |
|-------------|-------|--------|
| Standard | $0.80 / MTok | $4 / MTok |
| Prompt Caching - Write | $1 / MTok | - |
| Prompt Caching - Read | $0.08 / MTok | - |

## Legacy Models

### Claude Opus 3

| Pricing Type | Input | Output |
|-------------|-------|--------|
| Standard | $15 / MTok | $75 / MTok |
| Prompt Caching - Write | $18.75 / MTok | - |
| Prompt Caching - Read | $1.50 / MTok | - |

### Claude Sonnet 3.7

| Pricing Type | Input | Output |
|-------------|-------|--------|
| Standard | $3 / MTok | $15 / MTok |
| Prompt Caching - Write | $3.75 / MTok | - |
| Prompt Caching - Read | $0.30 / MTok | - |

### Claude Haiku 3

| Pricing Type | Input | Output |
|-------------|-------|--------|
| Standard | $0.25 / MTok | $1.25 / MTok |
| Prompt Caching - Write | $0.30 / MTok | - |
| Prompt Caching - Read | $0.03 / MTok | - |

## Notes

1. **Pricing Currency**: All prices are in USD
2. **MTok**: Million tokens (1M tokens)
3. **Token Counting**: Varies by language; approximately 750 words per 1,000 tokens
4. **Batch Processing**: Save 50% with batch processing (not included in this document)
5. **Prompt Caching**: Reflects 5-minute TTL; extended prompt caching available
6. **Context Windows**: 
   - Claude 3 models: 200K tokens
   - Claude 3.5 Haiku: 200K tokens
   - Claude 4 models: Context window information varies by model

## CSV Import Format

When importing to Conduit, use the following CSV columns:
- Model Pattern (e.g., "claude-4-opus", "claude-3.5-haiku")
- Provider (always "Anthropic")
- Model Type (chat)
- Input Cost (per 1K tokens) - divide the per-1M price by 1000
- Output Cost (per 1K tokens) - divide the per-1M price by 1000
- Priority (typically 10)
- Active (Yes/No)
- Description

Note: Prompt caching costs are not included in the standard CSV import as they require special handling.