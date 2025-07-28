import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { providerNameToType, providerTypeToName } from '@/lib/utils/providerTypeUtils';
import type { ProviderCredentialDto } from '@knn_labs/conduit-admin-client';

// POST /api/model-mappings/discover - Bulk discover model mappings
export async function POST(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const body = await req.json() as { autoCreate?: boolean; enableNewMappings?: boolean };
    
    // Call discoverModels to get all available models
    const discoveredModels = await adminClient.modelMappings.discoverModels();
    
    // Get all providers to map provider names to IDs
    const providersResponse = await adminClient.providers.list(1, 100);
    const providers: ProviderCredentialDto[] = Array.isArray(providersResponse) 
      ? providersResponse 
      : (providersResponse.items ?? []);
    const providerNameToId = new Map<string, number>();
    
    // Build a map of provider names to IDs
    for (const provider of providers) {
      // Convert provider type enum to string name
      const providerName = providerTypeToName(provider.providerType);
      providerNameToId.set(providerName.toLowerCase(), provider.id);
      
      // Also map the numeric value as a string for fallback
      providerNameToId.set(String(provider.providerType), provider.id);
    }
    
    // Also map discovered provider names to IDs
    for (const model of discoveredModels) {
      if (model.provider) {
        const normalizedProviderName = model.provider.toLowerCase();
        if (!providerNameToId.has(normalizedProviderName)) {
          // Try to match by provider type
          try {
            const providerType = providerNameToType(model.provider);
            const matchingProvider = providers.find(p => p.providerType === providerType);
            if (matchingProvider) {
              providerNameToId.set(normalizedProviderName, matchingProvider.id);
            }
          } catch {
            // If provider type conversion fails, just skip
            console.warn(`[Discover] Could not map provider name: ${model.provider}`);
          }
        }
      }
    }
    
    // If autoCreate is true, create mappings for discovered models
    if (body.autoCreate && discoveredModels.length > 0) {
      const results = [];
      for (const model of discoveredModels) {
        try {
          // Get provider ID from the map
          const providerId = providerNameToId.get(model.provider.toLowerCase());
          if (!providerId) {
            console.warn(`[Discover] Provider ID not found for provider: ${model.provider}`);
            results.push({ ...model, created: false, error: 'Provider not found' });
            continue;
          }
          
          // Create a mapping for each discovered model with capability flags
          const mapping = {
            modelId: model.modelId,
            providerId: providerId, // Use numeric provider ID
            providerModelId: model.modelId,
            isEnabled: body.enableNewMappings ?? false,
            priority: 50,
            // Map capabilities from discovered model
            supportsVision: model.capabilities?.vision ?? false,
            supportsImageGeneration: model.capabilities?.imageGeneration ?? false,
            supportsAudioTranscription: false, // Not in discovered capabilities
            supportsTextToSpeech: false, // Not in discovered capabilities
            supportsRealtimeAudio: false, // Not in discovered capabilities
            supportsFunctionCalling: model.capabilities?.functionCalling ?? false,
            supportsStreaming: model.capabilities?.chatStream ?? false,
            supportsVideoGeneration: model.capabilities?.videoGeneration ?? false,
            supportsEmbeddings: model.capabilities?.embeddings ?? false,
            maxContextLength: model.capabilities?.maxTokens,
            maxOutputTokens: model.capabilities?.maxOutputTokens,
            isDefault: false,
          };
          await adminClient.modelMappings.create(mapping);
          results.push({ ...model, created: true });
        } catch {
          // If mapping already exists or fails, just mark as not created
          results.push({ ...model, created: false });
        }
      }
      return NextResponse.json(results);
    } else {
      // Just return discovered models without creating
      return NextResponse.json(discoveredModels);
    }
  } catch (error) {
    return handleSDKError(error);
  }
}
