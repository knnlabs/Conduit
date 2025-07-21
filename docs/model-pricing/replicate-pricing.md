# Replicate Model Pricing

**Last Updated**: 2025-07-19  
**Source**: Replicate API Pricing Documentation

## Overview

Replicate offers usage-based pricing where you only pay for what you use. Some models are billed by hardware and time, others by input and output.

## Public Models

### Image Generation Models

| Model | Cost | Description |
|-------|------|-------------|
| black-forest-labs/flux-1.1-pro | $0.04 per output image | Faster, better FLUX Pro. Text-to-image model with excellent image quality |
| black-forest-labs/flux-schnell | $3.00 per thousand output images | Fastest image generation model tailored for local development and personal use |
| recraft-ai/recraft-v3 | $0.04 per output image | Text-to-image model with the ability to generate long texts, vector art |

### Video Generation Models

| Model | Cost | Description |
|-------|------|-------------|
| google/veo-2 | $0.50 per second of output video | State of the art video generation model |
| wavespeedia/wan-2.1-i2v-720p | $0.25 per second of output video | Accelerated inference for Wan 2.1 I4B image to video with high resolution |

## Hardware Pricing

### CPU Instances

| Hardware Tier | Cost per Second | Cost per Hour | Specs |
|--------------|-----------------|---------------|-------|
| CPU (Small) | $0.000025 | $0.09 | 1x CPU, 2GB RAM |
| CPU | $0.000100 | $0.36 | 4x CPU, 8GB RAM |

### GPU Instances

| Hardware Tier | Cost per Second | Cost per Hour | Specs |
|--------------|-----------------|---------------|-------|
| Nvidia A100 (80GB) GPU | $0.001400 | $5.04 | 1x GPU, 10x CPU, 144GB RAM |
| 2x Nvidia A100 (80GB) GPU | $0.002800 | $10.08 | 2x GPU, 20x CPU, 288GB RAM |
| 4x Nvidia A100 (80GB) GPU | $0.005600 | $20.16 | 4x GPU, 40x CPU, 576GB RAM |
| 8x Nvidia A100 (80GB) GPU | $0.011200 | $40.32 | 8x GPU, 80x CPU, 960GB RAM |
| Nvidia H100 GPU | $0.001525 | $5.49 | 1x GPU, 13x CPU, 72GB RAM |
| Nvidia L40S GPU | $0.000975 | $3.51 | 1x GPU, 10x CPU, 65GB RAM |
| 2x Nvidia L40S GPU | $0.001950 | $7.02 | 2x GPU, 20x CPU, 144GB RAM |
| Nvidia T4 GPU | $0.000225 | $0.81 | 1x GPU, 4x CPU, 16GB RAM |

### Additional Hardware (Reserved for Committed Spend)

| Hardware Tier | Cost per Second | Cost per Hour |
|--------------|-----------------|---------------|
| 2x Nvidia H100 GPU | $0.003050 | $10.98 |
| 4x Nvidia H100 GPU | $0.006100 | $21.96 |
| 8x Nvidia H100 GPU | $0.012200 | $43.92 |

## Private Models

Private models run on dedicated hardware so you pay for all the time instances of the model are online:
- Setup time
- Idle time (waiting for requests)
- Active time (processing requests)

Exception: Fast booting fine-tunes are only billed for active processing time.

## Model Deployment with Cog

You can deploy your own custom models using Cog, Replicate's open-source tool for packaging machine learning models.

## Enterprise & Volume Discounts

Enterprise features available:
- Dedicated account manager
- Priority support
- Higher GPU limits
- Performance SLAs
- Help with onboarding, custom models, and optimizations
- Volume discounts for large amounts of spend

Contact: sales@replicate.com

## Notes

1. **Pricing Currency**: All prices are in USD
2. **Billing Model**: 
   - Most models billed by time (per second of compute)
   - Some models billed by output (per image/video)
3. **Hardware Scaling**: Automatically scales up and down based on demand
4. **Private Models**: Pay for all online time except fast booting fine-tunes
5. **Model Estimates**: Cost estimates available on each model's page

## CSV Import Format

When importing to Conduit, use the following CSV columns:
- Model Pattern (e.g., "flux-1.1-pro", "veo-2")
- Provider (always "Replicate")
- Model Type (image/video/text)
- Hardware Tier (for time-based models)
- Cost per Second (for time-based models)
- Cost per Output (for output-based models)
- Output Type (image/video/second)
- Priority (typically 10)
- Active (Yes/No)
- Description

Note: Replicate's pricing model is different from token-based providers. Models are either billed by compute time or by output unit.