# Model Pricing Documentation

This directory contains pricing information for various LLM providers integrated with Conduit.

## Available Providers

- [OpenAI](./openai-pricing.md) - GPT-4, GPT-4o, DALL-E, Whisper, and embedding models
- [Anthropic](./anthropic-pricing.md) - Claude Opus, Sonnet, and Haiku models
- [MiniMax](./minimax-pricing.md) - Chat, text-to-speech, video generation, and image models

## Import Process

1. Navigate to the Conduit WebUI at `/model-costs/`
2. Click "Import from CSV"
3. Upload the CSV file generated from the pricing documentation
4. Review the preview to ensure accuracy
5. Click "Import" to add the pricing data

## CSV Format

The standard CSV format for model cost imports includes:

```csv
Model Pattern,Provider,Model Type,Input Cost (per 1K tokens),Output Cost (per 1K tokens),Embedding Cost (per 1K tokens),Image Cost (per image),Audio Cost (per minute),Video Cost (per second),Priority,Active,Description
```

### Important Notes

- **Cost Units**: Input/Output costs are per 1K tokens in the CSV, but stored as per 1M tokens in the database
- **Model Pattern**: Should match the exact model ID used by the provider
- **Provider**: Must match the provider name in Conduit (e.g., "OpenAI", "Anthropic", "MiniMax")
- **Model Type**: One of: chat, embedding, image, audio, video
- **Priority**: Higher numbers take precedence when multiple patterns match
- **Active**: Set to "Yes" or "No" to enable/disable the pricing rule

## Updating Pricing

When provider pricing changes:

1. Update the markdown documentation in this directory
2. Regenerate the CSV file based on the updated markdown
3. Import the new CSV through the WebUI
4. The system will update existing entries based on the model pattern

## Decimal Precision

- The system stores costs with high precision
- Display formatting is handled by the UI
- No manual conversion needed between per-1K and per-1M tokens