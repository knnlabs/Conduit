import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const timeRange = searchParams.get('range') || '7d';
    
    const adminClient = getServerAdminClient();
    
    // Parse time range into date objects
    const now = new Date();
    const startDate = new Date();
    
    switch (timeRange) {
      case '24h':
        startDate.setHours(now.getHours() - 24);
        break;
      case '7d':
        startDate.setDate(now.getDate() - 7);
        break;
      case '30d':
        startDate.setDate(now.getDate() - 30);
        break;
      case '90d':
        startDate.setDate(now.getDate() - 90);
        break;
      default:
        startDate.setDate(now.getDate() - 7);
    }

    // Get request logs for export
    const requestLogs = await adminClient.analytics.getRequestLogs({
      startDate: startDate.toISOString(),
      endDate: now.toISOString(),
      pageSize: 10000, // Export up to 10,000 records
      page: 1
    });

    // Create CSV headers
    const headers = [
      'Timestamp',
      'Virtual Key',
      'Provider',
      'Model', 
      'Status',
      'Duration (ms)',
      'Input Tokens',
      'Output Tokens',
      'Total Tokens',
      'Cost',
      'Currency',
      'IP Address',
      'Error Message'
    ];

    // Convert request logs to CSV format
    const csvRows = [headers.join(',')];
    
    requestLogs.items.forEach((log: any) => {
      const row = [
        log.timestamp || '',
        log.virtualKeyName || '',
        log.provider || '',
        log.model || '',
        log.status || '',
        log.duration || '',
        log.inputTokens || 0,
        log.outputTokens || 0,
        (log.inputTokens || 0) + (log.outputTokens || 0),
        log.cost || 0,
        log.currency || 'USD',
        log.ipAddress || '',
        log.errorMessage ? `"${log.errorMessage.replace(/"/g, '""')}"` : '' // Escape quotes in error messages
      ];
      csvRows.push(row.join(','));
    });

    const csv = csvRows.join('\n');
    
    // Generate filename with timestamp
    const timestamp = new Date().toISOString().split('T')[0]; // YYYY-MM-DD format
    const filename = `usage-analytics-${timeRange}-${timestamp}.csv`;

    return new NextResponse(csv, {
      headers: {
        'Content-Type': 'text/csv; charset=utf-8',
        'Content-Disposition': `attachment; filename="${filename}"`,
        'Cache-Control': 'no-cache, no-store, must-revalidate',
        'Pragma': 'no-cache',
        'Expires': '0'
      },
    });

  } catch (error) {
    // Enhanced error handling for export functionality
    console.error('Export failed:', error);
    
    // Get timeRange from URL params in case of error
    const { searchParams } = new URL(req.url);
    const errorTimeRange = searchParams.get('range') || '7d';
    
    // Try to provide a fallback export with error information
    const errorCsv = `Error,Message,Timestamp
Export Failed,"${error instanceof Error ? error.message.replace(/"/g, '""') : 'Unknown error occurred'}","${new Date().toISOString()}"
Note,"Please try again or contact support if the problem persists",""
Range,"${errorTimeRange}",""`;

    // Return error CSV instead of JSON error for download consistency
    return new NextResponse(errorCsv, {
      headers: {
        'Content-Type': 'text/csv; charset=utf-8',
        'Content-Disposition': `attachment; filename="usage-analytics-error-${errorTimeRange}-${new Date().toISOString().split('T')[0]}.csv"`,
        'X-Export-Error': 'true',
        'X-Error-Message': error instanceof Error ? error.message : 'Export failed'
      },
      status: 200 // Use 200 so the download still works
    });
  }
}