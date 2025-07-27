import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { getProviderTypeFromDto } from '@/lib/utils/providerTypeUtils';

// POST /api/providers/[id]/models/refresh - Refresh models for a provider
export async function POST(
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
    
    // Refresh models for this provider
    const models = await adminClient.providerModels.refreshProviderModels(providerType);
    
    return NextResponse.json(models);
  } catch (error) {
    return handleSDKError(error);
  }
}