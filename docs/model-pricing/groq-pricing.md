# Groq Model Pricing

**Last Updated**: 2025-07-19  
**Source**: Groq API Pricing Documentation

## Overview

Groq powers leading openly-available AI models with fast inference speeds. Get started for free and upgrade as your needs grow.

## Large Language Models (LLMs)

| Model | Speed (Tokens/Second) | Input (per 1M tokens) | Output (per 1M tokens) |
|-------|----------------------|----------------------|------------------------|
| Kimi K2 1T 128k | 200 | $1.00 | $3.00 (333,333 / $1) |
| Llama 4 Scout (17Bx16E) 128k | 594 | $0.11 (9.09M / $1) | $0.34 (2.94M / $1) |
| Llama 4 Maverick (17Bx128E) 128k | 562 | $0.20 (5M / $1) | $0.60 (1.6M / $1) |
| Llama Guard 4 12B 128k | 325 | $0.20 (5M / $1) | $0.20 (5M / $1) |
| DeepSeek R1 Distill Llama 70B 128k | 400 | $0.75 (1.33M / $1) | $0.99 (1.01M / $1) |
| Qwen3 32B 131k | 662 | $0.29 (3.44M / $1) | $0.59 (1.69M / $1) |
| Mistral Saba 24B 32k | 330 | $0.79 (1.27M / $1) | $0.79 (1.27M / $1) |
| Llama 3.3 70B Versatile 128k | 394 | $0.59 (1.69M / $1) | $0.79 (1.27M / $1) |
| Llama 3.1 8B Instant 128k | 840 | $0.05 (20M / $1) | $0.08 (12.5M / $1) |
| Llama 3 70B 8k | 330 | $0.59 (1.69M / $1) | $0.79 (1.27M / $1) |
| Llama 3 8B 8k | 1345 | $0.05 (20M / $1) | $0.08 (12.5M / $1) |
| Gemma 2 9B 8k | 500 | $0.20 (5M / $1) | $0.20 (5M / $1) |
| Llama Guard 3 8B 8k | 765 | $0.20 (5M / $1) | $0.20 (5M / $1) |

*Approximate number of tokens per $1

## Text-to-Speech (TTS) Models

| Model | Characters per Second | Cost (per 1M Characters) |
|-------|----------------------|-------------------------|
| PlayAI Dialog v1.0 | 140 | $50.00 |

## Automatic Speech Recognition (ASR) Models

| Model | Speed Factor | Cost (per Hour Transcribed) |
|-------|--------------|----------------------------|
| Whisper V3 Large | 217x | $0.111 |
| Whisper Large v3 Turbo | 228x | $0.04 |
| Distil-Whisper | 250x | $0.02 |

## Batch API

Batch processing lets you run thousands of API requests at scale by submitting your workload as an asynchronous batch of requests to Groq with:
- 50% lower cost
- No impact to your standard rate limits
- 24-hour to 7 day processing window

Learn more about Batch pricing and how to get started.

## Enterprise Features

For enterprise API solutions or on-prem deployments, contact Groq via their Enterprise Access Page.

## Notes

1. **Pricing Currency**: All prices are in USD
2. **Token Counting**: Varies by model and language
3. **Free Tier**: Get started for free and upgrade as needs grow
4. **Established in 2016**: Groq was established in 2016 for one thing: inference
5. **Speed**: Current speeds shown in tokens per second
6. **Other models**: Additional models available for specific customer requests including fine-tuned models

## CSV Import Format

When importing to Conduit, use the following CSV columns:
- Model Pattern (e.g., "llama-4-scout", "kimi-k2-1t")
- Provider (always "Groq")
- Model Type (chat/tts/asr)
- Input Cost (per 1K tokens) - divide the per-1M price by 1000
- Output Cost (per 1K tokens) - divide the per-1M price by 1000
- TTS Cost (per 1K characters) - for TTS models
- ASR Cost (per hour) - for ASR models
- Speed (tokens per second) - for performance metrics
- Priority (typically 10)
- Active (Yes/No)
- Description