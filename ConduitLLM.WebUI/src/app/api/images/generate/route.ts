import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/images/generate - Generate images using Core SDK
export async function POST(request: NextRequest) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await request.json();
    const coreClient = getServerCoreClient();
    
    // Call the Core SDK's image generation method
    const result = await coreClient.images.generate({
      prompt: body.prompt,
      model: body.model,
      size: body.size,
      quality: body.quality,
      n: body.n || 1,
    });
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}
