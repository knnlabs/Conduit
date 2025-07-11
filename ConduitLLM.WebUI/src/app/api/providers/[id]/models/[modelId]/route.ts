import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/providers/[id]/models/[modelId] - Get details for a specific model
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string; modelId: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id, modelId } = await params;
    const adminClient = getServerAdminClient();
    
    // Get provider details first
    const provider = await adminClient.providers.getById(parseInt(id, 10));
    
    // Get model details
    const modelDetails = await adminClient.providerModels.getModelDetails(
      provider.providerName,
      modelId
    );
    
    return NextResponse.json(modelDetails);
  } catch (error) {
    return handleSDKError(error);
  }
}