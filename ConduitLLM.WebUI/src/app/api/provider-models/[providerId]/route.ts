import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/provider-models/[providerId] - Get available models for a specific provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ providerId: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { providerId } = await params;
    const adminClient = getServerAdminClient();
    
    console.log('[Provider Models] Fetching models for provider ID:', providerId);
    
    // First get the provider details to get the provider name
    const provider = await adminClient.providers.getById(parseInt(providerId, 10));
    console.log('[Provider Models] Provider details:', provider);
    
    // Get models for this provider using the provider name
    const models = await adminClient.providerModels.getProviderModels(provider.providerName);
    console.log('[Provider Models] Found models:', models?.length || 0);
    
    return NextResponse.json(models);
  } catch (error) {
    console.error('[Provider Models] Error:', error);
    return handleSDKError(error);
  }
}