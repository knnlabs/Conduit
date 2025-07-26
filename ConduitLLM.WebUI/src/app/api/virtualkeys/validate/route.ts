import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

interface VirtualKeyValidateRequest {
  key: string;
}

// POST /api/virtualkeys/validate - Validate a virtual key
export async function POST(req: NextRequest) {
  try {
    const { key } = await req.json() as VirtualKeyValidateRequest;
    if (!key) {
      return NextResponse.json(
        { error: 'Key is required' },
        { status: 400 }
      );
    }
    
    const adminClient = getServerAdminClient();
    const result = await adminClient.virtualKeys.validate(key);
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}