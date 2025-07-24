import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// Type definitions for SDK responses
interface RequestLogDto {
  id: string;
  timestamp: string;
  status: string;
  duration: number;
  virtualKeyId: string;
  virtualKeyName?: string;
  provider: string;
  model: string;
  inputTokens: number;
  outputTokens: number;
  cost?: number;
  errorMessage?: string;
  userAgent?: string;
  ipAddress?: string;
}

interface RequestLogResponse {
  items: RequestLogDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}


interface WebUIRequestLog {
  id: string;
  timestamp: string;
  method: string;
  path: string;
  statusCode: number;
  duration: number;
  virtualKeyId: string;
  virtualKeyName: string;
  provider: string;
  model: string;
  tokenUsage: {
    prompt: number;
    completion: number;
    total: number;
  };
  cost?: number;
  error?: string;
  userAgent?: string;
  ipAddress?: string;
  requestBody?: unknown;
  responseBody?: unknown;
}

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
    
    try {
      // Parse status code parameter properly
      let statusCodeParam: number | undefined;
      const statusCodeStr = searchParams.get('statusCode');
      if (statusCodeStr && !statusCodeStr.endsWith('xx')) {
        statusCodeParam = parseInt(statusCodeStr, 10);
      }
      
      // Use the Admin SDK to fetch real request logs
      const logsResponse = await adminClient.analytics.getRequestLogs({
        page: parseInt(searchParams.get('page') ?? '1', 10),
        pageSize: parseInt(searchParams.get('pageSize') ?? '20', 10),
        startDate: searchParams.get('dateFrom') ?? undefined,
        endDate: searchParams.get('dateTo') ?? undefined,
        virtualKeyId: searchParams.get('virtualKeyId') ?? undefined,
        provider: searchParams.get('provider') ?? undefined,
        model: searchParams.get('model') ?? undefined,
        statusCode: statusCodeParam,
        // TODO: SDK should support search parameter for filtering logs
        // search: searchParams.get('search') || undefined,
      }) as unknown as RequestLogResponse;
      
      // Transform the response to match the expected format
      const logs: WebUIRequestLog[] = logsResponse.items.map((log: RequestLogDto): WebUIRequestLog => ({
        id: log.id,
        timestamp: log.timestamp,
        method: 'POST', // Most API calls are POST
        path: '/v1/chat/completions', // Most common endpoint
        statusCode: getStatusCode(log.status),
        duration: log.duration,
        virtualKeyId: log.virtualKeyId,
        virtualKeyName: log.virtualKeyName ?? 'Unknown Key',
        provider: log.provider,
        model: log.model,
        tokenUsage: {
          prompt: log.inputTokens,
          completion: log.outputTokens,
          total: log.inputTokens + log.outputTokens,
        },
        cost: log.cost,
        error: log.errorMessage,
        userAgent: log.userAgent,
        ipAddress: log.ipAddress,
        requestBody: undefined,
        responseBody: undefined,
      }));
      
      // Apply client-side filtering for search and status groups
      let filteredLogs: WebUIRequestLog[] = logs;
      
      // Search filtering
      const search = searchParams.get('search');
      if (search) {
        const searchLower = search.toLowerCase();
        filteredLogs = filteredLogs.filter((log: WebUIRequestLog) => 
          log.id.toLowerCase().includes(searchLower) ||
          log.model.toLowerCase().includes(searchLower) ||
          log.provider.toLowerCase().includes(searchLower) ||
          log.virtualKeyName.toLowerCase().includes(searchLower) ||
          (log.error && log.error.toLowerCase().includes(searchLower))
        );
      }
      
      // Status code group filtering (2xx, 4xx, 5xx)
      if (statusCodeStr?.endsWith('xx')) {
        const statusGroup = parseInt(statusCodeStr[0], 10);
        filteredLogs = filteredLogs.filter((log: WebUIRequestLog) => 
          Math.floor(log.statusCode / 100) === statusGroup
        );
      }
      
      return NextResponse.json({ 
        logs: filteredLogs,
        totalCount: filteredLogs.length,
        page: logsResponse.page,
        pageSize: logsResponse.pageSize,
        totalPages: Math.ceil(filteredLogs.length / logsResponse.pageSize),
      });
    } catch (sdkError) {
      // If SDK call fails, return the error
      console.error('Failed to fetch request logs from SDK:', sdkError);
      return handleSDKError(sdkError);
    }
  } catch (error) {
    return handleSDKError(error);
  }
}
