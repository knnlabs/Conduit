import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '7d';
    const keys = searchParams.get('keys')?.split(',').filter(Boolean);
    const adminClient = getServerAdminClient();
    
    // Calculate date range from time range parameter
    const now = new Date();
    const ranges = {
      '24h': 1,
      '7d': 7,
      '30d': 30,
      '90d': 90,
    };
    const days = ranges[range as keyof typeof ranges] || 7;
    const startDate = new Date(now.getTime() - days * 24 * 60 * 60 * 1000).toISOString();
    const endDate = now.toISOString();

    // Use the Admin SDK export functionality with correct parameters
    const exportResult = await adminClient.analytics.exportVirtualKeyAnalytics({
      format: 'csv',
      startDate,
      endDate,
      filters: {
        virtualKeyIds: keys,
      },
    });
    
    // Fetch the CSV data from the export URL
    if (exportResult.url) {
      const response = await fetch(exportResult.url);
      const csvData = await response.text();
      
      return new NextResponse(csvData, {
        headers: {
          'Content-Type': 'text/csv',
          'Content-Disposition': `attachment; filename="virtual-keys-analytics-${range}-${new Date().toISOString()}.csv"`,
        },
      });
    } else {
      throw new Error('Export URL not available');
    }
  } catch (error) {
    console.error('Error exporting virtual key analytics:', error);
    
    // Re-extract parameters for error response since they're in try block scope
    const { searchParams } = new URL(req.url);
    const range = searchParams.get('range') || '7d';
    const keys = searchParams.get('keys')?.split(',').filter(Boolean);
    
    // If export fails, return a minimal CSV with error message
    const errorCsv = `Virtual Key Analytics Export Error
Generated: ${new Date().toISOString()}
Time Range: ${range}
${keys && keys.length > 0 ? `Requested Keys: ${keys.join(', ')}` : 'All Keys'}

Error: Failed to export analytics data from the Admin API.
Please ensure the Admin API is configured correctly and try again.

If this error persists, contact your system administrator.`;

    return new NextResponse(errorCsv, {
      headers: {
        'Content-Type': 'text/csv',
        'Content-Disposition': `attachment; filename="virtual-keys-analytics-error-${new Date().toISOString()}.csv"`,
      },
    });
  }
}
