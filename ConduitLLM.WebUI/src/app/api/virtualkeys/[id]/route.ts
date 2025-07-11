import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
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
    const virtualKey = await adminClient.virtualKeys.get(id);
    return NextResponse.json(virtualKey);
  } catch (error) {
    return handleSDKError(error);
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
    const virtualKey = await adminClient.virtualKeys.update(id, body);
    return NextResponse.json(virtualKey);
  } catch (error) {
    return handleSDKError(error);
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
    await adminClient.virtualKeys.delete(id);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return handleSDKError(error);
  }
}