import { NextRequest } from 'next/server';
import { createCoreRoute } from '@/lib/utils/core-route-helpers';
import { validateImageGenerationRequest } from '@/lib/utils/core-route-validators';
import { withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { getServerCoreClient } from '@/lib/clients/server';

export const POST = createCoreRoute(
  {
    requireVirtualKey: false,
    validateBody: validateImageGenerationRequest,
    logContext: 'image_generations'
  },
  async ({ virtualKey }, body) => {
    // Remove virtual_key from body before sending to API
    const { virtual_key: _virtualKey, ...imageRequest } = body as Record<string, unknown>;
    
    // Get Core client with the virtual key
    const coreClient = getServerCoreClient(virtualKey);
    
    // Generate image using SDK
    const result = await withSDKErrorHandling(
      async () => coreClient.images.generate({
        prompt: imageRequest.prompt,
        model: imageRequest.model,
        n: imageRequest.n,
        size: imageRequest.size,
        quality: imageRequest.quality,
        style: imageRequest.style,
        response_format: imageRequest.response_format,
        user: imageRequest.user,
        // TODO: SDK does not yet support provider-specific parameters:
        // - aspectRatio, seed, steps, guidanceScale, negativePrompt
      }),
      'generate image'
    );

    return transformSDKResponse(result, {
      meta: {
        virtualKeyUsed: virtualKey.substring(0, 8) + '...',
        model: imageRequest.model,
      }
    });
  }
);

// Support for GET to check endpoint availability
export async function GET(_request: Request) {
  return transformSDKResponse({
    endpoint: '/v1/images/generations',
    methods: ['POST'],
    description: 'Create images from text prompts',
    authentication: 'Virtual key required',
    models: {
      openai: ['dall-e-2', 'dall-e-3'],
      minimax: ['minimax-image', 'image-01'],
      replicate: ['Various models via model name'],
      stability: ['stable-diffusion-xl-base-1.0', 'stable-diffusion-xl-1024-v1-0'],
    },
    parameters: {
      required: ['prompt'],
      optional: [
        'model',
        'n',
        'size',
        'quality',
        'style',
        'response_format',
        'aspect_ratio',
        'seed',
        'steps',
        'guidance_scale',
        'negative_prompt',
      ],
    },
    sizes: {
      'dall-e-2': ['256x256', '512x512', '1024x1024'],
      'dall-e-3': ['1024x1024', '1792x1024', '1024x1792'],
      'minimax-image': ['1:1', '16:9', '9:16', '4:3', '3:4'],
    },
  });
}