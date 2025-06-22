# MiniMax Models Configuration

## Image Generation Models

### image-01
- **Type**: Image Generation
- **Provider**: MiniMax
- **Capabilities**:
  - Text-to-image generation
  - Multiple aspect ratios: 1:1, 16:9, 9:16, 4:3, 3:4, 2.35:1, 1:2.35, 21:9, 9:21
  - Base64 and URL response formats
  - Prompt optimization
  - High-quality image generation

## Configuration

To use MiniMax image generation, configure the following:

1. **Provider Credentials**:
```json
{
  "ProviderName": "minimax",
  "ApiKey": "your-minimax-api-key",
  "ApiBase": "https://api.minimax.chat/v1"
}
```

2. **Model Mapping**:
```json
{
  "ModelAlias": "minimax-image",
  "ProviderName": "minimax",
  "ProviderModelId": "image-01"
}
```

## Usage Examples

### Via API:
```bash
curl -X POST http://localhost:5000/v1/images/generations \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "A beautiful sunset over mountains",
    "model": "minimax-image",
    "size": "16:9",
    "n": 1,
    "response_format": "url"
  }'
```

### Via Discovery API:
```bash
# Check if model supports image generation
curl http://localhost:5000/v1/discovery/models/minimax-image/capabilities/ImageGeneration \
  -H "Authorization: Bearer YOUR_API_KEY"

# Get all MiniMax models
curl http://localhost:5000/v1/discovery/providers/minimax/models \
  -H "Authorization: Bearer YOUR_API_KEY"
```

## Video Generation Models (Future)

### video-01
- **Type**: Video Generation
- **Provider**: MiniMax
- **Status**: Architecture designed, implementation pending
- **Capabilities**:
  - Text-to-video generation
  - Up to 6 seconds duration
  - Multiple resolutions: 720x480, 1280x720, 1920x1080, 720x1280, 1080x1920
  - Async task-based generation

## Chat Models with Vision

### abab6.5-chat
- **Type**: Chat with Vision
- **Provider**: MiniMax
- **Capabilities**:
  - Text generation
  - Image understanding
  - Multimodal conversations
  - 245K token context window
  - 8K max output tokens