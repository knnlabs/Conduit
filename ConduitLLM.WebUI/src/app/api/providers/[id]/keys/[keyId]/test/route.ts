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
    
    // Use the test key endpoint from the SDK
    const providers = adminClient.providers;
    if (!providers || typeof providers.testKey !== 'function') {
      throw new Error('Providers service not available');
    }
    
    const result = await providers.testKey(providerId, keyIdNum);
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}