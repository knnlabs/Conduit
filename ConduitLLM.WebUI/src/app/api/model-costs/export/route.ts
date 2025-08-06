import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient, type ConduitAdminClient } from '@/lib/server/adminClient';
import type { ModelCost } from '@/app/model-costs/types/modelCost';
import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

interface ExtendedModelProviderMappingDto extends ModelProviderMappingDto {
  modelAlias?: string;
  providerName?: string;
  providerTypeName?: string;
}

interface EnrichedModelCost extends ModelCost {
  providers: Array<{
    providerId: number;
    providerName: string;
    providerType: string;
  }>;
}

async function enrichModelCostsWithProviders(
  modelCosts: ModelCost[],
  adminClient: ConduitAdminClient
): Promise<EnrichedModelCost[]> {
  try {
    // Fetch all model mappings (returns array directly, not paginated)
    const mappings = await adminClient.modelMappings.list() as ExtendedModelProviderMappingDto[];
    
    return modelCosts.map(cost => {
      // Find all unique providers for this cost's model aliases
      const providersMap = new Map<number, { providerId: number; providerName: string; providerType: string }>();
      
      cost.associatedModelAliases.forEach(alias => {
        const mapping = mappings.find(m => 
          m.modelAlias === alias || m.providerModelId === alias
        );
        
        if (mapping) {
          const providerId = mapping.providerId;
          if (!providersMap.has(providerId)) {
            providersMap.set(providerId, {
              providerId,
              providerName: mapping.providerName ?? `Provider ${providerId}`,
              providerType: mapping.providerTypeName ?? 'Unknown'
            });
          }
        }
      });

      return {
        ...cost,
        providers: Array.from(providersMap.values())
      };
    });
  } catch (error) {
    console.warn('[ModelCosts] Failed to enrich with provider data:', error);
    // Return costs without enrichment if fetching mappings fails
    return modelCosts.map(cost => ({
      ...cost,
      providers: []
    }));
  }
}

function convertToCSV(modelCosts: EnrichedModelCost[]): string {
  if (!modelCosts || modelCosts.length === 0) {
    return '';
  }

  // Define CSV headers matching the model cost structure
  const headers = [
    'Cost Name',
    'Associated Model Aliases',
    'Provider Names',
    'Provider Types',
    'Model Type',
    'Input Cost (per 1K tokens)',
    'Output Cost (per 1K tokens)',
    'Cached Input Cost (per 1K tokens)',
    'Cache Write Cost (per 1K tokens)',
    'Embedding Cost (per 1K tokens)',
    'Image Cost (per image)',
    'Audio Cost (per minute)',
    'Audio Cost (per 1K chars)',
    'Audio Input Cost (per minute)',
    'Audio Output Cost (per minute)',
    'Video Cost (per second)',
    'Video Resolution Multipliers',
    'Batch Processing Multiplier',
    'Supports Batch Processing',
    'Image Quality Multipliers',
    'Search Unit Cost (per 1K units)',
    'Priority',
    'Active',
    'Description',
    'Created At',
    'Updated At'
  ];

  // Helper function to escape CSV values
  const escapeCSV = (value: unknown): string => {
    if (value === null || value === undefined) return '';
    const str = typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean' 
      ? String(value) 
      : JSON.stringify(value);
    if (str.includes(',') || str.includes('"') || str.includes('\n')) {
      return `"${str.replace(/"/g, '""')}"`;
    }
    return str;
  };

  // Build CSV rows
  const rows = modelCosts.map(cost => {
    // Note: SDK costs are now per million tokens, convert to per 1K for export
    const inputCostPer1K = cost.inputCostPerMillionTokens 
      ? (cost.inputCostPerMillionTokens / 1000).toFixed(4) 
      : '';
    const outputCostPer1K = cost.outputCostPerMillionTokens 
      ? (cost.outputCostPerMillionTokens / 1000).toFixed(4) 
      : '';
    const cachedInputCostPer1K = cost.cachedInputCostPerMillionTokens 
      ? (cost.cachedInputCostPerMillionTokens / 1000).toFixed(4) 
      : '';
    const cachedInputWriteCostPer1K = cost.cachedInputWriteCostPerMillionTokens 
      ? (cost.cachedInputWriteCostPerMillionTokens / 1000).toFixed(4) 
      : '';
    const embeddingCostPer1K = cost.embeddingCostPerMillionTokens 
      ? (cost.embeddingCostPerMillionTokens / 1000).toFixed(4) 
      : '';
    const searchUnitCostPer1K = cost.costPerSearchUnit 
      ? cost.costPerSearchUnit.toFixed(4) 
      : '';

    // Extract provider information
    const providerNames = cost.providers.map(p => p.providerName).join('; ');
    const providerTypes = cost.providers.map(p => p.providerType).join('; ');

    return [
      escapeCSV(cost.costName),
      escapeCSV(cost.associatedModelAliases?.join(', ') || ''),
      escapeCSV(providerNames),
      escapeCSV(providerTypes),
      escapeCSV(cost.modelType),
      escapeCSV(inputCostPer1K),
      escapeCSV(outputCostPer1K),
      escapeCSV(cachedInputCostPer1K),
      escapeCSV(cachedInputWriteCostPer1K),
      escapeCSV(embeddingCostPer1K),
      escapeCSV(cost.imageCostPerImage),
      escapeCSV(cost.audioCostPerMinute),
      escapeCSV(cost.audioCostPerKCharacters),
      escapeCSV(cost.audioInputCostPerMinute),
      escapeCSV(cost.audioOutputCostPerMinute),
      escapeCSV(cost.videoCostPerSecond),
      escapeCSV(cost.videoResolutionMultipliers),
      escapeCSV(cost.batchProcessingMultiplier),
      escapeCSV(cost.supportsBatchProcessing ? 'Yes' : 'No'),
      escapeCSV(cost.imageQualityMultipliers),
      escapeCSV(searchUnitCostPer1K),
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
    const format = searchParams.get('format') ?? 'csv';
    const provider = searchParams.get('provider') ?? undefined;
    const isActive = searchParams.get('isActive')
      ? searchParams.get('isActive') === 'true'
      : undefined;


    const adminClient = getServerAdminClient();
    
    // Fetch all model costs with large page size
    const response = await adminClient.modelCosts.list({
      page: 1,
      pageSize: 1000,
      provider,
      isActive,
    });

    // Enrich model costs with provider information
    const enrichedCosts = await enrichModelCostsWithProviders(
      response.items ?? [],
      adminClient
    );

    if (format === 'json') {
      // Return JSON format with enriched data
      return NextResponse.json(enrichedCosts);
    } else {
      // Convert to CSV with enriched data
      const csv = convertToCSV(enrichedCosts);
      const filename = `model-costs-${new Date().toISOString().split('T')[0]}.csv`;

      const headers = new Headers();
      headers.set('Content-Type', 'text/csv');
      headers.set('Content-Disposition', `attachment; filename="${filename}"`);
      
      return new NextResponse(csv, { headers });
    }
  } catch (error) {
    console.error('[ModelCosts] Export error:', error);
    return handleSDKError(error);
  }
}