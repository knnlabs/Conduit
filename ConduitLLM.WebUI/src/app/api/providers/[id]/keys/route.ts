import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import type { CreateProviderKeyCredentialDto } from '@knn_labs/conduit-admin-client';

export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const adminClient = getServerAdminClient();
    const { id } = await params;
    const providerId = parseInt(id, 10);
    
    if (isNaN(providerId)) {
      return NextResponse.json({ error: 'Invalid provider ID' }, { status: 400 });
    }
    
    const keys = await adminClient.providers.listKeys(providerId);
    return NextResponse.json(keys);
  } catch (error) {
    return handleSDKError(error);
  }
}

export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const adminClient = getServerAdminClient();
    const { id } = await params;
    const providerId = parseInt(id, 10);
    
    if (isNaN(providerId)) {
      return NextResponse.json({ error: 'Invalid provider ID' }, { status: 400 });
    }
    
    const body = await req.json() as unknown;
    const newKey = await adminClient.providers.createKey(providerId, body as CreateProviderKeyCredentialDto);
    return NextResponse.json(newKey);
  } catch (error) {
    return handleSDKError(error);
  }
}