import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';
import type { AsyncVideoGenerationRequest } from '@knn_labs/conduit-core-client';

// POST /api/videos/generate - Generate videos using Core SDK
export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as AsyncVideoGenerationRequest & { useProgressTracking?: boolean };
    const coreClient = await getServerCoreClient();
    
    // Check if client wants to use the new progress tracking method
    // Note: Server-side SDK doesn't have SignalR, so we still use generateAsync
    // The client will need to establish its own SignalR connection for real-time updates
    const result = await coreClient.videos.generateAsync(body);
    
    // Return the task information so client can track progress
    return NextResponse.json({
      ...result,
      // Add a flag to indicate that client-side progress tracking is available
      supportsProgressTracking: body.useProgressTracking ?? false
    });
  } catch (error) {
    return handleSDKError(error);
  }
}
