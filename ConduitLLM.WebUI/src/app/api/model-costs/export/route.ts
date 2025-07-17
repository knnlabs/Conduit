import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

function convertToCSV(modelCosts: any[]): string {
  if (!modelCosts || modelCosts.length === 0) {
    return '';
  }

  // Define CSV headers matching the model cost structure
  const headers = [
    'Model Pattern',
    'Provider',
    'Model Type',
    'Input Cost (per 1K tokens)',
    'Output Cost (per 1K tokens)',
    'Embedding Cost (per 1K tokens)',
    'Image Cost (per image)',
    'Audio Cost (per minute)',
    'Audio Cost (per 1K chars)',
    'Audio Input Cost (per minute)',
    'Audio Output Cost (per minute)',
    'Video Cost (per second)',
    'Video Resolution Multipliers',
    'Priority',
    'Active',
    'Description',
    'Created At',
    'Updated At'
  ];

  // Helper function to escape CSV values
  const escapeCSV = (value: any): string => {
    if (value === null || value === undefined) return '';
    const str = String(value);
    if (str.includes(',') || str.includes('"') || str.includes('\n')) {
      return `"${str.replace(/"/g, '""')}"`;
    }
    return str;
  };

  // Build CSV rows
  const rows = modelCosts.map(cost => {
    // Convert costs from per million to per thousand tokens for user-friendly display
    const inputCostPer1K = cost.inputCostPerMillionTokens 
      ? (cost.inputCostPerMillionTokens / 1000).toFixed(4) 
      : '';
    const outputCostPer1K = cost.outputCostPerMillionTokens 
      ? (cost.outputCostPerMillionTokens / 1000).toFixed(4) 
      : '';
    const embeddingCostPer1K = cost.embeddingTokenCost 
      ? cost.embeddingTokenCost.toFixed(4) 
      : '';

    return [
      escapeCSV(cost.modelIdPattern),
      escapeCSV(cost.providerName),
      escapeCSV(cost.modelType),
      escapeCSV(inputCostPer1K),
      escapeCSV(outputCostPer1K),
      escapeCSV(embeddingCostPer1K),
      escapeCSV(cost.imageCostPerImage),
      escapeCSV(cost.audioCostPerMinute),
      escapeCSV(cost.audioCostPerKCharacters),
      escapeCSV(cost.audioInputCostPerMinute),
      escapeCSV(cost.audioOutputCostPerMinute),
      escapeCSV(cost.videoCostPerSecond),
      escapeCSV(cost.videoResolutionMultipliers),
      escapeCSV(cost.priority),
      escapeCSV(cost.isActive ? 'Yes' : 'No'),
      escapeCSV(cost.description),
      escapeCSV(cost.createdAt),
      escapeCSV(cost.updatedAt)
    ].join(',');
  });

  return [headers.join(','), ...rows].join('\n');
}

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const format = searchParams.get('format') || 'csv';
    const provider = searchParams.get('provider') || undefined;
    const isActive = searchParams.get('isActive') 
      ? searchParams.get('isActive') === 'true'
      : undefined;

    console.log('[ModelCosts] Export request:', { format, provider, isActive });

    const adminClient = getServerAdminClient();
    
    // Fetch all model costs with large page size
    const response = await adminClient.modelCosts.list({
      page: 1,
      pageSize: 1000,
      provider,
      isActive,
    });

    if (format === 'json') {
      // Return JSON format
      return NextResponse.json(response.items || []);
    } else {
      // Convert to CSV
      const csv = convertToCSV(response.items || []);
      const filename = `model-costs-${new Date().toISOString().split('T')[0]}.csv`;

      return new NextResponse(csv, {
        headers: {
          'Content-Type': 'text/csv',
          'Content-Disposition': `attachment; filename="${filename}"`,
        },
      });
    }
  } catch (error) {
    console.error('[ModelCosts] Export error:', error);
    return handleSDKError(error);
  }
}