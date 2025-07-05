import { createCoreRoute } from '@/lib/utils/core-route-helpers';
import { validateAudioTranscriptionRequest } from '@/lib/utils/core-route-validators';
import { withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { getServerCoreClient } from '@/lib/clients/server';

export const POST = createCoreRoute(
  {
    requireVirtualKey: false,
    parseAsFormData: true,
    validateFormData: validateAudioTranscriptionRequest,
    logContext: 'audio_transcriptions'
  },
  async ({ virtualKey }, formData: FormData) => {
    // Extract audio file
    const audioFile = formData.get('file') as File;
    
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
  }
);

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