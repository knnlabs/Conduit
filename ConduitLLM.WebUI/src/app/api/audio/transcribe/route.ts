import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/audio/transcribe - Transcribe audio using Core SDK
export async function POST(request: NextRequest) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const formData = await request.formData();
    const file = formData.get('file') as File;
    const model = formData.get('model') as string | null;
    const language = formData.get('language') as string | null;
    const prompt = formData.get('prompt') as string | null;
    
    if (!file) {
      return NextResponse.json(
        { error: 'File is required' },
        { status: 400 }
      );
    }
    
    const coreClient = await getServerCoreClient();
    
    // Convert File to Buffer for SDK
    const buffer = Buffer.from(await file.arrayBuffer());
    
    // Call the Core SDK's audio transcription method
    const result = await coreClient.audio.transcribe({
      file: {
        data: buffer,
        filename: file.name
      },
      model: model as any, // Model is required but we'll let the API validate
      language: language || undefined,
      prompt: prompt || undefined,
    });
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}
