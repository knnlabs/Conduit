import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/virtualkeys - List all virtual keys
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const virtualKeys = await adminClient.virtualKeys.list();
    return NextResponse.json(virtualKeys);
  } catch (error) {
    console.error('Error fetching virtual keys:', error);
    return NextResponse.json(
      { error: 'Failed to fetch virtual keys' },
      { status: 500 }
    );
  }
}

// POST /api/virtualkeys - Create a new virtual key
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const body = await req.json();
    const virtualKey = await adminClient.virtualKeys.create(body);
    return NextResponse.json(virtualKey);
  } catch (error) {
    console.error('Error creating virtual key:', error);
    return NextResponse.json(
      { error: 'Failed to create virtual key' },
      { status: 500 }
    );
  }
}