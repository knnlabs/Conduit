# Provider Models

This document describes the models supported by various providers in Conduit.

## MiniMax Models

MiniMax provides both chat and image generation capabilities:

### Chat Models with Vision
- **Models**: 
  - `minimax-chat` (maps to `abab6.5-chat`) - Latest model
  - `abab6.5s-chat` - Smaller variant
  - `abab5.5-chat` - Previous generation
- **Features**: Text generation with image understanding
- **Context**: 245K tokens
- **Max Output**: 8K tokens
- **Supports**: Function calling, JSON mode, streaming

### Image Generation
- **Model**: `minimax-image` (maps to `image-01`)
- **Aspect Ratios**: 1:1, 16:9, 9:16, 4:3, 3:4, and more
- **Features**: Prompt optimization, high-quality generation

### Video Generation (Model Defined)
- **Model**: `video-01`
- **Resolutions**: 720x480, 1280x720, 1920x1080, 720x1280, 1080x1920
- **Max Duration**: 6 seconds

## OpenAI Models

### Image Generation
- **DALL-E 2**: `dall-e-2`
- **DALL-E 3**: `dall-e-3`

## Replicate

- Supports various models via model name
- Check Replicate's model hub for available options