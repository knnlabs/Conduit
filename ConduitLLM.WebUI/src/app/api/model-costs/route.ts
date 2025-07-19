import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { CreateModelCostDto } from '@/app/model-costs/types/modelCost';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const page = parseInt(searchParams.get('page') ?? '1', 10);
    const pageSize = parseInt(searchParams.get('pageSize') ?? '50', 10);
    const provider = searchParams.get('provider') ?? undefined;
    const isActive = searchParams.get('isActive') 
      ? searchParams.get('isActive') === 'true'
      : undefined;


    const adminClient = getServerAdminClient();
    const response = await adminClient.modelCosts.list({
      page,
      pageSize,
      provider,
      isActive,
    });

    return NextResponse.json(response);
  } catch (error) {
    console.error('[ModelCosts] GET error:', error);
    return handleSDKError(error);
  }
}

interface LegacyCreateModelCostDto {
  modelId: string;
  inputTokenCost?: number;
  outputTokenCost?: number;
  embeddingTokenCost?: number;
  imageCostPerImage?: number;
  audioCostPerMinute?: number;
  audioCostPerKCharacters?: number;
  audioInputCostPerMinute?: number;
  audioOutputCostPerMinute?: number;
  videoCostPerSecond?: number;
  videoResolutionMultipliers?: string;
  description?: string;
  priority?: number;
}

export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as CreateModelCostDto | LegacyCreateModelCostDto;

    // Type guard to ensure body is a valid object
    if (!body || typeof body !== 'object') {
      return NextResponse.json(
        { error: 'Invalid request body' },
        { status: 400 }
      );
    }

    const requestData = body as (CreateModelCostDto | LegacyCreateModelCostDto) & { modelIdPattern?: string; modelId?: string };

    const adminClient = getServerAdminClient();
    
    // Handle both new format (modelIdPattern) and legacy format (modelId)
    const createData: CreateModelCostDto = requestData.modelIdPattern 
      ? requestData as CreateModelCostDto  // New format
      : {     // Transform legacy format
          modelIdPattern: requestData.modelId ?? '',
          inputTokenCost: requestData.inputTokenCost ?? 0,
          outputTokenCost: requestData.outputTokenCost ?? 0,
          embeddingTokenCost: requestData.embeddingTokenCost,
          imageCostPerImage: requestData.imageCostPerImage,
          audioCostPerMinute: requestData.audioCostPerMinute,
          audioCostPerKCharacters: requestData.audioCostPerKCharacters,
          audioInputCostPerMinute: requestData.audioInputCostPerMinute,
          audioOutputCostPerMinute: requestData.audioOutputCostPerMinute,
          videoCostPerSecond: requestData.videoCostPerSecond,
          videoResolutionMultipliers: requestData.videoResolutionMultipliers,
          description: requestData.description,
          priority: requestData.priority ?? 0,
        };

    const result = await adminClient.modelCosts.create(createData);
    
    return NextResponse.json(result, { status: 201 });
  } catch (error) {
    console.error('[ModelCosts] POST error:', error);
    return handleSDKError(error);
  }
}