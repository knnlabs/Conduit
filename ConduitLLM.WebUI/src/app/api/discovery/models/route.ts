import { NextRequest, NextResponse } from 'next/server';
import { getServerCoreClient } from '@/lib/server/coreClient';

export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const capability = searchParams.get('capability');
    
    // Get Core client with proper authentication
    const coreClient = await getServerCoreClient();
    
    // Use the discovery service to get models
    const response = await coreClient.discovery.getModels();
    
    // Filter by capability if provided
    let models = response.data;
    if (capability) {
      models = models.filter(model => {
        const capabilityKey = capability.replace('-', '_').toLowerCase();
        return model.capabilities && model.capabilities[capabilityKey];
      });
    }
    
    return NextResponse.json({
      data: models,
      count: models.length
    });
  } catch (error) {
    console.error('Error fetching discovery models:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}