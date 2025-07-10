import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/model-mappings - List all model mappings
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const mappings = await adminClient.modelMappings.list();
    return NextResponse.json(mappings);
  } catch (error) {
    console.error('Error fetching model mappings:', error);
    return NextResponse.json(
      { error: 'Failed to fetch model mappings' },
      { status: 500 }
    );
  }
}

// POST /api/model-mappings - Create a new model mapping
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const body = await req.json();
    const mapping = await adminClient.modelMappings.create(body);
    return NextResponse.json(mapping);
  } catch (error) {
    console.error('Error creating model mapping:', error);
    return NextResponse.json(
      { error: 'Failed to create model mapping' },
      { status: 500 }
    );
  }
}