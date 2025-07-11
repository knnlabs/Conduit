import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/model-mappings/discover - Bulk discover model mappings
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

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
          // Create a mapping for each discovered model
          const mapping = {
            modelId: model.modelId,
            providerId: model.provider,
            providerModelId: model.modelId,
            isEnabled: body.enableNewMappings || false,
            priority: 50,
          };
          const created = await adminClient.modelMappings.create(mapping);
          results.push({ ...model, created: true });
        } catch (err) {
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
