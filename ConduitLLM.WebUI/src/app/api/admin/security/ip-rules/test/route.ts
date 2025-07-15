import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { handleSDKError } from '@/lib/errors/sdk-errors';

// POST /api/admin/security/ip-rules/test - Test an IP address
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await req.json();
    
    // IP filtering is not yet available in the current SDK version
    return NextResponse.json({ error: 'IP filtering not available' }, { status: 501 });
  } catch (error) {
    return handleSDKError(error);
  }
}