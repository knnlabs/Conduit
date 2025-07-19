# Cerebras Model Pricing

**Last Updated**: 2025-07-19  
**Source**: Cerebras API Pricing Documentation

## Overview

Cerebras offers three pricing tiers: Exploration (pay-as-you-go), Growth (subscription), and Enterprise (custom).

## Pricing Tiers

### Exploration Tier
**Pay-as-you-go simplicity for prototyping, testing, and small-scale applications**

| Model | Speed (Tokens/s) | Input (per 1M tokens) | Output (per 1M tokens) |
|-------|------------------|----------------------|------------------------|
| Llama 4 Maverick | ~1500 | $0.20 | $0.60 |
| Llama 3.1 8B | ~2200 | $0.10 | $0.10 |
| Llama 3.3 70B | ~2100 | $0.85 | $1.20 |
| Qwen 3 32B | ~2600 | $0.40 | $0.80 |
| Qwen 3 235B* | ~1500 | $0.60 | $1.20 |
| Deepseek R1 Distill Llama 70B | ~2600 | $2.20 | $2.50 |

*Preview models are intended for evaluation purposes only and may be discontinued at short notice.

**Features**:
- Instant access to popular Cerebras-supported models
- Standard 16k/32k context length
- No minimum commitment – pay only for what you use
- Community support via Discord
- Pay per token available
- *Llama API coming soon

### Growth Tier
**Monthly subscription for growing teams, production applications, and consistent workloads**

Everything in Exploratory, plus:
- Higher rate limits (300+ RPM)
- Higher request priority (lower latency at high traffic times)
- Early access to upcoming models and API features
- Monthly subscription with predictable costs
- Prioritized support via Slack

**Monthly subscription starting at $1500/month**

#### Growth Tier Pricing Examples

**Qwen3 32B**
| Tier | Monthly Fee | Max Tokens per Min | Max Tokens per Day | Max Requests per Min |
|------|-------------|-------------------|-------------------|---------------------|
| 1 | $1,500 | 300k input / 30k output | 70M | 300 |
| 3 | $5,000 | 1M input / 100k output | 325M | 1,000 |
| 4 | $7,000 | 1.2M input / 120k output | 470M | 1,200 |
| 5 | $10,000 | 1.45M input / 145k output | 680M | 1,450 |

**Llama-4 Scout 17B**
| Tier | Monthly Fee | Max Tokens per Min | Max Tokens per Day | Max Requests per Min |
|------|-------------|-------------------|-------------------|---------------------|
| 1 | $1,500 | 300k input / 30k output | 54M | 300 |
| 2 | $3,000 | 600k input / 60k output | 115M | 600 |
| 3 | $5,000 | 1M input / 100k output | 185M | 1,000 |
| 4 | $7,000 | 1.2M input / 120k output | 255M | 1,200 |
| 5 | $10,000 | 1.45M input / 145k output | 365M | 1,450 |

**Llama-3.3 70B**
| Tier | Monthly Fee | Max Tokens per Min | Max Tokens per Day | Max Requests per Min |
|------|-------------|-------------------|-------------------|---------------------|
| 1 | $1,500 | 300k input / 30k output | 41M | 300 |
| 2 | $3,000 | 600k input / 60k output | 85M | 600 |
| 3 | $5,000 | 1M input / 100k output | 140M | 1,000 |
| 4 | $7,000 | 1.2M input / 120k output | 190M | 1,200 |
| 5 | $10,000 | 1.45M input / 145k output | 275M | 1,450 |

**DeepSeek R1 Llama-70B Distilled**
| Tier | Monthly Fee | Max Tokens per Min | Max Tokens per Day | Max Requests per Min |
|------|-------------|-------------------|-------------------|---------------------|
| 1 | $2,000 | 200k input / 33k output | 28M | 200 |
| 2 | $3,600 | 400k input / 66k output | 56M | 400 |
| 3 | $7,500 | 800k input / 133k output | 120M | 800 |

### Enterprise Tier
**Perfect for large-scale deployments, regulated industries, and organizations requiring guaranteed performance**

Everything in Exploratory, plus:
- Access to all Cerebras-supported models and support for fine-tuned models
- Highest rate limits for production workloads
- Lowest latency with dedicated queue priority
- Extended context length support (up to 128k)
- Custom pricing tailored to your usage
- Dedicated deployment options
- Model fine-tuning and training services available
- Dedicated support team with response time guarantees

**Contact Sales for custom pricing**

## Notes

1. **Pricing Currency**: All prices are in USD
2. **Context Windows**: 
   - Exploration tier: Standard 16k/32k context
   - Enterprise tier: Extended context up to 128k
3. **Rate Limits**: Higher tiers offer increased rate limits and priority
4. **Support**: 
   - Exploration: Community via Discord
   - Growth: Prioritized via Slack
   - Enterprise: Dedicated support team
5. **Fine-tuning**: Available in Enterprise tier
6. **Preview Models**: May be discontinued at short notice

## CSV Import Format

When importing to Conduit, use the following CSV columns:
- Model Pattern (e.g., "llama-4-maverick", "qwen3-32b")
- Provider (always "Cerebras")
- Model Type (chat)
- Input Cost (per 1K tokens) - divide the per-1M price by 1000
- Output Cost (per 1K tokens) - divide the per-1M price by 1000
- Speed (tokens per second)
- Priority (typically 10)
- Active (Yes/No)
- Description

Note: Growth tier pricing is subscription-based with token limits rather than per-token pricing.