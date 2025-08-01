import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerCoreClient } from '@/lib/server/coreClient';

// GET /api/provider-models/[providerId] - Get available models for a specific provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ providerId: string }> }
) {
  try {
    const { providerId } = await params;
    const providerIdNum = parseInt(providerId, 10);
    
    if (isNaN(providerIdNum)) {
      return NextResponse.json({ error: 'Invalid provider ID' }, { status: 400 });
    }
    
    console.error('[Provider Models] Fetching models for provider ID:', providerIdNum);
    
    // Use the Core client to get provider models (this is a Core API function)
    const coreClient = await getServerCoreClient();
    
    if (!coreClient.providerModels || typeof coreClient.providerModels.getProviderModels !== 'function') {
      throw new Error('Provider models service not available');
    }
    
    // Get models for this provider using the provider ID
    const models = await coreClient.providerModels.getProviderModels(providerIdNum);
    console.error('[Provider Models] Found models:', models?.length || 0);
    
    return NextResponse.json(models);
  } catch (error) {
    console.error('[Provider Models] Error:', error);
    return handleSDKError(error);
  }
}