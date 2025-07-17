import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const startDate = searchParams.get('startDate') || undefined;
    const endDate = searchParams.get('endDate') || undefined;
    const groupBy = searchParams.get('groupBy') as 'provider' | 'model' | undefined;

    console.log('[ModelCosts] Overview request:', { startDate, endDate, groupBy });

    const adminClient = getServerAdminClient();
    const response = await adminClient.modelCosts.getOverview({
      startDate,
      endDate,
      groupBy,
    });

    return NextResponse.json(response);
  } catch (error) {
    console.error('[ModelCosts] Overview error:', error);
    return handleSDKError(error);
  }
}