import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/model-mappings/[id] - Get a single model mapping
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const mapping = await adminClient.modelMappings.getById(parseInt(id, 10));
    return NextResponse.json(mapping);
  } catch (error) {
    console.error('Error fetching model mapping:', error);
    return NextResponse.json(
      { error: 'Failed to fetch model mapping' },
      { status: 500 }
    );
  }
}

// PUT /api/model-mappings/[id] - Update a model mapping
export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const body = await req.json();
    const mapping = await adminClient.modelMappings.update(parseInt(id, 10), body);
    return NextResponse.json(mapping);
  } catch (error) {
    console.error('Error updating model mapping:', error);
    return NextResponse.json(
      { error: 'Failed to update model mapping' },
      { status: 500 }
    );
  }
}

// DELETE /api/model-mappings/[id] - Delete a model mapping
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    await adminClient.modelMappings.deleteById(parseInt(id, 10));
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    console.error('Error deleting model mapping:', error);
    return NextResponse.json(
      { error: 'Failed to delete model mapping' },
      { status: 500 }
    );
  }
}