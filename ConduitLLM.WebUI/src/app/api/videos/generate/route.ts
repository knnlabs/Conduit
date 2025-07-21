import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';
import type { AsyncVideoGenerationRequest } from '@knn_labs/conduit-core-client';

// POST /api/videos/generate - Generate videos using Core SDK
export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as AsyncVideoGenerationRequest;
    const coreClient = await getServerCoreClient();
    
    // Video generation only supports async mode
    const result = await coreClient.videos.generateAsync(body);
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}
