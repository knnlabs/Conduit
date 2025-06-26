---
sidebar_position: 2
title: Image Generation
description: Generate high-quality images from text prompts using OpenAI DALL-E, MiniMax, and Replicate
---

# Image Generation

Conduit provides image generation capabilities through the standard `/v1/images/generations` endpoint, supporting OpenAI DALL-E, MiniMax, and Replicate models with OpenAI-compatible API format.

## Quick Start

### Basic Image Generation

```javascript
import OpenAI from 'openai';

const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'https://api.conduit.yourdomain.com/v1'
});

const response = await openai.images.generate({
  model: 'dall-e-3',
  prompt: 'A futuristic city skyline at sunset with flying cars and neon lights',
  size: '1024x1024',
  quality: 'hd',
  n: 1
});

console.log('Generated image URL:', response.data[0].url);
console.log('Revised prompt:', response.data[0].revised_prompt);
```

### Multiple Image Generation

```javascript
const response = await openai.images.generate({
  model: 'dall-e-2',
  prompt: 'A cute robot playing with a cat in a garden',
  size: '512x512',
  n: 4 // Generate 4 variations
});

response.data.forEach((image, index) => {
  console.log(`Image ${index + 1}: ${image.url}`);
});
```

## Supported Models and Providers

### OpenAI DALL-E

**Models: `dall-e-2`, `dall-e-3`**

| Model | Max Resolution | Quality Options | Style Control | Notes |
|-------|----------------|-----------------|---------------|-------|
| **DALL-E 3** | 1024x1024 | Standard, HD | Natural, Vivid | Only n=1 supported |
| **DALL-E 2** | 1024x1024 | Standard | None | Multiple variations supported |

```javascript
// DALL-E 3 with quality and style control
const dalleImage = await openai.images.generate({
  model: 'dall-e-3',
  prompt: 'A magical forest with glowing mushrooms and fairy lights',
  size: '1024x1024',
  quality: 'hd',      // 'standard' or 'hd'
  style: 'vivid',     // 'natural' or 'vivid'
  n: 1                // DALL-E 3 supports only n=1
});

// DALL-E 2 for multiple variations
const dalle2Images = await openai.images.generate({
  model: 'dall-e-2',
  prompt: 'A serene mountain landscape with a crystal-clear lake',
  size: '1024x1024',
  n: 4 // Multiple variations supported
});
```

**DALL-E Features:**
- **Prompt Enhancement**: DALL-E 3 automatically improves prompts
- **Safety Filtering**: Built-in content policy enforcement
- **High Resolution**: Up to 1024x1024 pixels
- **Style Control**: Natural vs. vivid styles (DALL-E 3)

### MiniMax Image Generation

**Model: `minimax-image` (alias for `image-01`)**

```javascript
const minimaxImage = await openai.images.generate({
  model: 'minimax-image',
  prompt: 'A cyberpunk street scene with neon signs and rain reflections',
  size: '1024x1024'
});
```

**Available Aspect Ratios:**
- 1:1 (square)
- 16:9 (landscape)  
- 9:16 (portrait)
- 4:3 (standard)
- 3:4 (portrait)

**Features:**
- Fast generation (30-60 seconds)
- Cost-effective pricing
- Good quality-to-price ratio
- Multiple aspect ratios

### Replicate Models

**Models: Various community and official models**

Replicate provides access to a wide variety of community-hosted models. Specify the full model name including the owner.

```javascript
// Example with a Replicate model
const replicateImage = await openai.images.generate({
  model: 'stability-ai/stable-diffusion',
  prompt: 'A portrait of a wise old wizard with a long beard and mystical robes',
  size: '1024x1024'
});
```

**Features:**
- **Community models**: Access to various open-source models
- **Async processing**: Built-in polling for completion
- **Variable pricing**: Depends on the specific model used
- **Extensive catalog**: Hundreds of available models

## Error Handling

### Common Error Handling

```javascript
try {
  const response = await openai.images.generate({
    model: 'dall-e-3',
    prompt: 'A beautiful landscape',
    size: '1024x1024'
  });
  
  console.log('Image generated:', response.data[0].url);
} catch (error) {
  console.error('Generation failed:', error.message);
  
  // Handle specific error types
  if (error.code === 'content_policy_violation') {
    console.log('Prompt violates content policy');
  } else if (error.code === 'rate_limit_exceeded') {
    console.log('Rate limit exceeded, try again later');
  } else if (error.code === 'insufficient_quota') {
    console.log('Insufficient quota for this model');
  }
}
```

## Best Practices

### Prompt Engineering

```javascript
// Good prompts for image generation
const goodPrompts = [
  'A professional headshot of a business executive in modern office lighting',
  'Product photography of a smartphone on white background with studio lighting',
  'A serene landscape with mountains reflected in a crystal-clear lake at sunset',
  'Modern minimalist interior design with natural lighting and clean lines'
];

// Tips for better results:
// 1. Be specific about style and composition
// 2. Include lighting conditions
// 3. Mention quality descriptors (professional, high-quality, detailed)
// 4. Keep prompts focused and clear
// 5. Avoid overly complex scenes
```

### Performance Optimization

```javascript
// Use appropriate models for different use cases
const configurations = {
  // Fast generation for testing
  testing: {
    model: 'dall-e-2',
    size: '512x512',
    quality: 'standard'
  },
  
  // Balanced quality and speed
  production: {
    model: 'dall-e-3',
    size: '1024x1024',
    quality: 'standard'
  },
  
  // Maximum quality
  premium: {
    model: 'dall-e-3',
    size: '1024x1024',
    quality: 'hd',
    style: 'natural'
  },
  
  // Cost-effective option
  budget: {
    model: 'minimax-image',
    size: '1024x1024'
  }
};
```

## Limitations

### What's NOT Supported

Conduit's image generation currently supports only basic text-to-image generation. The following features are **not implemented**:

- **Image editing** (`/v1/images/edits`) - inpainting and outpainting
- **Image variations** (`/v1/images/variations`) - generating variations of existing images
- **Image-to-image** transformations
- **Async image generation endpoints** - only synchronous generation is available

Only the standard `/v1/images/generations` endpoint is implemented.

## Next Steps

- **Video Generation**: Explore [video generation capabilities](video-generation)
- **Admin API**: Manage providers and virtual keys via [Admin API](../admin/admin-api-overview)