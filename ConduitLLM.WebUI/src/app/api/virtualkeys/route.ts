import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { 
  CreateVirtualKeyRequest
} from '@knn_labs/conduit-admin-client';

// GET /api/virtualkeys - List all virtual keys
export async function GET(req: NextRequest) {

  try {
    const { searchParams } = new URL(req.url);
    const page = parseInt(searchParams.get('page') ?? '1', 10);
    const pageSize = parseInt(searchParams.get('pageSize') ?? '100', 10);
    
    // Fetching virtual keys with pagination
    
    const adminClient = getServerAdminClient();
    const response = await adminClient.virtualKeys.list(page, pageSize);
    
    // Just return the response as-is - let the frontend handle metadata parsing
    return NextResponse.json(response);
  } catch (error) {
    console.warn('[VirtualKeys GET] Error:', error);
    return handleSDKError(error);
  }
}

// POST /api/virtualkeys - Create a new virtual key
export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as CreateVirtualKeyRequest;
    
    // Validate required fields
    if (!body.keyName || typeof body.keyName !== 'string' || body.keyName.trim().length === 0) {
      return NextResponse.json(
        { error: 'Key name is required and must be a non-empty string' },
        { status: 400 }
      );
    }
    
    // Validate optional fields
    if (body.maxBudget !== undefined && (typeof body.maxBudget !== 'number' || body.maxBudget < 0)) {
      return NextResponse.json(
        { error: 'Max budget must be a positive number' },
        { status: 400 }
      );
    }
    
    if (body.allowedModels !== undefined && typeof body.allowedModels !== 'string') {
      return NextResponse.json(
        { error: 'Allowed models must be a string' },
        { status: 400 }
      );
    }
    
    // Creating virtual key
    
    const adminClient = getServerAdminClient();
    const virtualKey = await adminClient.virtualKeys.create(body);
    
    // Virtual key created successfully
    return NextResponse.json(virtualKey, { status: 201 });
  } catch (error) {
    console.error('[VirtualKeys] Error creating virtual key:', error);
    return handleSDKError(error);
  }
}