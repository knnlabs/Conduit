import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { getProviderTypeFromDto, providerTypeToName } from '@/lib/utils/providerTypeUtils';
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
    
    // Get the provider type and convert to name for the API call
    const providerType = getProviderTypeFromDto(provider);
    const providerName = providerTypeToName(providerType);
    
    // Get models for this provider using the provider name
    const models = await adminClient.providerModels.getProviderModels(providerName);
    console.error('[Provider Models] Found models:', models?.length || 0);
    
    return NextResponse.json(models);
  } catch (error) {
    console.error('[Provider Models] Error:', error);
    return handleSDKError(error);
  }
}