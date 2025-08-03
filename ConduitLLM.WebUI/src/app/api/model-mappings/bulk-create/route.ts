import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

interface BulkCreateRequest {
  models: Array<{
    modelId: string;
    displayName: string;
    providerId: string | number;
    capabilities: {
      supportsVision: boolean;
      supportsImageGeneration: boolean;
      supportsAudioTranscription: boolean;
      supportsTextToSpeech: boolean;
      supportsRealtimeAudio: boolean;
      supportsFunctionCalling: boolean;
      supportsStreaming: boolean;
      supportsVideoGeneration: boolean;
      supportsEmbeddings: boolean;
      supportsChat: boolean;
      maxContextLength?: number | null;
      maxOutputTokens?: number | null;
    };
  }>;
  defaultPriority?: number;
  enableByDefault?: boolean;
}

// POST /api/model-mappings/bulk-create - Create multiple model mappings at once
export async function POST(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const body = await req.json() as BulkCreateRequest;
    
    
    if (!body.models || body.models.length === 0) {
      return NextResponse.json(
        { error: 'No models provided' },
        { status: 400 }
      );
    }
    
    // Validate provider IDs
    for (const model of body.models) {
      const providerId = typeof model.providerId === 'number' ? model.providerId : parseInt(model.providerId, 10);
      if (isNaN(providerId) || providerId <= 0) {
        return NextResponse.json(
          { error: `Invalid provider ID for model ${model.modelId}: ${model.providerId}` },
          { status: 400 }
        );
      }
    }
    
    // Use the SDK's bulkCreate method for better performance and proper error handling
    const mappings = body.models.map(model => ({
      modelId: model.modelId,
      providerId: typeof model.providerId === 'number' ? model.providerId : parseInt(model.providerId, 10),
      providerModelId: model.modelId,
      isEnabled: body.enableByDefault ?? true,
      priority: body.defaultPriority ?? 50,
      // Include all capability flags that the SDK supports
      supportsVision: model.capabilities.supportsVision,
      supportsImageGeneration: model.capabilities.supportsImageGeneration,
      supportsAudioTranscription: model.capabilities.supportsAudioTranscription,
      supportsTextToSpeech: model.capabilities.supportsTextToSpeech,
      supportsRealtimeAudio: model.capabilities.supportsRealtimeAudio,
      supportsFunctionCalling: model.capabilities.supportsFunctionCalling,
      supportsStreaming: model.capabilities.supportsStreaming,
      supportsVideoGeneration: model.capabilities.supportsVideoGeneration,
      supportsEmbeddings: model.capabilities.supportsEmbeddings,
      supportsChat: model.capabilities.supportsChat,
      maxContextLength: model.capabilities.maxContextLength ?? undefined,
      maxOutputTokens: model.capabilities.maxOutputTokens ?? undefined,
      // isDefault is required by the SDK
      isDefault: false,
      // Notes field is not part of CreateModelProviderMappingDto
      // metadata: JSON.stringify({
      //   displayName: model.displayName,
      //   bulkImported: true,
      //   importedAt: new Date().toISOString()
      // }),
    }));
    
    // Use the SDK's bulkCreate method
    const bulkRequest = {
      mappings: mappings,
      replaceExisting: false
    };
    
    const bulkResponse = await adminClient.modelMappings.bulkCreate(bulkRequest);
    
    // Response summary available for debugging if needed
    
    // Return detailed results matching the expected format
    return NextResponse.json({
      success: true,
      created: bulkResponse.successCount,
      failed: bulkResponse.failureCount,
      details: {
        created: bulkResponse.created,
        failed: bulkResponse.errors.map((error, index) => ({
          modelId: body.models[index]?.modelId || 'unknown',
          error: error,
          providerId: body.models[index]?.providerId || 0
        }))
      }
    });
  } catch (error) {
    console.error('[Bulk Create] Error:', error);
    console.error('[Bulk Create] Error details:', {
      name: error instanceof Error ? error.name : 'Unknown',
      message: error instanceof Error ? error.message : String(error),
      stack: error instanceof Error ? error.stack : undefined
    });
    return handleSDKError(error);
  }
}