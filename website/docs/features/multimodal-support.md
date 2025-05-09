---
sidebar_position: 5
title: Multimodal Support
description: Learn how to use Conduit for multimodal AI capabilities including vision and image generation
---

# Multimodal Support

Conduit provides support for multimodal AI capabilities, allowing you to work with images, text, and other data types through a unified API.

## Vision Models

Conduit supports vision-enabled models from various providers, enabling you to analyze images and process them alongside text.

### Using Vision Models

To use a vision model with Conduit, send a chat completion request with image content:

```json
{
  "model": "my-gpt4-vision",
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "text",
          "text": "What's in this image?"
        },
        {
          "type": "image_url",
          "image_url": {
            "url": "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAA..."
          }
        }
      ]
    }
  ]
}
```

### Supported Image Formats

Conduit supports multiple image formats:
- JPEG
- PNG
- WebP
- GIF (first frame only for some providers)

### Image Input Methods

You can provide images in several ways:
- Base64-encoded data URLs
- HTTP/HTTPS URLs to publicly accessible images
- Local file paths (for self-hosted deployments only)

## Image Generation

Conduit also supports image generation through compatible providers:

```json
{
  "prompt": "A serene mountain landscape at sunset",
  "model": "my-dall-e",
  "n": 1,
  "size": "1024x1024"
}
```

### Image Generation Providers

Conduit supports image generation through:
- OpenAI (DALL-E)
- Stability AI (if configured)
- Midjourney (through integration)
- Other compatible providers

## Audio Processing

Some providers offer audio processing capabilities, which Conduit can expose:

- Speech-to-text transcription
- Text-to-speech synthesis
- Audio analysis

These features are available through dedicated endpoints with the same authentication and routing mechanisms as text-based models.

## Working with Multiple Modalities

Conduit provides a standardized way to combine different modalities in your requests:

```json
{
  "model": "my-multimodal-model",
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "text",
          "text": "Summarize the contents of this image and audio clip"
        },
        {
          "type": "image_url",
          "image_url": {
            "url": "https://example.com/image.jpg"
          }
        },
        {
          "type": "audio_url",
          "audio_url": {
            "url": "https://example.com/audio.mp3"
          }
        }
      ]
    }
  ]
}
```

## Provider Capabilities

Not all providers support all modalities. Conduit's provider capabilities detection helps identify which models can handle different input types.

To check model capabilities:
1. Navigate to **Models** in the Web UI
2. View the capabilities column for each model
3. Filter models by capability

## Next Steps

- Explore [Model Routing](model-routing) to understand how requests are directed to providers
- Learn about [Provider Integration](provider-integration) for adding new multimodal services
- See the [API Reference](../api-reference/overview) for detailed endpoint documentation