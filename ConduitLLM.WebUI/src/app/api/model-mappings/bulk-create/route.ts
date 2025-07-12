import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

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
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const body: BulkCreateRequest = await req.json();
    
    if (!body.models || body.models.length === 0) {
      return NextResponse.json(
        { error: 'No models provided' },
        { status: 400 }
      );
    }
    
    console.log(`[Bulk Create] Creating ${body.models.length} model mappings`);
    
    // Create mappings one by one using the SDK since bulk create has issues
    const results: {
      created: any[];
      failed: Array<{ modelId: string; error: string }>;
    } = {
      created: [],
      failed: []
    };
    
    for (const model of body.models) {
      try {
        // Prepare all capabilities and metadata
        const metadata = {
          displayName: model.displayName,
          capabilities: {
            supportsVision: model.capabilities.supportsVision,
            supportsImageGeneration: model.capabilities.supportsImageGeneration,
            supportsAudioTranscription: model.capabilities.supportsAudioTranscription,
            supportsTextToSpeech: model.capabilities.supportsTextToSpeech,
            supportsRealtimeAudio: model.capabilities.supportsRealtimeAudio,
            supportsFunctionCalling: model.capabilities.supportsFunctionCalling,
            supportsStreaming: model.capabilities.supportsStreaming,
            supportsVideoGeneration: model.capabilities.supportsVideoGeneration,
            supportsEmbeddings: model.capabilities.supportsEmbeddings,
            maxContextLength: model.capabilities.maxContextLength,
            maxOutputTokens: model.capabilities.maxOutputTokens,
          }
        };
        
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
          maxContextLength: model.capabilities.maxContextLength || undefined,
          maxOutputTokens: model.capabilities.maxOutputTokens || undefined,
          // Store additional data in metadata
          metadata: JSON.stringify(metadata),
        };
        
        console.log('[Bulk Create] Creating mapping:', {
          modelId: createData.modelId,
          providerId: createData.providerId,
          providerModelId: createData.providerModelId,
        });
        
        // Use the regular create method with full data
        const mapping = await adminClient.modelMappings.create(createData);
        
        results.created.push(mapping);
      } catch (error: any) {
        console.error(`[Bulk Create] Failed to create mapping for ${model.modelId}:`, error);
        console.error('[Bulk Create] Error details:', {
          modelId: model.modelId,
          providerId: model.providerId,
          errorContext: error.context,
          errorDetails: error.details,
        });
        results.failed.push({
          modelId: model.modelId,
          error: error.details || error.message || 'Unknown error',
        });
      }
    }
    
    console.log(`[Bulk Create] Created: ${results.created.length}, Failed: ${results.failed.length}`);
    
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