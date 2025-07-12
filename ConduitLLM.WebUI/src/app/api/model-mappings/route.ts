import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
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
    const searchParams = req.nextUrl.searchParams;
    const page = parseInt(searchParams.get('page') || '1', 10);
    const pageSize = parseInt(searchParams.get('pageSize') || '50', 10);
    
    // SDK returns paginated response
    const response = await adminClient.modelMappings.list(page, pageSize);
    
    // For backward compatibility, return just the items array
    // Components expect an array, not a paginated response
    return NextResponse.json(response.items || []);
  } catch (error) {
    return handleSDKError(error);
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
    return NextResponse.json(mapping, { status: 201 });
  } catch (error) {
    return handleSDKError(error);
  }
}
