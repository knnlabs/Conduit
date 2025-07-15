import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// POST /api/config/caching/[cacheId]/refresh - Refresh a specific cache
export async function POST(
  req: NextRequest,
  context: { params: Promise<{ cacheId: string }> }
) {

  try {
    const adminClient = getServerAdminClient();
    const { cacheId } = await context.params;
    
    // For now, clear the cache as a refresh mechanism
    // In the future, this would call a specific refresh endpoint
    await adminClient.configuration.clearCacheByRegion(cacheId);
    
    return NextResponse.json({ 
      success: true, 
      message: `Cache ${cacheId} refreshed successfully` 
    });
  } catch (error) {
    return handleSDKError(error);
  }
}