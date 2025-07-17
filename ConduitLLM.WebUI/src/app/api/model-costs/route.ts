import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url);
    const page = parseInt(searchParams.get('page') || '1', 10);
    const pageSize = parseInt(searchParams.get('pageSize') || '50', 10);
    const provider = searchParams.get('provider') || undefined;
    const isActive = searchParams.get('isActive') 
      ? searchParams.get('isActive') === 'true'
      : undefined;

    console.log('[ModelCosts] GET request:', { page, pageSize, provider, isActive });

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

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    console.log('[ModelCosts] POST request body:', body);

    const adminClient = getServerAdminClient();
    
    // Handle both new format (modelIdPattern) and legacy format (modelId)
    const createData = body.modelIdPattern 
      ? body  // New format
      : {     // Transform legacy format
          modelIdPattern: body.modelId,
          inputTokenCost: body.inputTokenCost,
          outputTokenCost: body.outputTokenCost,
          embeddingTokenCost: body.embeddingTokenCost,
          imageCostPerImage: body.imageCostPerImage,
          audioCostPerMinute: body.audioCostPerMinute,
          audioCostPerKCharacters: body.audioCostPerKCharacters,
          audioInputCostPerMinute: body.audioInputCostPerMinute,
          audioOutputCostPerMinute: body.audioOutputCostPerMinute,
          videoCostPerSecond: body.videoCostPerSecond,
          videoResolutionMultipliers: body.videoResolutionMultipliers,
          description: body.description,
          priority: body.priority || 0,
        };

    const result = await adminClient.modelCosts.create(createData);
    
    console.log('[ModelCosts] POST success:', result.id);
    return NextResponse.json(result, { status: 201 });
  } catch (error) {
    console.error('[ModelCosts] POST error:', error);
    return handleSDKError(error);
  }
}