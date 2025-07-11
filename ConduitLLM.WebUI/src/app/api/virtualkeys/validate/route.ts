import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/virtualkeys/validate - Validate a virtual key
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { key } = await req.json();
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