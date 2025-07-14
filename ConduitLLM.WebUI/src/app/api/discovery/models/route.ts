import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerCoreClient } from '@/lib/server/coreClient';
import { DiscoveryService } from '@knn_labs/conduit-core-client';

// GET /api/discovery/models - Get discovered models with capabilities
export async function GET(request: NextRequest) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const coreClient = await getServerCoreClient();
    const { searchParams } = new URL(request.url);
    const capability = searchParams.get('capability');
    
    // Create discovery service instance
    const discoveryService = new DiscoveryService(coreClient);
    
    // Get all models from discovery service
    const modelsResponse = await discoveryService.getModels();
    
    // Filter by capability if specified
    if (capability) {
      const filteredData = modelsResponse.data.filter((model) => {
        const capabilityKey = capability.replace(/-/g, '_'); // Convert kebab-case to snake_case
        return model.capabilities[capabilityKey as keyof typeof model.capabilities] === true;
      });
      
      return NextResponse.json({
        ...modelsResponse,
        data: filteredData,
        count: filteredData.length
      });
    }
    
    return NextResponse.json(modelsResponse);
  } catch (error) {
    return handleSDKError(error);
  }
}