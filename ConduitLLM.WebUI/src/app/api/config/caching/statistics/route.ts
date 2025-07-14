import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/config/caching/statistics - Get cache statistics
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    
    // Get region ID from query params if provided
    const searchParams = req.nextUrl.searchParams;
    const regionId = searchParams.get('regionId');
    
    // Get cache statistics
    const statistics = await adminClient.configuration.getCacheStatistics();
    
    // If a specific region is requested, filter the response
    if (regionId) {
      return NextResponse.json({
        regionId,
        statistics: statistics,
      });
    }
    
    return NextResponse.json(statistics);
  } catch (error) {
    return handleSDKError(error);
  }
}