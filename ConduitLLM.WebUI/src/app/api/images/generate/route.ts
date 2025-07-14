import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerCoreClient } from '@/lib/server/coreClient';

// POST /api/images/generate - Generate images using Core SDK
export async function POST(request: NextRequest) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await request.json();
    console.log('[Images API] Request body:', JSON.stringify(body, null, 2));
    
    const coreClient = await getServerCoreClient();
    console.log('[Images API] Core client obtained successfully');
    
    // Check if async generation is requested
    if (body.async === true) {
      // Use async generation
      console.log('[Images API] Using async generation');
      const result = await coreClient.images.generateAsync({
        prompt: body.prompt,
        model: body.model,
        n: body.n,
        quality: body.quality,
        response_format: body.response_format,
        size: body.size,
        style: body.style,
        user: body.user,
        webhook_url: body.webhook_url,
        // Pass through any additional parameters
        ...body
      });
      
      console.log('[Images API] Async generation result:', result);
      return NextResponse.json(result);
    } else {
      // Use synchronous generation (default)
      console.log('[Images API] Using synchronous generation');
      const result = await coreClient.images.generate({
        prompt: body.prompt,
        model: body.model,
        n: body.n,
        quality: body.quality,
        response_format: body.response_format,
        size: body.size,
        style: body.style,
        user: body.user,
        // Pass through any additional parameters
        ...body
      });
      
      console.log('[Images API] Sync generation result:', result);
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
