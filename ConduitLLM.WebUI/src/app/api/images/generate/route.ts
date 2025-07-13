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
    const coreClient = await getServerCoreClient();
    
    // Check if async generation is requested
    if (body.async === true) {
      // Use async generation
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
      
      return NextResponse.json(result);
    } else {
      // Use synchronous generation (default)
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
      
      return NextResponse.json(result);
    }
  } catch (error) {
    return handleSDKError(error);
  }
}
