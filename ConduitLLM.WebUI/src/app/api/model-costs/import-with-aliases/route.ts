import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { CreateModelCostDto } from '@/app/model-costs/types/modelCost';
import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

interface ImportWithAliasesRequest {
  costName: string;
  modelAliases: string[];
  modelType: string;
  inputTokenCost: number;
  outputTokenCost: number;
  // ... other cost fields
  [key: string]: unknown;
}

// Extended type to include additional fields from API response
interface ExtendedModelProviderMappingDto extends ModelProviderMappingDto {
  modelAlias?: string;
}

export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as ImportWithAliasesRequest[];
    const adminClient = getServerAdminClient();
    
    // First, fetch all model mappings to resolve aliases
    const allMappings = await adminClient.modelMappings.list() as ExtendedModelProviderMappingDto[];
    
    // Create a map of alias to mapping ID
    const aliasToMappingId = new Map<string, number>();
    for (const mapping of allMappings) {
      if (mapping?.modelAlias && mapping?.id) {
        aliasToMappingId.set(mapping.modelAlias, mapping.id);
      }
    }
    
    // Process each import item
    const modelCosts: CreateModelCostDto[] = [];
    const errors: Array<{ costName: string; error: string }> = [];
    
    for (const item of body) {
      const mappingIds: number[] = [];
      const missingAliases: string[] = [];
      
      // Resolve aliases to mapping IDs
      for (const alias of item.modelAliases) {
        const mappingId = aliasToMappingId.get(alias);
        if (mappingId) {
          mappingIds.push(mappingId);
        } else {
          missingAliases.push(alias);
        }
      }
      
      if (missingAliases.length > 0) {
        errors.push({
          costName: item.costName,
          error: `Model aliases not found: ${missingAliases.join(', ')}`
        });
        continue;
      }
      
      if (mappingIds.length === 0) {
        errors.push({
          costName: item.costName,
          error: 'No valid model mappings found'
        });
        continue;
      }
      
      // Create the model cost DTO
      const costDto: CreateModelCostDto = {
        costName: item.costName,
        modelProviderMappingIds: mappingIds,
        modelType: item.modelType as 'chat' | 'embedding' | 'image' | 'audio' | 'video',
        inputTokenCost: item.inputTokenCost,
        outputTokenCost: item.outputTokenCost,
        cachedInputTokenCost: item.cachedInputTokenCost as number | undefined,
        cachedInputWriteCost: item.cachedInputWriteCost as number | undefined,
        embeddingTokenCost: item.embeddingTokenCost as number | undefined,
        imageCostPerImage: item.imageCostPerImage as number | undefined,
        audioCostPerMinute: item.audioCostPerMinute as number | undefined,
        audioCostPerKCharacters: item.audioCostPerKCharacters as number | undefined,
        audioInputCostPerMinute: item.audioInputCostPerMinute as number | undefined,
        audioOutputCostPerMinute: item.audioOutputCostPerMinute as number | undefined,
        videoCostPerSecond: item.videoCostPerSecond as number | undefined,
        videoResolutionMultipliers: item.videoResolutionMultipliers as string | undefined,
        supportsBatchProcessing: item.supportsBatchProcessing as boolean | undefined,
        batchProcessingMultiplier: item.batchProcessingMultiplier as number | undefined,
        imageQualityMultipliers: item.imageQualityMultipliers as string | undefined,
        costPerSearchUnit: item.costPerSearchUnit as number | undefined,
        costPerInferenceStep: item.costPerInferenceStep as number | undefined,
        defaultInferenceSteps: item.defaultInferenceSteps as number | undefined,
        priority: item.priority as number | undefined,
        description: item.description as string | undefined,
      };
      
      modelCosts.push(costDto);
    }
    
    // Import the resolved costs
    let importResult = null;
    if (modelCosts.length > 0) {
      importResult = await adminClient.modelCosts.import(modelCosts);
    }
    
    return NextResponse.json({
      success: modelCosts.length,
      failed: errors.length,
      errors,
      importResult,
    });
  } catch (error) {
    console.error('[ModelCosts] Import with aliases error:', error);
    return handleSDKError(error);
  }
}