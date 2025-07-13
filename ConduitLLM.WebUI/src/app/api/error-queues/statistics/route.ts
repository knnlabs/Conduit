import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

export async function GET(request: NextRequest) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const { searchParams } = new URL(request.url);
    
    const options = {
      since: searchParams.get('since') 
        ? new Date(searchParams.get('since')!) 
        : undefined,
      groupBy: searchParams.get('groupBy') as 'hour' | 'day' | 'week' | undefined,
    };

    const result = await adminClient.errorQueues.getStatistics(options);
    
    return NextResponse.json(result);
  } catch (error: any) {
    console.error('Error fetching error queue statistics:', error);
    return NextResponse.json(
      { error: error.message || 'Failed to fetch error queue statistics' },
      { status: error.statusCode || 500 }
    );
  }
}