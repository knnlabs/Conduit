import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { BulkMappingRequest, CreateModelProviderMappingDto } from '@knn_labs/conduit-admin-client';
interface BulkCreateRequest {
  models: Array<{
    modelId: string;
    displayName: string;
    providerId: string;
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
    
    console.warn('[Bulk Create] Request received with', body.models?.length || 0, 'models');
    
    if (!body.models || body.models.length === 0) {
      return NextResponse.json(
        { error: 'No models provided' },
        { status: 400 }
      );
    }
    
    
    // Use the SDK's bulkCreate method for better performance and proper error handling
    const mappings = body.models.map(model => ({
      modelId: model.modelId,
      providerId: model.providerId,
      providerModelId: model.modelId,
      isEnabled: body.enableByDefault ?? true,
      priority: body.defaultPriority ?? 50,
      // Include all capability flags that the SDK supports
      supportsVision: model.capabilities.supportsVision,
      supportsImageGeneration: model.capabilities.supportsImageGeneration,
      supportsAudioTranscription: model.capabilities.supportsAudioTranscription,
      supportsTextToSpeech: model.capabilities.supportsTextToSpeech,
      supportsRealtimeAudio: model.capabilities.supportsRealtimeAudio,
      supportsFunctionCalling: model.capabilities.supportsFunctionCalling || false,
      supportsStreaming: model.capabilities.supportsStreaming,
      supportsVideoGeneration: model.capabilities.supportsVideoGeneration,
      supportsEmbeddings: model.capabilities.supportsEmbeddings || false,
      maxContextLength: model.capabilities.maxContextLength ?? undefined,
      maxOutputTokens: model.capabilities.maxOutputTokens ?? undefined,
      // isDefault is required by the SDK
      isDefault: false,
      // Use 'notes' instead of 'metadata' as per the generated types
      notes: JSON.stringify({
        displayName: model.displayName,
        bulkImported: true,
        importedAt: new Date().toISOString()
      }),
    }));
    
    // Use the SDK's bulkCreate method with proper typing
    const bulkRequest: BulkMappingRequest = {
      mappings: mappings as CreateModelProviderMappingDto[],
      replaceExisting: false
    };
    
    console.warn('[Bulk Create] Calling SDK bulkCreate with', mappings.length, 'mappings');
    console.warn('[Bulk Create] First mapping:', JSON.stringify(mappings[0], null, 2));
    
    const bulkResponse = await adminClient.modelMappings.bulkCreate(bulkRequest);
    
    console.warn('[Bulk Create] Response received:', {
      created: bulkResponse.created.length,
      failed: bulkResponse.failed.length,
      failedDetails: bulkResponse.failed
    });
    
    // Return detailed results matching the expected format
    return NextResponse.json({
      success: true,
      created: bulkResponse.created.length,
      failed: bulkResponse.failed.length,
      details: {
        created: bulkResponse.created,
        failed: bulkResponse.failed.map(f => ({
          modelId: f.mapping.modelId,
          error: f.error || 'Unknown error'
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