import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import type { UpdateProviderKeyCredentialDto } from '@knn_labs/conduit-admin-client';

export async function PATCH(
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
    
    const body = await req.json() as unknown;
    const updatedKey = await adminClient.providers.updateKey(providerId, keyIdNum, body as UpdateProviderKeyCredentialDto);
    return NextResponse.json(updatedKey);
  } catch (error) {
    return handleSDKError(error);
  }
}

export async function DELETE(
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
    
    await adminClient.providers.deleteKey(providerId, keyIdNum);
    return NextResponse.json({ success: true });
  } catch (error) {
    return handleSDKError(error);
  }
}