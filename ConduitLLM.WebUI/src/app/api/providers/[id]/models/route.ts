import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/providers/[id]/models - Get available models for a specific provider
export async function GET(
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
    
    // Get models for this provider using the provider ID directly
    const models = await adminClient.providerModels.getProviderModels(id);
    
    return NextResponse.json(models);
  } catch (error) {
    return handleSDKError(error);
  }
}
