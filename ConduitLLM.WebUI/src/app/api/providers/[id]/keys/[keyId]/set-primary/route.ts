import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ id: string; keyId: string }> }
) {
  try {
    const adminClient = getServerAdminClient();
    const { id, keyId } = await params;
    const providerId = parseInt(id, 10);
    const keyIdNum = parseInt(keyId, 10);
    
    if (isNaN(providerId) || isNaN(keyIdNum)) {
      return NextResponse.json({ error: 'Invalid ID' }, { status: 400 });
    }
    
    await adminClient.providers.setPrimaryKey(providerId, keyIdNum);
    return NextResponse.json({ success: true });
  } catch (error) {
    return handleSDKError(error);
  }
}