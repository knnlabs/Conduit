import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { CreateModelCostDto } from '@/app/model-costs/types/modelCost';

export async function POST(req: NextRequest) {
  try {
    const body: unknown = await req.json();
    
    // Expecting an array of model costs to import
    if (!Array.isArray(body)) {
      return NextResponse.json(
        { error: 'Expected an array of model costs' },
        { status: 400 }
      );
    }

    // Type guard to ensure all items are valid CreateModelCostDto objects
    const modelCosts: CreateModelCostDto[] = body.filter(
      (item: unknown): item is CreateModelCostDto => 
        item !== null &&
        typeof item === 'object' && 
        'costName' in item &&
        typeof (item as CreateModelCostDto).costName === 'string' &&
        'modelProviderMappingIds' in item &&
        Array.isArray((item as CreateModelCostDto).modelProviderMappingIds)
    );

    if (modelCosts.length !== body.length) {
      return NextResponse.json(
        { error: 'Some items in the array are not valid model cost objects' },
        { status: 400 }
      );
    }

    const adminClient = getServerAdminClient();
    const result = await adminClient.modelCosts.import(modelCosts);
    return NextResponse.json(result);
  } catch (error) {
    console.error('[ModelCosts] Import error:', error);
    return handleSDKError(error);
  }
}