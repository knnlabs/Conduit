# MiniMax Model Pricing

**Last Updated**: 2025-01-19  
**Source**: MiniMax API Pricing Documentation

## Text Models

### Chat Completion v2

| Model | Context Size | Input (per 1M tokens) | Output (per 1M tokens) |
|-------|--------------|----------------------|------------------------|
| MiniMax-M1 | < 200K tokens | $0.40 | $2.20 |
| MiniMax-M1 | > 200K tokens | $13.00 | $2.20 |
| MiniMax-Text-01 | All sizes | $0.20 | $1.10 |

### Batch Processing

| Model | Context Size | Input (per 1M tokens) | Output (per 1M tokens) |
|-------|--------------|----------------------|------------------------|
| MiniMax-M1 | < 200K tokens | $0.20 | $1.10 |
| MiniMax-M1 | > 200K tokens | $0.70 | $1.10 |

## Audio Models

### Text-to-Speech (T2A)

| Model | Use Case | Cost |
|-------|----------|------|
| speech-02-turbo | Standard quality TTS | $60 per 1M characters |
| speech-02-hd | High quality TTS | $100 per 1M characters |

### Voice Services

| Service | Cost |
|---------|------|
| Rapid Voice Cloning (speech-02-hd/turbo) | $3 per voice |
| Voice Design | $3 per voice |

## Video Generation

| Model | Resolution/Duration | Cost |
|-------|-------------------|------|
| MiniMax-Hailuo-02 | 768P, 5s video | $0.28 per video |
| MiniMax-Hailuo-02 | 768P, 10s video | $0.56 per video |
| MiniMax-Hailuo-02 | 1080P, 5s video | $0.48 per video |
| S2V-01 | Per video | $0.65 per video |
| T2V/I2V-01-Director | Per video | $0.43 per video |
| I2V-01-Live | Per video | $0.43 per video |

## Image Generation

| Model | Cost |
|-------|------|
| image-01 | $0.0035 per image |

## Notes

1. **Pricing Currency**: All prices are in USD
2. **Token Counting**: Varies by language and model
3. **Context Windows**: 
   - MiniMax-M1 supports up to and beyond 200K tokens with different pricing tiers
   - MiniMax-Text-01 has unified pricing regardless of context size
4. **Batch Processing**: Offers reduced pricing for batch operations
5. **Audio Character Counting**: Based on input text characters for TTS
6. **Video Resolution**: Default is 768P, with 1080P available for some models

## CSV Import Format

When importing to Conduit, use the following CSV columns:
- Model Pattern (e.g., "minimax-m1", "minimax-text-01")
- Provider (always "MiniMax")
- Model Type (chat/audio/video/image)
- Input Cost (per 1K tokens) - divide the per-1M price by 1000
- Output Cost (per 1K tokens) - divide the per-1M price by 1000
- Audio Cost (per minute) - for TTS models
- Video Cost (per second) - for video generation
- Image Cost (per image) - for image generation
- Priority (typically 10)
- Active (Yes/No)
- Description

Note: For models with context-dependent pricing (MiniMax-M1), separate entries may be needed.