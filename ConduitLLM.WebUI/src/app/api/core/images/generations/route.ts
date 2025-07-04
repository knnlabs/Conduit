
import { NextRequest } from 'next/server';
import { validateCoreSession, extractVirtualKey } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { getServerCoreClient } from '@/lib/clients/server';
import { createValidationError } from '@/lib/utils/route-helpers';

export async function POST(request: NextRequest) {
  try {
    // Validate session - don't require virtual key in session yet
    const validation = await validateCoreSession(request, { requireVirtualKey: false });
    if (!validation.isValid) {
      return new Response(
        JSON.stringify({ error: validation.error || 'Unauthorized' }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      );
    }

    // Parse request body
    const body = await request.json();
    
    // Extract virtual key from various sources
    const virtualKey = body.virtual_key || 
                      extractVirtualKey(request) || 
                      validation.session?.virtualKey;
    
    if (!virtualKey) {
      return createValidationError(
        'Virtual key is required. Provide it via virtual_key field, x-virtual-key header, or Authorization header',
        { missingField: 'virtual_key' }
      );
    }

    // Remove virtual_key from body before sending to API
    const { virtual_key: _virtualKey, ...imageRequest } = body;
    
    // Validate required fields
    if (!imageRequest.prompt) {
      return createValidationError(
        'Prompt is required',
        { missingField: 'prompt' }
      );
    }

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
  } catch (error: unknown) {
    // Handle validation errors specially
    if ((error as { message?: string })?.message?.includes('required')) {
      return createValidationError((error as { message?: string })?.message || 'Validation error');
    }
    
    return mapSDKErrorToResponse(error);
  }
}

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