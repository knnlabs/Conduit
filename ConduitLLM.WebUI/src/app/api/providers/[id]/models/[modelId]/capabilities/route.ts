import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/providers/[id]/models/[modelId]/capabilities - Get model capabilities
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string; modelId: string }> }
) {

  try {
    const { id, modelId } = await params;
    const adminClient = getServerAdminClient();
    
    // Get provider details first
    const provider = await adminClient.providers.getById(parseInt(id, 10));
    
    // Get model capabilities
    const providerWithName = provider as { providerName?: string };
    const providerName = providerWithName.providerName ?? 'unknown';
    const capabilities = await adminClient.providerModels.getModelCapabilities(
      providerName,
      modelId
    );
    
    return NextResponse.json(capabilities);
  } catch (error) {
    return handleSDKError(error);
  }
}