import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/model-mappings - List all model mappings
export async function GET() {

  try {
    const adminClient = getServerAdminClient();
    
    // SDK now returns an array directly (backend doesn't support pagination yet)
    const response = await adminClient.modelMappings.list();
    
    return NextResponse.json(response);
  } catch (error) {
    return handleSDKError(error);
  }
}

// POST /api/model-mappings - Create a new model mapping
export async function POST(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const body = await req.json() as {
      modelId: string;
      providerModelId: string;
      providerId: string;
      priority?: number;
      isEnabled?: boolean;
      supportsVision?: boolean;
      supportsImageGeneration?: boolean;
      supportsAudioTranscription?: boolean;
      supportsTextToSpeech?: boolean;
      supportsRealtimeAudio?: boolean;
      supportsFunctionCalling?: boolean;
      supportsStreaming?: boolean;
      supportsVideoGeneration?: boolean;
      supportsEmbeddings?: boolean;
      maxContextLength?: number;
      maxOutputTokens?: number;
      isDefault?: boolean;
      metadata?: string;
    };
    
    // Log the incoming data for debugging
    console.warn('[Model Mappings] Creating with data:', JSON.stringify(body, null, 2));
    
    // Transform frontend data to match SDK DTO expectations
    const transformedBody = {
      modelId: body.modelId,
      providerModelId: body.providerModelId,
      providerId: body.providerId, // Frontend now sends provider name directly
      priority: body.priority ?? 100,
      isEnabled: body.isEnabled ?? true, // Default to enabled
      supportsVision: body.supportsVision ?? false,
      supportsImageGeneration: body.supportsImageGeneration ?? false,
      supportsAudioTranscription: body.supportsAudioTranscription ?? false,
      supportsTextToSpeech: body.supportsTextToSpeech ?? false,
      supportsRealtimeAudio: body.supportsRealtimeAudio ?? false,
      supportsFunctionCalling: body.supportsFunctionCalling ?? false,
      supportsStreaming: body.supportsStreaming ?? false,
      supportsVideoGeneration: body.supportsVideoGeneration ?? false,
      supportsEmbeddings: body.supportsEmbeddings ?? false,
      maxContextLength: body.maxContextLength ?? undefined,
      maxOutputTokens: body.maxOutputTokens ?? undefined,
      metadata: body.metadata ?? undefined,
      isDefault: body.isDefault ?? false,
    };
    
    console.warn('[Model Mappings] Transformed data:', JSON.stringify(transformedBody, null, 2));
    
    const mapping = await adminClient.modelMappings.create(transformedBody);
    return NextResponse.json(mapping, { status: 201 });
  } catch (error) {
    console.error('[Model Mappings] Creation error:', error);
    return handleSDKError(error);
  }
}
