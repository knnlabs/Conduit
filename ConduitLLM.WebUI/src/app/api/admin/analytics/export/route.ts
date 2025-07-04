import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { createFileResponse, createValidationError } from '@/lib/utils/route-helpers';

const VALID_EXPORT_TYPES = ['usage', 'cost', 'virtual-keys', 'request-logs', 'providers'];
const VALID_FORMATS = ['csv', 'json', 'excel'];

export const POST = withSDKAuth(
  async (request, { auth }) => {
    let body: { type?: string; format?: string; filters?: Record<string, unknown> };
    try {
      body = await request.json();
      const { type, format, filters } = body;

      // Validate export type
      if (!type || !VALID_EXPORT_TYPES.includes(type)) {
        return createValidationError(
          `Invalid export type. Supported types: ${VALID_EXPORT_TYPES.join(', ')}`,
          { providedType: type }
        );
      }

      // Validate format
      if (!format || !VALID_FORMATS.includes(format)) {
        return createValidationError(
          `Invalid export format. Supported formats: ${VALID_FORMATS.join(', ')}`,
          { providedFormat: format }
        );
      }

      // Export analytics data using SDK
      const blob = await withSDKErrorHandling(
        async () => auth.adminClient!.analytics.exportAnalytics(
          {
            startDate: (filters?.startDate as string) || new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
            endDate: (filters?.endDate as string) || new Date().toISOString(),
            virtualKeyIds: filters?.virtualKeyId ? [Number(filters?.virtualKeyId)] : undefined,
            models: filters?.modelName ? [filters?.modelName as string] : undefined,
            providers: filters?.providerId ? [filters?.providerId as string] : undefined,
            groupBy: filters?.groupBy as "hour" | "day" | "week" | "month" | undefined,
            includeMetadata: (filters?.includeMetadata as boolean) ?? true,
          },
          format as 'csv' | 'excel' | 'json'
        ),
        `export ${type} analytics`
      );

      // Convert Blob to ArrayBuffer then to Buffer
      const arrayBuffer = await blob.arrayBuffer();
      const buffer = Buffer.from(arrayBuffer);

      // Determine content type and filename
      const contentTypes = {
        csv: 'text/csv',
        json: 'application/json',
        excel: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
      };

      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      const filename = `${type}-export-${timestamp}.${format}`;

      // Return file response
      return createFileResponse(
        buffer,
        {
          filename,
          contentType: contentTypes[format as keyof typeof contentTypes],
          disposition: 'attachment',
        }
      );
    } catch (error: unknown) {
      // Return error if export not available
      if ((error as Record<string, unknown>)?.statusCode === 404 || (error as Record<string, unknown>)?.type === 'NOT_FOUND') {
        return createValidationError(
          'Analytics export feature is not yet available',
          { feature: 'analytics-export', available: false }
        );
      }
      
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

