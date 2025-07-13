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
      includeEmpty: searchParams.get('includeEmpty') === 'true',
      minMessages: searchParams.get('minMessages') 
        ? parseInt(searchParams.get('minMessages')!) 
        : undefined,
      queueNameFilter: searchParams.get('queueNameFilter') || undefined,
    };

    const result = await adminClient.errorQueues.getErrorQueues(options);
    
    return NextResponse.json(result);
  } catch (error: any) {
    console.error('Error fetching error queues:', error);
    return NextResponse.json(
      { error: error.message || 'Failed to fetch error queues' },
      { status: error.statusCode || 500 }
    );
  }
}