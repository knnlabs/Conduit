import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// POST /api/providers/[id]/test-connection - Test connection to a provider
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const result = await adminClient.providers.testConnectionById(parseInt(id, 10));
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}