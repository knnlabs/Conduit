import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// POST /api/model-mappings/discover - Bulk discover model mappings
export async function POST(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const body = await req.json();
    
    // Call discoverModels to get all available models
    const discoveredModels = await adminClient.modelMappings.discoverModels();
    
    // If autoCreate is true, create mappings for discovered models
    if (body.autoCreate && discoveredModels.length > 0) {
      const results = [];
      for (const model of discoveredModels) {
        try {
          // Create a mapping for each discovered model with capability flags
          const mapping = {
            modelId: model.modelId,
            providerId: model.provider,
            providerModelId: model.modelId,
            isEnabled: body.enableNewMappings ?? false,
            priority: 50,
            // Map capabilities from discovered model
            supportsVision: model.capabilities?.vision ?? false,
            supportsImageGeneration: model.capabilities?.imageGeneration ?? false,
            supportsFunctionCalling: model.capabilities?.functionCalling ?? false,
            supportsStreaming: model.capabilities?.chatStream ?? false,
            maxContextLength: model.capabilities?.maxTokens,
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
