
import { NextRequest } from 'next/server';
import { validateCoreSession, extractVirtualKey } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { getServerCoreClient } from '@/lib/clients/server';
import { createValidationError } from '@/lib/utils/route-helpers';

export async function POST(request: NextRequest) {
  try {
    // Validate session - don't require virtual key in session yet
    const validation = await validateCoreSession(request, { requireVirtualKey: false });
    if (!validation.isValid) {
      return new Response(
        JSON.stringify({ error: validation.error || 'Unauthorized' }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      );
    }

    // Parse multipart form data for file upload
    const formData = await request.formData();
    
    // Extract virtual key from various sources
    const virtualKey = formData.get('virtual_key') as string || 
                      extractVirtualKey(request) || 
                      validation.session?.virtualKey;
    
    if (!virtualKey) {
      return createValidationError(
        'Virtual key is required. Provide it via virtual_key field, x-virtual-key header, or Authorization header',
        { missingField: 'virtual_key' }
      );
    }

    // Extract audio file
    const audioFile = formData.get('file') as File;
    if (!audioFile) {
      return createValidationError(
        'Audio file is required',
        { missingField: 'file' }
      );
    }

    // Extract transcription parameters
    const model = formData.get('model') as string || 'whisper-1';
    const language = formData.get('language') as string;
    const prompt = formData.get('prompt') as string;
    const responseFormat = formData.get('response_format') as string || 'json';
    const temperature = formData.get('temperature') ? parseFloat(formData.get('temperature') as string) : undefined;
    
    // Convert timestamp_granularities if provided
    const timestampGranularitiesStr = formData.get('timestamp_granularities') as string;
    let timestampGranularities: ('word' | 'segment')[] | undefined;
    if (timestampGranularitiesStr) {
      try {
        timestampGranularities = JSON.parse(timestampGranularitiesStr);
      } catch {
        timestampGranularities = [timestampGranularitiesStr as 'word' | 'segment'];
      }
    }
    
    // Use SDK audio transcription
    const client = getServerCoreClient(virtualKey);
    
    // Convert File to Buffer for SDK
    const arrayBuffer = await audioFile.arrayBuffer();
    const buffer = Buffer.from(arrayBuffer);
    
    const result = await withSDKErrorHandling(
      async () => (client as { audio: { transcribe: (params: unknown) => Promise<unknown> } }).audio.transcribe({
        file: {
          data: buffer,
          filename: audioFile.name,
          contentType: audioFile.type,
        },
        model: model as unknown, // Cast to TranscriptionModel
        language,
        prompt,
        response_format: responseFormat as unknown,
        temperature,
        timestamp_granularities: timestampGranularities,
      }),
      'transcribe audio'
    );

    // Handle different response formats
    if (responseFormat === 'text' || responseFormat === 'srt' || responseFormat === 'vtt') {
      // For text-based formats, the result should be the text property
      const textResult = typeof result === 'string' ? result : (result as { text?: string }).text || '';
      return new Response(textResult, {
        headers: {
          'Content-Type': responseFormat === 'text' ? 'text/plain' : 
                          responseFormat === 'srt' ? 'text/srt' : 
                          'text/vtt',
        },
      });
    }

    return transformSDKResponse(result, {
      meta: {
        virtualKeyUsed: virtualKey.substring(0, 8) + '...',
        model: model || 'whisper-1',
        fileName: audioFile.name,
        fileSize: audioFile.size,
      }
    });
  } catch (error: unknown) {
    // Handle validation errors specially
    if ((error as { message?: string })?.message?.includes('required')) {
      return createValidationError((error as { message?: string })?.message || 'Validation error');
    }
    
    // Handle file size errors
    if ((error as Record<string, unknown>)?.statusCode === 413 || (error as { message?: string })?.message?.includes('too large')) {
      return createValidationError('File too large. Maximum size is 25MB', {
        maxSize: '25MB',
        providedSize: (error as Record<string, unknown>).fileSize,
      });
    }
    
    // Handle unsupported media type
    if ((error as Record<string, unknown>)?.statusCode === 415 || (error as { message?: string })?.message?.includes('unsupported')) {
      return createValidationError('Unsupported media type. Supported formats: mp3, mp4, mpeg, mpga, m4a, wav, webm', {
        providedType: (error as Record<string, unknown>).mediaType,
      });
    }
    
    return mapSDKErrorToResponse(error);
  }
}

// Support for GET to check endpoint availability
export async function GET(_request: Request) {
  return transformSDKResponse({
    endpoint: '/v1/audio/transcriptions',
    methods: ['POST'],
    description: 'Transcribe audio into text',
    authentication: 'Virtual key required',
    models: {
      openai: ['whisper-1'],
      groq: ['whisper-large-v3'],
    },
    parameters: {
      required: ['file'],
      optional: [
        'model',
        'language',
        'prompt',
        'response_format',
        'temperature',
        'timestamp_granularities',
      ],
    },
    supportedFormats: ['mp3', 'mp4', 'mpeg', 'mpga', 'm4a', 'wav', 'webm'],
    maxFileSize: '25MB',
    responseFormats: ['json', 'text', 'srt', 'verbose_json', 'vtt'],
  });
}