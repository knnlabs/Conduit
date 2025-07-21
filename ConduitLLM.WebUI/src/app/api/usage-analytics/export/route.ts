import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';

interface RequestLog {
  timestamp?: string;
  virtualKeyName?: string;
  provider?: string;
  model?: string;
  status?: string;
  duration?: number;
  inputTokens?: number;
  outputTokens?: number;
  cost?: number;
  currency?: string;
  ipAddress?: string;
  errorMessage?: string;
}

interface RequestLogsResponse {
  items: RequestLog[];
}

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const timeRange = searchParams.get('range') ?? '7d';
    
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
    }) as RequestLogsResponse;

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
    
    requestLogs.items.forEach((log: RequestLog) => {
      const row = [
        log.timestamp ?? '',
        log.virtualKeyName ?? '',
        log.provider ?? '',
        log.model ?? '',
        log.status ?? '',
        log.duration ?? '',
        log.inputTokens ?? 0,
        log.outputTokens ?? 0,
        (log.inputTokens ?? 0) + (log.outputTokens ?? 0),
        log.cost ?? 0,
        log.currency ?? 'USD',
        log.ipAddress ?? '',
        log.errorMessage ? `"${log.errorMessage.replace(/"/g, '""')}"` : '' // Escape quotes in error messages
      ];
      csvRows.push(row.join(','));
    });

    const csv = csvRows.join('\n');
    
    // Generate filename with timestamp
    const timestamp = new Date().toISOString().split('T')[0]; // YYYY-MM-DD format
    const filename = `usage-analytics-${timeRange}-${timestamp}.csv`;

    const responseHeaders: Record<string, string> = {};
    responseHeaders['Content-Type'] = 'text/csv; charset=utf-8';
    responseHeaders['Content-Disposition'] = `attachment; filename="${filename}"`;
    responseHeaders['Cache-Control'] = 'no-cache, no-store, must-revalidate';
    responseHeaders['Pragma'] = 'no-cache';
    responseHeaders['Expires'] = '0';
    
    return new NextResponse(csv, { headers: responseHeaders });

  } catch (error) {
    // Enhanced error handling for export functionality
    console.error('Export failed:', error);
    
    // Get timeRange from URL params in case of error
    const { searchParams } = new URL(req.url);
    const errorTimeRange = searchParams.get('range') ?? '7d';
    
    // Try to provide a fallback export with error information
    const errorCsv = `Error,Message,Timestamp
Export Failed,"${error instanceof Error ? error.message.replace(/"/g, '""') : 'Unknown error occurred'}","${new Date().toISOString()}"
Note,"Please try again or contact support if the problem persists",""
Range,"${errorTimeRange}",""`;

    // Return error CSV instead of JSON error for download consistency
    const errorHeaders: Record<string, string> = {};
    errorHeaders['Content-Type'] = 'text/csv; charset=utf-8';
    errorHeaders['Content-Disposition'] = `attachment; filename="usage-analytics-error-${errorTimeRange}-${new Date().toISOString().split('T')[0]}.csv"`;
    errorHeaders['X-Export-Error'] = 'true';
    errorHeaders['X-Error-Message'] = error instanceof Error ? error.message : 'Export failed';
    
    return new NextResponse(errorCsv, {
      headers: errorHeaders,
      status: 200 // Use 200 so the download still works
    });
  }
}