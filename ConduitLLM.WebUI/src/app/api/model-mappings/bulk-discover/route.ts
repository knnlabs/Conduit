import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

// POST /api/model-mappings/bulk-discover - Discover models from a specific provider with capabilities
export async function POST(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const { providerId, providerName } = await req.json();
    
    if (!providerId || !providerName) {
      return NextResponse.json(
        { error: 'Provider ID and name are required' },
        { status: 400 }
      );
    }
    
    console.warn('[Bulk Discover] Starting discovery for provider:', providerName);
    
    // Discover all models from the provider
    const discoveredModels = await adminClient.modelMappings.discoverProviderModels(providerName);
    
    // Get existing mappings to check for conflicts
    const existingMappingsResponse = await adminClient.modelMappings.list();
    let existingMappings: ModelProviderMappingDto[] = [];
if (Array.isArray(existingMappingsResponse)) {
  existingMappings = existingMappingsResponse;
} else if (
  typeof existingMappingsResponse === 'object' &&
  existingMappingsResponse !== null &&
  'items' in existingMappingsResponse &&
  Array.isArray((existingMappingsResponse as { items: unknown }).items)
) {
  existingMappings = (existingMappingsResponse as { items: ModelProviderMappingDto[] }).items ?? [];
}
    
    // Create a set of existing model IDs for quick lookup
    const existingModelIds = new Set(existingMappings.map((m: ModelProviderMappingDto) => m.modelId));
    
    // Enhance discovered models with conflict information
    const enhancedModels = discoveredModels.map(model => ({
      ...model,
      providerId: providerName, // Backend expects provider name, not numeric ID
      hasConflict: existingModelIds.has(model.modelId),
      existingMapping: existingMappings.find((m: ModelProviderMappingDto) => m.modelId === model.modelId) ?? null,
      // Map capabilities to frontend expected format
      capabilities: {
        supportsVision: model.capabilities?.vision ?? false,
        supportsImageGeneration: model.capabilities?.imageGeneration ?? false,
        supportsAudioTranscription: false, // Not in discovered capabilities
        supportsTextToSpeech: false, // Not in discovered capabilities
        supportsRealtimeAudio: false, // Not in discovered capabilities
        supportsFunctionCalling: model.capabilities?.functionCalling ?? false,
        supportsStreaming: model.capabilities?.chatStream ?? false,
        supportsVideoGeneration: model.capabilities?.videoGeneration ?? false,
        supportsEmbeddings: model.capabilities?.embeddings ?? false,
        maxContextLength: model.capabilities?.maxTokens ?? null,
        maxOutputTokens: model.capabilities?.maxOutputTokens ?? null,
      }
    }));
    
    console.warn(`[Bulk Discover] Found ${enhancedModels.length} models, ${enhancedModels.filter(m => m.hasConflict).length} have conflicts`);
    
    return NextResponse.json({
      providerId,
      providerName,
      models: enhancedModels,
      totalModels: enhancedModels.length,
      conflictCount: enhancedModels.filter(m => m.hasConflict).length
    });
  } catch (error) {
    console.error('[Bulk Discover] Error:', error);
    return handleSDKError(error);
  }
}