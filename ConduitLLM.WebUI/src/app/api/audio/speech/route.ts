import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/audio/speech - Generate speech using Core SDK
export async function POST(request: NextRequest) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await request.json();
    const coreClient = await getServerCoreClient();
    
    // Call the Core SDK's text-to-speech method
    const result = await coreClient.audio.generateSpeech({
      input: body.text,
      model: body.model,
      voice: body.voice,
      speed: body.speed,
    });
    
    // The SDK returns an object with audio Buffer, we need to return the audio data
    return new NextResponse(result.audio, {
      headers: {
        'Content-Type': 'audio/mpeg',
      },
    });
  } catch (error) {
    return handleSDKError(error);
  }
}
