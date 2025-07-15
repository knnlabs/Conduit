import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/config/routing/providers - Get provider priorities
export async function GET(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    
    // Fallback to fetching providers and mapping to priorities
    try {
      const response = await adminClient.providers.list(1, 100);
      const providers = response.items || response || [];
      
      // Map providers to priority format
      const priorities = providers
        .filter((p: any) => p.isEnabled)
        .map((provider: any, index: number) => ({
          providerId: provider.id,
          providerName: provider.providerName || provider.name,
          priority: index + 1,
          weight: 100 - (index * 20), // Descending weights
          isEnabled: provider.isEnabled
        }));
      
      return NextResponse.json(priorities);
    } catch (fallbackError) {
      console.warn('Failed to fetch providers:', fallbackError);
      return NextResponse.json([]);
    }
  } catch (error) {
    console.error('Error fetching provider priorities:', error);
    return handleSDKError(error);
  }
}

// PUT /api/config/routing/providers - Update provider priorities
export async function PUT(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const providers = await req.json();
    
    // Return the requested priorities if SDK doesn't support it yet
    return NextResponse.json(providers);
  } catch (error) {
    console.error('Error updating provider priorities:', error);
    return handleSDKError(error);
  }
}