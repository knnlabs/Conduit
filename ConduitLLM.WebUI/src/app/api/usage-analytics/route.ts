import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '7d';
    const adminClient = getServerAdminClient();
    
    // Get real usage analytics data from the Admin SDK
    const analytics = await adminClient.analytics.getUsageAnalytics({
      timeRange: range,
      includeTimeSeries: true,
      includeProviderBreakdown: true,
      includeModelBreakdown: true,
      includeVirtualKeyBreakdown: true,
      includeEndpointBreakdown: true,
    });
    
    return NextResponse.json(analytics);
  } catch (error) {
    return handleSDKError(error);
  }
}
