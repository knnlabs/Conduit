import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// POST /api/virtualkeys/[id]/spend/reset - Reset spend for a virtual key
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    await adminClient.virtualKeys.resetSpend(id);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return handleSDKError(error);
  }
}