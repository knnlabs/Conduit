import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { getProviderTypeFromDto } from '@/lib/utils/providerTypeUtils';

// GET /api/providers/[id]/models - Get available models for a specific provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    
    // Get provider details first
    const provider = await adminClient.providers.getById(parseInt(id, 10));
    
    // Get the provider type
    const providerType = getProviderTypeFromDto(provider);
    
    // Get models for this provider
    const models = await adminClient.providerModels.getProviderModels(providerType);
    
    return NextResponse.json(models);
  } catch (error) {
    return handleSDKError(error);
  }
}
