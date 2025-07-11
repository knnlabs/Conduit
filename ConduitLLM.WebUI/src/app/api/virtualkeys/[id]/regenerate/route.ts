import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/virtualkeys/[id]/regenerate - Regenerate a virtual key
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
    const result = await adminClient.virtualKeys.regenerateKey(id);
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}