import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/virtualkeys/[id]/spend - Get spend history for a virtual key
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
    const spendHistory = await adminClient.virtualKeys.getSpend(id);
    return NextResponse.json(spendHistory);
  } catch (error) {
    return handleSDKError(error);
  }
}

// POST /api/virtualkeys/[id]/spend/reset - Reset spend for a virtual key
export async function POST(
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
    await adminClient.virtualKeys.resetSpend(id);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return handleSDKError(error);
  }
}