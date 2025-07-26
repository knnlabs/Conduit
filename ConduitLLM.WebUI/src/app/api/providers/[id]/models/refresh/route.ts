import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
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
    
    // Refresh models for this provider
    const providerWithName = provider as { providerName?: string };
    const models = await adminClient.providerModels.refreshProviderModels(providerWithName.providerName ?? '');
    
    return NextResponse.json(models);
  } catch (error) {
    return handleSDKError(error);
  }
}