import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';

interface AsyncImageGenerationRequest {
  prompt: string;
  model?: string;
  n?: number;
  quality?: string;
  responseFormat?: string;
  size?: string;
  style?: string;
  user?: string;
  webhookUrl?: string;
  async?: boolean;
  [key: string]: unknown;
}

interface ImageGenerationRequest {
  prompt: string;
  model?: string;
  n?: number;
  quality?: string;
  responseFormat?: string;
  size?: string;
  style?: string;
  user?: string;
  [key: string]: unknown;
}

// POST /api/images/generate - Generate images using Core SDK
export async function POST(request: NextRequest) {

  try {
    const body = await request.json() as AsyncImageGenerationRequest;
    
    const coreClient = await getServerCoreClient();
    
    // Check if async generation is requested
    if (body.async === true) {
      // Use async generation
      const result = await coreClient.images.generateAsync(body as AsyncImageGenerationRequest);
      return NextResponse.json(result);
    } else {
      // Use synchronous generation (default)
      const syncRequest: ImageGenerationRequest = {
        prompt: body.prompt,
        model: body.model,
        n: body.n,
        quality: body.quality,
        responseFormat: body.responseFormat,
        size: body.size,
        style: body.style,
        user: body.user
      };
      
      const result = await coreClient.images.generate(syncRequest as any);
      
      return NextResponse.json(result);
    }
  } catch (error) {
    console.error('[Images API] Error occurred:', error);
    if (error instanceof Error) {
      console.error('[Images API] Error message:', error.message);
      console.error('[Images API] Error stack:', error.stack);
    }
    return handleSDKError(error);
  }
}
