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

### Via Admin API Discovery:
```bash
# Discover MiniMax models (requires master key authentication)
curl http://localhost:8080/api/modelprovidermapping/discover/provider/minimax \
  -H "X-API-Key: YOUR_MASTER_KEY"

# Check specific model capabilities
curl http://localhost:8080/api/modelprovidermapping/discover/model/minimax/minimax-image \
  -H "X-API-Key: YOUR_MASTER_KEY"

# Test if a model supports a specific capability
curl http://localhost:8080/api/modelprovidermapping/discover/capability/minimax-image/ImageGeneration \
  -H "X-API-Key: YOUR_MASTER_KEY"
```

> **Note**: Model discovery is an administrative task performed through the Admin API using master key authentication. Core API focuses on serving requests with pre-configured model mappings.

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