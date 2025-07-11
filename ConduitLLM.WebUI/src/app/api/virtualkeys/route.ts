import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/virtualkeys - List all virtual keys
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const page = parseInt(searchParams.get('page') || '1', 10);
    const pageSize = parseInt(searchParams.get('pageSize') || '100', 10);
    
    const adminClient = getServerAdminClient();
    const response = await adminClient.virtualKeys.list(page, pageSize);
    
    // Return the response as-is (includes items array and pagination info)
    return NextResponse.json(response);
  } catch (error) {
    return handleSDKError(error);
  }
}

// POST /api/virtualkeys - Create a new virtual key
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await req.json();
    console.log('[VirtualKeys] Creating virtual key with data:', body);
    
    const adminClient = getServerAdminClient();
    const virtualKey = await adminClient.virtualKeys.create(body);
    
    console.log('[VirtualKeys] Virtual key created successfully:', virtualKey);
    return NextResponse.json(virtualKey);
  } catch (error) {
    console.error('[VirtualKeys] Error creating virtual key:', error);
    return handleSDKError(error);
  }
}