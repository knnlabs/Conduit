import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function POST(req: NextRequest) {
  try {
    const adminClient = getServerAdminClient();
    const body = await req.json() as { providerId: number; keyId: number };
    
    const { providerId, keyId } = body;
    
    if (!providerId || !keyId || isNaN(providerId) || isNaN(keyId)) {
      return NextResponse.json({ error: 'Invalid provider ID or key ID' }, { status: 400 });
    }
    
    const result = await adminClient.providers.testKey(providerId, keyId);
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}