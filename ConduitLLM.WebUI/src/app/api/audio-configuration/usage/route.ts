import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/audio-configuration/usage - Get audio usage data
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
    
    if (searchParams.has('startDate')) {
      filters.startDate = searchParams.get('startDate');
    }
    if (searchParams.has('endDate')) {
      filters.endDate = searchParams.get('endDate');
    }
    if (searchParams.has('providerId')) {
      filters.providerId = searchParams.get('providerId');
    }
    if (searchParams.has('virtualKeyId')) {
      filters.virtualKeyId = searchParams.get('virtualKeyId');
    }
    if (searchParams.has('operationType')) {
      filters.operationType = searchParams.get('operationType');
    }
    
    const page = parseInt(searchParams.get('page') || '1');
    const pageSize = parseInt(searchParams.get('pageSize') || '20');
    
    // Add pagination to filters
    filters.page = page;
    filters.pageSize = pageSize;
    
    const usage = await adminClient.audio.getUsage(filters);
    
    return NextResponse.json(usage);
  } catch (error) {
    console.error('Error fetching audio usage:', error);
    return handleSDKError(error);
  }
}