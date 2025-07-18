import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
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
    
    if (!body.models || body.models.length === 0) {
      return NextResponse.json(
        { error: 'No models provided' },
        { status: 400 }
      );
    }
    
    
    // Create mappings one by one using the SDK since bulk create has issues
    interface MappingResult {
      modelId: string;
      error: string;
    }
    
    const results: {
      created: unknown[];
      failed: MappingResult[];
    } = {
      created: [],
      failed: []
    };
    
    for (const model of body.models) {
      try {
        
        // Log the data we're about to send
        const createData = {
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
          supportsFunctionCalling: model.capabilities.supportsFunctionCalling,
          supportsStreaming: model.capabilities.supportsStreaming,
          supportsVideoGeneration: model.capabilities.supportsVideoGeneration,
          supportsEmbeddings: model.capabilities.supportsEmbeddings,
          maxContextLength: model.capabilities.maxContextLength ?? undefined,
          maxOutputTokens: model.capabilities.maxOutputTokens ?? undefined,
          // Don't set capabilities string - let backend derive from boolean flags
          // isDefault is required by the SDK
          isDefault: false,
          // Store additional metadata in notes
          metadata: JSON.stringify({
            displayName: model.displayName,
            bulkImported: true,
            importedAt: new Date().toISOString()
          }),
        };
        
        
        // Use the regular create method with full data
        const mapping = await adminClient.modelMappings.create(createData);
        
        results.created.push(mapping);
      } catch (error: unknown) {
        
        // Extract more detailed error message
        let errorMessage = 'Unknown error';
        if (error instanceof Error) {
          errorMessage = error.message;
        } else if (typeof error === 'string') {
          errorMessage = error;
        }
        
        results.failed.push({
          modelId: model.modelId,
          error: errorMessage,
        });
      }
    }
    
    
    // Return detailed results
    return NextResponse.json({
      success: true,
      created: results.created.length,
      failed: results.failed.length,
      details: {
        created: results.created,
        failed: results.failed
      }
    });
  } catch (error) {
    console.error('[Bulk Create] Error:', error);
    return handleSDKError(error);
  }
}