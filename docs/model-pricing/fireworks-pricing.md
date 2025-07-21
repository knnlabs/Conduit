# Fireworks AI Model Pricing

**Last Updated**: 2025-07-19  
**Source**: Fireworks AI Pricing Documentation

## Overview

Fireworks offers serverless inference with per-token pricing, fine-tuning capabilities, and on-demand GPU deployments. Start building with $1 in free credits.

## Text and Vision Models

### Model Categories by Size

| Model Category | Input (per 1M tokens) | Output (per 1M tokens) |
|----------------|----------------------|------------------------|
| Less than 4B parameters | $0.10 | - |
| 4B - 16B parameters | $0.20 | - |
| More than 16B parameters | $0.90 | - |
| MoE 0B - 56B parameters (e.g. Mixtral 8x7B) | $0.50 | - |
| MoE 56.1B - 176B parameters (e.g. DBRX, Mixtral 8x22B) | $1.20 | - |

### Specific Models

| Model | Input (per 1M tokens) | Output (per 1M tokens) |
|-------|----------------------|------------------------|
| DeepSeek V3 | $0.90 | - |
| DeepSeek R1 (Fast) | $3.00 | $8.00 |
| DeepSeek R1 0528 (Fast) | $3.00 | $8.00 |
| DeepSeek R1 (Basic) | $0.55 | $2.19 |
| Meta Llama 3.1 405B | $3.00 | - |
| Meta Llama 4 Maverick (Basic) | $0.22 | $0.88 |
| Meta Llama 4 Scout (Basic) | $0.15 | $0.60 |
| Qwen3 235B | $0.22 | $0.88 |
| Qwen3 30B | $0.15 | $0.60 |
| Kimi K2 Instruct (1T parameter model) | - | - |

## Speech to Text (STT)

| Model | Cost |
|-------|------|
| Whisper-v3-large | $0.0015 per audio minute ($0.000025 per second) |
| Whisper-v3-large-turbo | $0.0009 per audio minute ($0.000015 per second) |
| Streaming transcription service | $0.0032 per audio minute ($0.0000533 per second) |

**Additional Notes**:
- Diarization adds a 40% surcharge to pricing
- Batch API prices are reduced 40%

## Image Generation

| Model | Cost per Step | Cost per Image |
|-------|---------------|----------------|
| All Non-Flux Models (SDXL, Playground, etc) | $0.00013 ($0.0039 per 30 step image) | $0.0002 ($0.006 per 30 step image) |
| FLUX.1[dev] | $0.0005 ($0.014 per 28 step image) | N/A on serverless |
| FLUX.1[schnell] | $0.00035 ($0.0014 per 4 step image) | N/A on serverless |
| FLUX.1 Kontext Pro | - | $0.04 per image |
| FLUX.1 Kontext Max | - | $0.08 per image |

**Note**: All models besides the Flux Kontext models are charged by the number of inference steps (denoising iterations). The Flux Kontext models are charged a flat rate per generated image.

## Embeddings

| Model Size | Cost (per 1M input tokens) |
|------------|---------------------------|
| Up to 150M parameters | $0.008 |
| 150M - 350M parameters | $0.016 |

## Fine-Tuning

| Base Model | Cost (per 1M training tokens) |
|------------|------------------------------|
| Models up to 16B parameters | $0.50 |
| Models 16.1B - 80B | $3.00 |
| DeepSeek R1 / V3 | $10.00 |

**Note**: There is no additional cost for having LoRA fine-tunes up to the quota for an account. Inference for fine-tuned models costs the same as the base models.

## On-Demand GPU Deployments

| GPU Type | Cost per Hour |
|----------|---------------|
| A100 80 GB GPU | $2.90 |
| H100 80 GB GPU | $5.80 |
| H200 141 GB GPU | $6.99 |
| B200 180 GB GPU | $11.99 |
| AMD MI300X | $4.99 |

**Note**: Pay per GPU second, with no extra charges for start-up times. For estimates of per-token prices, see their blog. Results vary by use case, but they often observe improvements like ~250% higher throughput and 50% faster speed on Fireworks compared to open source inference engines.

## Enterprise Features

For enterprise deployments, contact Fireworks for:
- Faster speeds
- Lower costs
- Higher rate limits

## Notes

1. **Pricing Currency**: All prices are in USD
2. **Token Counting**: Varies by model and language
3. **Free Credits**: New users get $1 in free credits
4. **Billing Model**: Postpaid billing with high rate limits
5. **Batch Processing**: 40% discount for batch API usage
6. **Model Availability**: Base model pricing shown, actual model availability may vary

## CSV Import Format

When importing to Conduit, use the following CSV columns:
- Model Pattern (e.g., "deepseek-v3", "llama-3.1-405b")
- Provider (always "Fireworks")
- Model Type (chat/embedding/image/audio)
- Input Cost (per 1K tokens) - divide the per-1M price by 1000
- Output Cost (per 1K tokens) - divide the per-1M price by 1000
- Embedding Cost (per 1K tokens) - for embedding models
- Image Cost (per image/step) - for image generation
- Audio Cost (per minute) - for STT models
- Training Cost (per 1K tokens) - for fine-tuning
- Priority (typically 10)
- Active (Yes/No)
- Description

Note: Some models are priced by parameter count categories rather than specific model names.