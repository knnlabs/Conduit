import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const since = searchParams.get('since') ? new Date(searchParams.get('since') ?? '') : undefined;
    const groupBy = searchParams.get('groupBy') as 'hour' | 'day' | 'week' | undefined;

    const adminClient = getServerAdminClient();
    const response = await adminClient.errorQueues.getStatistics({
      since,
      groupBy,
    });
    
    return NextResponse.json(response);
  } catch (error) {
    return handleSDKError(error);
  }
}