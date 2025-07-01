import { NextRequest } from 'next/server';
import { validateCoreSession, extractVirtualKey } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
// Audio API not yet supported in SDK, using direct API call
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
    
    // TODO: SDK does not yet support audio.transcribe()
    // Stub implementation until SDK adds audio transcription support
    const result = await withSDKErrorHandling(
      async () => {
        // Stub response based on format
        if (responseFormat === 'text') {
          return 'This is a stub transcription. SDK does not yet support audio transcription.';
        } else if (responseFormat === 'srt' || responseFormat === 'vtt') {
          return `1\n00:00:00,000 --> 00:00:05,000\nThis is a stub transcription. SDK does not yet support audio transcription.`;
        } else {
          // JSON response
          return {
            text: 'This is a stub transcription. SDK does not yet support audio transcription.',
            language: language || 'en',
            duration: 5.0,
            segments: [{
              id: 0,
              seek: 0,
              start: 0,
              end: 5,
              text: 'This is a stub transcription. SDK does not yet support audio transcription.',
              tokens: [],
              temperature: temperature || 0,
              avg_logprob: 0,
              compression_ratio: 1,
              no_speech_prob: 0
            }]
          };
        }
      },
      'transcribe audio'
    );

    // Handle different response formats
    if (responseFormat === 'text' || responseFormat === 'srt' || responseFormat === 'vtt') {
      return new Response(result as string, {
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
  } catch (error: any) {
    // Handle validation errors specially
    if (error.message?.includes('required')) {
      return createValidationError(error.message);
    }
    
    // Handle file size errors
    if (error.statusCode === 413 || error.message?.includes('too large')) {
      return createValidationError('File too large. Maximum size is 25MB', {
        maxSize: '25MB',
        providedSize: error.fileSize,
      });
    }
    
    // Handle unsupported media type
    if (error.statusCode === 415 || error.message?.includes('unsupported')) {
      return createValidationError('Unsupported media type. Supported formats: mp3, mp4, mpeg, mpga, m4a, wav, webm', {
        providedType: error.mediaType,
      });
    }
    
    return mapSDKErrorToResponse(error);
  }
}

// Support for GET to check endpoint availability
export async function GET(request: NextRequest) {
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