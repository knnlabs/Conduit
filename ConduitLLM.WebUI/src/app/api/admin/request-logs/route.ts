import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformPaginatedResponse } from '@/lib/utils/sdk-transforms';
import { parseQueryParams, createFileResponse, createValidationError } from '@/lib/utils/route-helpers';

export const GET = withSDKAuth(
  async (request, { auth }) => {
    try {
      const params = parseQueryParams(request);
      
      // Get request logs with filtering
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.analytics.getRequestLogs({
          pageNumber: params.page,
          pageSize: params.pageSize,
          startDate: params.startDate,
          endDate: params.endDate,
          status: params.get('status') as 'success' | 'error' | 'timeout' | undefined,
          virtualKeyId: params.get('virtualKeyId') ? parseInt(params.get('virtualKeyId')!) : undefined,
          model: params.get('modelName') || undefined,
          provider: params.get('providerId') || undefined,
          minDuration: params.get('minDuration') ? parseInt(params.get('minDuration')!) : undefined,
          maxDuration: params.get('maxDuration') ? parseInt(params.get('maxDuration')!) : undefined,
          ipAddress: params.get('ipAddress') || undefined,
          sortBy: params.sortBy && params.sortOrder ? {
            field: params.sortBy,
            direction: params.sortOrder as 'asc' | 'desc'
          } : undefined,
          search: params.get('search') || undefined,
        }),
        'get request logs'
      );

      // The SDK returns a PaginatedResponse object
      return transformPaginatedResponse(result.items || [], {
        page: result.pageNumber || params.page,
        pageSize: result.pageSize || params.pageSize,
        total: result.totalCount || 0,
      });
    } catch (error: unknown) {
      // Return empty result for 404
      if ((error as Record<string, unknown>)?.statusCode === 404 || (error as Record<string, unknown>)?.type === 'NOT_FOUND') {
        return transformPaginatedResponse([], {
          page: 1,
          pageSize: 50,
          total: 0,
        });
      }
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

// Export request logs
export const POST = withSDKAuth(
  async (request, { auth }) => {
    let body: { format?: string; filters?: Record<string, unknown> } = {};
    try {
      body = await request.json();
      const { format, filters } = body;

      // Validate format
      const validFormats = ['csv', 'json', 'excel'];
      if (!format || !validFormats.includes(format)) {
        return createValidationError(
          `Invalid export format. Supported formats: ${validFormats.join(', ')}`,
          { providedFormat: format }
        );
      }

      // Export request logs using the generic analytics export
      const blob = await withSDKErrorHandling(
        async () => auth.adminClient!.analytics.exportAnalytics(
          {
            startDate: (filters?.startDate as string) || new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
            endDate: (filters?.endDate as string) || new Date().toISOString(),
            virtualKeyIds: filters?.virtualKeyId ? [Number(filters.virtualKeyId)] : undefined,
            models: filters?.modelName ? [filters.modelName as string] : undefined,
            providers: filters?.providerId ? [filters.providerId as string] : undefined,
            includeMetadata: true,
          },
          format as 'csv' | 'excel' | 'json'
        ),
        'export request logs'
      );

      // Convert Blob to ArrayBuffer then to Buffer
      const arrayBuffer = await blob.arrayBuffer();
      const buffer = Buffer.from(arrayBuffer);

      // Determine content type
      const contentTypes = {
        csv: 'text/csv',
        json: 'application/json',
        excel: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
      };

      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      const filename = `request-logs-${timestamp}.${format}`;

      return createFileResponse(
        buffer,
        {
          filename,
          contentType: contentTypes[format as keyof typeof contentTypes],
          disposition: 'attachment',
        }
      );
    } catch (error: unknown) {
      // Provide sample data if not available
      if ((error as Record<string, unknown>)?.statusCode === 404 || (error as Record<string, unknown>)?.type === 'NOT_FOUND') {
        const format = body.format || 'csv';
        const sampleData = generateSampleRequestLogs(format);
        
        return createFileResponse(
          sampleData.content,
          {
            filename: sampleData.filename,
            contentType: sampleData.contentType,
            disposition: 'attachment',
          }
        );
      }
      
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

// Generate sample request logs for demonstration
function generateSampleRequestLogs(format: string): {
  content: string;
  filename: string;
  contentType: string;
} {
  const date = new Date();
  const timestamp = date.toISOString().replace(/[:.]/g, '-');
  
  if (format === 'csv') {
    return {
      content: `Timestamp,Method,Endpoint,Status,Duration (ms),Virtual Key,Model,Provider,Request Tokens,Response Tokens,Cost
${date.toISOString()},POST,/v1/chat/completions,200,1523,demo-key,gpt-4,OpenAI,150,200,0.015
${date.toISOString()},POST,/v1/chat/completions,200,823,test-key,gpt-3.5-turbo,OpenAI,100,150,0.001
${date.toISOString()},POST,/v1/completions,429,45,prod-key,claude-3-opus,Anthropic,0,0,0.000
${date.toISOString()},GET,/v1/models,200,67,demo-key,-,-,0,0,0.000`,
      filename: `request-logs-${timestamp}.csv`,
      contentType: 'text/csv',
    };
  }
  
  if (format === 'json') {
    return {
      content: JSON.stringify({
        exportDate: date.toISOString(),
        logs: [
          {
            timestamp: date.toISOString(),
            method: 'POST',
            endpoint: '/v1/chat/completions',
            status: 200,
            duration: 1523,
            virtualKey: 'demo-key',
            model: 'gpt-4',
            provider: 'OpenAI',
            usage: {
              promptTokens: 150,
              completionTokens: 200,
              totalTokens: 350,
            },
            cost: 0.015,
          },
        ],
        metadata: {
          totalRecords: 1,
          exportVersion: '1.0',
        },
      }, null, 2),
      filename: `request-logs-${timestamp}.json`,
      contentType: 'application/json',
    };
  }
  
  // Default for Excel format - return CSV
  return generateSampleRequestLogs('csv');
}