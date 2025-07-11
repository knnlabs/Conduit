import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/videos/generate - Generate videos using Core SDK
export async function POST(request: NextRequest) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await request.json();
    const coreClient = getServerCoreClient();
    
    // Call the Core SDK's async video generation method
    const result = await coreClient.videos.generateAsync({
      prompt: body.prompt,
      model: body.model,
      duration: body.duration,
      size: body.resolution, // Map resolution to size property
    });
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}
