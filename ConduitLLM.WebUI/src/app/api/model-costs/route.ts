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

    // The Admin API returns an array directly, but frontend expects a paginated response
    // If response is an array, wrap it in the expected structure
    if (Array.isArray(response)) {
      const paginatedResponse = {
        items: response,
        totalCount: response.length,
        page: page,
        pageSize: pageSize,
        totalPages: Math.ceil(response.length / pageSize)
      };
      return NextResponse.json(paginatedResponse);
    }

    return NextResponse.json(response);
  } catch (error) {
    console.error('[ModelCosts] GET error:', error);
    return handleSDKError(error);
  }
}


export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as CreateModelCostDto;

    // Type guard to ensure body is a valid object
    if (!body || typeof body !== 'object') {
      return NextResponse.json(
        { error: 'Invalid request body' },
        { status: 400 }
      );
    }

    const adminClient = getServerAdminClient();

    const result = await adminClient.modelCosts.create(body);
    
    return NextResponse.json(result, { status: 201 });
  } catch (error) {
    console.error('[ModelCosts] POST error:', error);
    return handleSDKError(error);
  }
}