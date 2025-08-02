import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/model-mappings - List all model mappings
export async function GET() {

  try {
    const adminClient = getServerAdminClient();
    
    // SDK now returns an array directly (backend doesn't support pagination yet)
    const response = await adminClient.modelMappings.list();
    
    // Response sample available for debugging if needed
    
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
      providerId: number;
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
    
    // Validate required fields
    if (!body.modelId || !body.providerModelId || !body.providerId) {
      return NextResponse.json(
        { error: 'Missing required fields: modelId, providerModelId, and providerId are required' },
        { status: 400 }
      );
    }
    
    // Pass data directly to SDK - providerId is already a number from the frontend
    const transformedBody = {
      modelId: body.modelId,
      providerModelId: body.providerModelId,
      providerId: body.providerId, // Already a number from frontend
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
    
    
    const mapping = await adminClient.modelMappings.create(transformedBody);
    return NextResponse.json(mapping, { status: 201 });
  } catch (error) {
    console.error('[Model Mappings] Creation error:', error);
    return handleSDKError(error);
  }
}
