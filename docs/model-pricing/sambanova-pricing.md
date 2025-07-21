# SambaNova Model Pricing

**Last Updated**: 2025-07-19  
**Source**: SambaNova Cloud Platform

## Overview

SambaNova provides high-performance AI models with a focus on open-source models. Their pricing model information is available through their cloud platform.

## Available Models

### DeepSeek Models
- **DeepSeek-R1-0528** - Text model
- **DeepSeek-R1-Distill-Llama-70B** - Text model
- **DeepSeek-V3-0324** - Text model

### Meta Models
- **Llama-3.3-Swallow-70B-Instruct-v0.4** - Text model
- **Llama-4-Maverick-17B-128E-Instruct** - Text model with image support
- **Meta-Llama-3.1-8B-Instruct** - Text model
- **Meta-Llama-3.3-70B-Instruct** - Text model

### Qwen Models
- **Qwen3-32B** - Text model

### OpenAI Models (via SambaNova)
- **Whisper-Large-v3** - Audio model

### Other Models
- Additional models available and grouped by family

## Pricing Structure

SambaNova Cloud offers:
- Free tier for exploration and testing
- Pay-as-you-go pricing for production use
- Enterprise pricing for large-scale deployments

**Note**: Specific pricing details are not publicly listed on their pricing page. Users need to:
1. Sign up for SambaNova Cloud
2. Access the platform to view current pricing
3. Contact sales for enterprise pricing

## Features

- **Model Variety**: Access to latest open-source models including DeepSeek, Meta Llama, and Qwen families
- **Multi-Modal Support**: Some models support text and image inputs
- **Audio Processing**: Whisper model available for audio transcription
- **Grouped by Family**: Models organized by provider family for easy navigation
- **Filter Options**: Platform includes filtering capabilities to find the right model

## Enterprise Options

For enterprise deployments, SambaNova offers:
- Custom pricing based on usage
- Dedicated support
- SLA guarantees
- On-premises deployment options

## Notes

1. **Platform Access Required**: Pricing details available after signing up for SambaNova Cloud
2. **Model Availability**: Model list may change as new models are added
3. **Multi-Modal Support**: Some models like Llama-4-Maverick support both text and image inputs
4. **Open Source Focus**: Primarily hosts open-source models from various providers

## CSV Import Format

When importing to Conduit, use the following CSV columns:
- Model Pattern (e.g., "deepseek-r1-0528", "llama-3.3-70b")
- Provider (always "SambaNova")
- Model Type (chat/audio/multimodal)
- Input Cost (per 1K tokens) - requires platform access
- Output Cost (per 1K tokens) - requires platform access
- Priority (typically 10)
- Active (Yes/No)
- Description

Note: Since specific pricing is not publicly available, costs need to be obtained through the SambaNova Cloud platform after registration.