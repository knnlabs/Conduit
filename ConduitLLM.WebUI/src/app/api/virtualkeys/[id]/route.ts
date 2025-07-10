import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/virtualkeys/[id] - Get a single virtual key
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
    const virtualKey = await adminClient.virtualKeys.getById(parseInt(id, 10));
    return NextResponse.json(virtualKey);
  } catch (error) {
    console.error('Error fetching virtual key:', error);
    return NextResponse.json(
      { error: 'Failed to fetch virtual key' },
      { status: 500 }
    );
  }
}

// PUT /api/virtualkeys/[id] - Update a virtual key
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
    const virtualKey = await adminClient.virtualKeys.update(parseInt(id, 10), body);
    return NextResponse.json(virtualKey);
  } catch (error) {
    console.error('Error updating virtual key:', error);
    return NextResponse.json(
      { error: 'Failed to update virtual key' },
      { status: 500 }
    );
  }
}

// DELETE /api/virtualkeys/[id] - Delete a virtual key
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
    await adminClient.virtualKeys.deleteById(parseInt(id, 10));
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    console.error('Error deleting virtual key:', error);
    return NextResponse.json(
      { error: 'Failed to delete virtual key' },
      { status: 500 }
    );
  }
}