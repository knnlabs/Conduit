import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

function getStatusCode(status: string): number {
  if (status === 'success') {
    return 200;
  }
  if (status === 'timeout') {
    return 408;
  }
  return 500;
}

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const adminClient = getServerAdminClient();
    
    // Fetch all logs for export (up to 1000)
    const logsResponse = await adminClient.analytics.getRequestLogs({
      page: 1,
      pageSize: 1000,
      startDate: searchParams.get('dateFrom') ?? undefined,
      endDate: searchParams.get('dateTo') ?? undefined,
      virtualKeyId: searchParams.get('virtualKeyId') ?? undefined,
      provider: searchParams.get('provider') ?? undefined,
      model: searchParams.get('model') ?? undefined,
    });
    
    // Create CSV header
    const csvRows = [
      'Timestamp,Status Code,Duration (ms),Virtual Key,Provider,Model,Input Tokens,Output Tokens,Total Tokens,Cost,Error,IP Address,User Agent'
    ];
    
    // Add data rows
    logsResponse.items.forEach((log) => {
      const totalTokens = log.inputTokens + log.outputTokens;
      const row = [
        log.timestamp,
        getStatusCode(log.status),
        log.duration,
        log.virtualKeyName ?? log.virtualKeyId,
        log.provider,
        log.model,
        log.inputTokens,
        log.outputTokens,
        totalTokens,
        log.cost ?? '',
        log.errorMessage ? `"${log.errorMessage.replace(/"/g, '""')}"` : '',
        log.ipAddress ?? '',
        log.userAgent ? `"${log.userAgent.replace(/"/g, '""')}"` : ''
      ];
      csvRows.push(row.join(','));
    });
    
    const csv = csvRows.join('\n');
    const responseHeaders: Record<string, string> = {};
    responseHeaders['Content-Type'] = 'text/csv';
    responseHeaders['Content-Disposition'] = `attachment; filename="request-logs-${new Date().toISOString().split('T')[0]}.csv"`;
    
    return new NextResponse(csv, { headers: responseHeaders });
  } catch (error) {
    return handleSDKError(error);
  }
}
