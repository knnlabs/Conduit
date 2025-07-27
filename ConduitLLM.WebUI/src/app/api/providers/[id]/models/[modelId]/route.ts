import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { getProviderTypeFromDto } from '@/lib/utils/providerTypeUtils';

// GET /api/providers/[id]/models/[modelId] - Get details for a specific model
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string; modelId: string }> }
) {

  try {
    const { id, modelId } = await params;
    const adminClient = getServerAdminClient();
    
    // Get provider details first
    const provider = await adminClient.providers.getById(parseInt(id, 10));
    
    // Get the provider type
    const providerType = getProviderTypeFromDto(provider);
    
    // Get model details
    const modelDetails = await adminClient.providerModels.getModelDetails(
      providerType,
      modelId
    );
    
    return NextResponse.json(modelDetails);
  } catch (error) {
    return handleSDKError(error);
  }
}