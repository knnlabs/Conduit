import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// POST /api/virtualkeys/[id]/regenerate - Regenerate a virtual key
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const result = await adminClient.virtualKeys.regenerateKey(id);
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}