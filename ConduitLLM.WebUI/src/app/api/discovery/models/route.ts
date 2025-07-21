import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';
import type { ModelsDiscoveryResponse, DiscoveredModel } from '@knn_labs/conduit-core-client';

// GET /api/discovery/models - Get discovered models with capabilities
export async function GET(request: NextRequest) {

  try {
    const coreClient = await getServerCoreClient();
    const { searchParams } = new URL(request.url);
    const capability = searchParams.get('capability');
    
    // Use the discovery service from the Core SDK
    const modelsResponse = await (coreClient.discovery.getModels as () => Promise<ModelsDiscoveryResponse>)();
    
    // Filter by capability if specified
    if (capability) {
      const filteredData = modelsResponse.data.filter((model: DiscoveredModel) => {
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