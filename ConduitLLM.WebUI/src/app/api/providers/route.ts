import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/providers - List all providers
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const providers = await adminClient.providers.list();
    return NextResponse.json(providers);
  } catch (error) {
    return handleSDKError(error);
  }
}

// POST /api/providers - Create a new provider
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await req.json();
    const adminClient = getServerAdminClient();
    const provider = await adminClient.providers.create(body);
    return NextResponse.json(provider);
  } catch (error) {
    return handleSDKError(error);
  }
}