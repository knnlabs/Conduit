import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

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

    console.log('[ModelCosts] GET by ID:', id);

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

    const body = await req.json();
    console.log('[ModelCosts] PUT request:', { id, body });

    const adminClient = getServerAdminClient();
    const result = await adminClient.modelCosts.update(id, body);

    console.log('[ModelCosts] PUT success:', id);
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

    console.log('[ModelCosts] DELETE request:', id);

    const adminClient = getServerAdminClient();
    await adminClient.modelCosts.deleteById(id);

    console.log('[ModelCosts] DELETE success:', id);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    console.error('[ModelCosts] DELETE error:', error);
    return handleSDKError(error);
  }
}