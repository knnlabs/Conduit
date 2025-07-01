import { NextRequest } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { createFileResponse, createValidationError } from '@/lib/utils/route-helpers';

const VALID_EXPORT_TYPES = ['usage', 'cost', 'virtual-keys', 'request-logs', 'providers'];
const VALID_FORMATS = ['csv', 'json', 'excel'];

export const POST = withSDKAuth(
  async (request, { auth }) => {
    let body: any;
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
            startDate: filters?.startDate || new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
            endDate: filters?.endDate || new Date().toISOString(),
            virtualKeyIds: filters?.virtualKeyId ? [filters?.virtualKeyId] : undefined,
            models: filters?.modelName ? [filters?.modelName] : undefined,
            providers: filters?.providerId ? [filters?.providerId] : undefined,
            groupBy: filters?.groupBy,
            includeMetadata: filters?.includeMetadata ?? true,
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
    } catch (error: any) {
      // Provide sample data if feature not available
      if (error.statusCode === 404 || error.type === 'NOT_FOUND') {
        const sampleData = generateSampleExport(
          body.type || 'usage',
          body.format || 'csv'
        );
        
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

// Generate sample export data for demonstration
function generateSampleExport(type: string, format: string): {
  content: string;
  filename: string;
  contentType: string;
} {
  const date = new Date();
  const timestamp = date.toISOString().replace(/[:.]/g, '-');
  
  const contentTypes = {
    csv: 'text/csv',
    json: 'application/json',
    excel: 'text/csv', // Return CSV for Excel format as fallback
  };

  if (type === 'usage' && format === 'csv') {
    return {
      content: `Date,Virtual Key,Model,Requests,Input Tokens,Output Tokens,Total Cost
${date.toISOString().split('T')[0]},demo-key,gpt-4,100,25000,25000,5.00
${date.toISOString().split('T')[0]},test-key,gpt-3.5-turbo,500,50000,50000,2.00
${date.toISOString().split('T')[0]},prod-key,claude-3-opus,200,37500,37500,7.50`,
      filename: `usage-export-${timestamp}.csv`,
      contentType: contentTypes.csv,
    };
  }
  
  if (type === 'cost' && format === 'csv') {
    return {
      content: `Date,Provider,Model,Total Cost,Request Count,Average Cost
${date.toISOString().split('T')[0]},OpenAI,gpt-4,10.00,200,0.05
${date.toISOString().split('T')[0]},OpenAI,gpt-3.5-turbo,2.00,500,0.004
${date.toISOString().split('T')[0]},Anthropic,claude-3-opus,7.50,200,0.0375`,
      filename: `cost-export-${timestamp}.csv`,
      contentType: contentTypes.csv,
    };
  }
  
  if (type === 'virtual-keys' && format === 'csv') {
    return {
      content: `Key Name,Key ID,Total Spend,Request Count,Created At,Last Used,Status,Remaining Budget
demo-key,vk_demo_123,15.00,300,${date.toISOString()},${date.toISOString()},active,85.00
test-key,vk_test_456,5.00,100,${date.toISOString()},${date.toISOString()},active,95.00
prod-key,vk_prod_789,50.00,1000,${date.toISOString()},${date.toISOString()},active,450.00`,
      filename: `virtual-keys-export-${timestamp}.csv`,
      contentType: contentTypes.csv,
    };
  }
  
  if (format === 'json') {
    const jsonData = {
      type,
      exportDate: date.toISOString(),
      filters: {},
      data: [
        {
          date: date.toISOString().split('T')[0],
          metrics: {
            requests: 100,
            cost: 5.00,
            tokens: 50000,
          },
        },
      ],
      metadata: {
        totalRecords: 1,
        exportVersion: '1.0',
      },
    };
    
    return {
      content: JSON.stringify(jsonData, null, 2),
      filename: `${type}-export-${timestamp}.json`,
      contentType: contentTypes.json,
    };
  }
  
  // Default: return empty CSV
  return {
    content: 'No data available for export',
    filename: `${type}-export-${timestamp}.${format}`,
    contentType: 'text/plain',
  };
}