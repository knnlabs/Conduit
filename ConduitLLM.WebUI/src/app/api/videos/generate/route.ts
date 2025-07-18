import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';

interface AsyncVideoGenerationRequest {
  prompt: string;
  model?: string;
  duration?: number;
  fps?: number;
  resolution?: string;
  aspect_ratio?: string;
  style?: string;
  seed?: number;
  user?: string;
  webhook_url?: string;
}

// POST /api/videos/generate - Generate videos using Core SDK
export async function POST(request: NextRequest) {
  try {
    const body = await request.json() as AsyncVideoGenerationRequest;
    const coreClient = await getServerCoreClient();
    
    // Video generation only supports async mode
    const result = await coreClient.videos.generateAsync({
      prompt: body.prompt,
      model: body.model,
      duration: body.duration,
      fps: body.fps,
      resolution: body.resolution,
      aspect_ratio: body.aspect_ratio,
      style: body.style,
      seed: body.seed,
      user: body.user,
      webhook_url: body.webhook_url,
    } as any);
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}
