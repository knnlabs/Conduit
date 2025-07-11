import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/providers/[id]/models/refresh - Refresh models for a provider
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    
    // Get provider details first
    const provider = await adminClient.providers.getById(parseInt(id, 10));
    
    // Refresh models for this provider
    const models = await adminClient.providerModels.refreshProviderModels(provider.providerName);
    
    return NextResponse.json(models);
  } catch (error) {
    return handleSDKError(error);
  }
}