import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/audio-configuration/usage/summary - Get audio usage summary
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const searchParams = req.nextUrl.searchParams;
    
    // Build filters from query params
    const filters: any = {};
    
    // Default to last 30 days if no dates provided
    if (searchParams.has('startDate')) {
      filters.startDate = searchParams.get('startDate');
    } else {
      filters.startDate = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString();
    }
    
    if (searchParams.has('endDate')) {
      filters.endDate = searchParams.get('endDate');
    } else {
      filters.endDate = new Date().toISOString();
    }
    if (searchParams.has('providerId')) {
      filters.providerId = searchParams.get('providerId');
    }
    if (searchParams.has('virtualKeyId')) {
      filters.virtualKeyId = searchParams.get('virtualKeyId');
    }
    if (searchParams.has('groupBy')) {
      filters.groupBy = searchParams.getAll('groupBy');
    }
    
    const summary = await adminClient.audio.getUsageSummary(filters);
    
    return NextResponse.json(summary);
  } catch (error) {
    console.error('Error fetching audio usage summary:', error);
    return handleSDKError(error);
  }
}