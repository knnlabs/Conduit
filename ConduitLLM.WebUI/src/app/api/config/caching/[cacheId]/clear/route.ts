import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// POST /api/config/caching/[cacheId]/clear - Clear a specific cache
export async function POST(
  req: NextRequest,
  context: { params: Promise<{ cacheId: string }> }
) {

  try {
    const adminClient = getServerAdminClient();
    const { cacheId } = await context.params;
    
    // Clear the cache using the SDK method
    await (adminClient.configuration.clearCacheByRegion as (cacheId: string) => Promise<unknown>)(cacheId);
    
    return NextResponse.json({ 
      success: true, 
      message: `Cache ${cacheId} cleared successfully` 
    });
  } catch (error) {
    return handleSDKError(error);
  }
}