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
        
        // Log the data we're about to send
        const createData = {
          modelId: model.modelId,
          providerId: model.providerId,
          providerModelId: model.modelId,
          isEnabled: body.enableByDefault ?? true,
          priority: body.defaultPriority ?? 50,
          // Include only capability flags that the SDK supports
          supportsVision: model.capabilities.supportsVision,
          supportsImageGeneration: model.capabilities.supportsImageGeneration,
          supportsAudioTranscription: model.capabilities.supportsAudioTranscription,
          supportsTextToSpeech: model.capabilities.supportsTextToSpeech,
          supportsRealtimeAudio: model.capabilities.supportsRealtimeAudio,
          supportsVideoGeneration: model.capabilities.supportsVideoGeneration,
          maxContextLength: model.capabilities.maxContextLength || undefined,
          // Map unsupported capabilities to the capabilities string field
          capabilities: [
            model.capabilities.supportsFunctionCalling && 'function-calling',
            model.capabilities.supportsStreaming && 'streaming',
            model.capabilities.supportsEmbeddings && 'embeddings'
          ].filter(Boolean).join(',') || undefined,
          // isDefault is required by the SDK
          isDefault: false,
          // Store additional metadata in notes
          notes: JSON.stringify({
            displayName: model.displayName,
            maxOutputTokens: model.capabilities.maxOutputTokens,
            bulkImported: true,
            importedAt: new Date().toISOString()
          }),
        };
        
        console.log('[Bulk Create] Creating mapping with full data:', JSON.stringify(createData, null, 2));
        
        // Use the regular create method with full data
        const mapping = await adminClient.modelMappings.create(createData);
        
        results.created.push(mapping);
      } catch (error: any) {
        console.error(`[Bulk Create] Failed to create mapping for ${model.modelId}:`, error);
        console.error('[Bulk Create] Error details:', {
          modelId: model.modelId,
          providerId: model.providerId,
          errorMessage: error.message,
          errorStack: error.stack,
          errorResponse: error.response,
          errorContext: error.context,
          errorDetails: error.details,
          fullError: JSON.stringify(error, null, 2),
        });
        
        // Extract more detailed error message
        let errorMessage = 'Unknown error';
        if (error.response?.data?.message) {
          errorMessage = error.response.data.message;
        } else if (error.response?.data) {
          errorMessage = typeof error.response.data === 'string' ? error.response.data : JSON.stringify(error.response.data);
        } else if (error.details) {
          errorMessage = error.details;
        } else if (error.message) {
          errorMessage = error.message;
        }
        
        results.failed.push({
          modelId: model.modelId,
          error: errorMessage,
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