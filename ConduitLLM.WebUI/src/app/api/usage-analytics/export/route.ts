import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '7d';
    const adminClient = getServerAdminClient();
    
    // Use the Admin SDK to export usage analytics
    const exportResult = await adminClient.analytics.exportUsageAnalytics({ 
      format: 'csv',
    });
    
    // Fetch the exported data from the URL
    const response = await fetch(exportResult.url);
    const exportData = await response.text();

    return new NextResponse(exportData, {
      headers: {
        'Content-Type': 'text/csv',
        'Content-Disposition': `attachment; filename="usage-analytics-${range}-${new Date().toISOString()}.csv"`,
      },
    });
  } catch (error) {
    return handleSDKError(error);
  }
}
