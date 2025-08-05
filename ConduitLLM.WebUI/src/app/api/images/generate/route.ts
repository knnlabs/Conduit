import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/sdk-config';

// POST /api/images/generate - Generate images using Core SDK
export async function POST(request: NextRequest) {

  try {
    const body = await request.json() as {
      prompt: string;
      model?: string;
      n?: number;
      quality?: 'standard' | 'hd';
      'response_format'?: 'url' | 'b64_json';
      size?: '256x256' | '512x512' | '1024x1024' | '1792x1024' | '1024x1792';
      style?: 'vivid' | 'natural';
      user?: string;
      'webhook_url'?: string;
      'webhook_metadata'?: Record<string, unknown>;
      'timeout_seconds'?: number;
      async?: boolean;
    };
    
    const coreClient = await getServerCoreClient();
    
    // Check if async generation is requested
    if (body.async === true) {
      // Use async generation
      const result = await coreClient.images.generateAsync(body);
      return NextResponse.json(result);
    } else {
      // Use synchronous generation (default)
      const result = await coreClient.images.generate(body);
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
