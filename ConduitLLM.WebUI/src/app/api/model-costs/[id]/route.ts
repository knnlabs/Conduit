import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { UpdateModelCostDto } from '@/app/model-costs/types/modelCost';

interface RouteParams {
  params: Promise<{
    id: string;
  }>;
}

export async function GET(req: NextRequest, { params }: RouteParams) {
  try {
    const { id: idStr } = await params;
    const id = parseInt(idStr, 10);
    
    if (isNaN(id)) {
      return NextResponse.json(
        { error: 'Invalid ID format' },
        { status: 400 }
      );
    }


    const adminClient = getServerAdminClient();
    const result = await adminClient.modelCosts.getById(id);

    return NextResponse.json(result);
  } catch (error) {
    console.error('[ModelCosts] GET by ID error:', error);
    return handleSDKError(error);
  }
}

export async function PUT(req: NextRequest, { params }: RouteParams) {
  try {
    const { id: idStr } = await params;
    const id = parseInt(idStr, 10);
    
    if (isNaN(id)) {
      return NextResponse.json(
        { error: 'Invalid ID format' },
        { status: 400 }
      );
    }

    const body: unknown = await req.json();
    
    // Type guard to ensure body is a valid UpdateModelCostDto
    if (!body || typeof body !== 'object') {
      return NextResponse.json(
        { error: 'Invalid request body' },
        { status: 400 }
      );
    }

    const updateData = body as UpdateModelCostDto;

    const adminClient = getServerAdminClient();
    const result = await adminClient.modelCosts.update(id, updateData);

    return NextResponse.json(result);
  } catch (error) {
    console.error('[ModelCosts] PUT error:', error);
    return handleSDKError(error);
  }
}

export async function DELETE(req: NextRequest, { params }: RouteParams) {
  try {
    const { id: idStr } = await params;
    const id = parseInt(idStr, 10);
    
    if (isNaN(id)) {
      return NextResponse.json(
        { error: 'Invalid ID format' },
        { status: 400 }
      );
    }


    const adminClient = getServerAdminClient();
    await adminClient.modelCosts.deleteById(id);

    return new NextResponse(null, { status: 204 });
  } catch (error) {
    console.error('[ModelCosts] DELETE error:', error);
    return handleSDKError(error);
  }
}