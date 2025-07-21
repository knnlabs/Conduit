import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/providers/[id]/models - Get available models for a specific provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    
    // Get models for this provider using the provider ID directly
    const models = await adminClient.providerModels.getProviderModels(id);
    
    return NextResponse.json(models);
  } catch (error) {
    return handleSDKError(error);
  }
}
