import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/models/search - Search for models across all providers
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const query = searchParams.get('q') || '';
    const provider = searchParams.get('provider');
    const capability = searchParams.get('capability');
    
    const adminClient = getServerAdminClient();
    
    const filters: any = {};
    if (provider) filters.provider = provider;
    if (capability) filters.capability = capability;
    
    const results = await adminClient.providerModels.searchModels(query, filters);
    
    return NextResponse.json(results);
  } catch (error) {
    return handleSDKError(error);
  }
}