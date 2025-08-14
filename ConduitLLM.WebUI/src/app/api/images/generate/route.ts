import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/sdk-config';
import type { ImageGenerationRequest } from '@knn_labs/conduit-core-client';

// POST /api/images/generate - Generate images using Core SDK
export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as ImageGenerationRequest;
    const coreClient = await getServerCoreClient();
    
    // Use the SDK's image generation method
    const result = await coreClient.images.generate(body);
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}