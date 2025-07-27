import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { getProviderTypeFromDto } from '@/lib/utils/providerTypeUtils';
// GET /api/provider-models/[providerId] - Get available models for a specific provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ providerId: string }> }
) {

  try {
    const { providerId } = await params;
    const adminClient = getServerAdminClient();
    
    console.error('[Provider Models] Fetching models for provider ID:', providerId);
    
    // First get the provider details to get the provider type
    const provider = await adminClient.providers.getById(parseInt(providerId, 10));
    console.error('[Provider Models] Provider details:', provider);
    
    // Get the provider type
    const providerType = getProviderTypeFromDto(provider);
    
    // Get models for this provider using the provider type
    const models = await adminClient.providerModels.getProviderModels(providerType);
    console.error('[Provider Models] Found models:', models?.length || 0);
    
    return NextResponse.json(models);
  } catch (error) {
    console.error('[Provider Models] Error:', error);
    return handleSDKError(error);
  }
}