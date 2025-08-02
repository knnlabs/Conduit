import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { 
  VirtualKeyDto, 
  CreateVirtualKeyRequest,
  PaginatedResponse 
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
    
    console.warn('[VirtualKeys GET] SDK response type:', typeof response);
    
    // If response is an object, check its structure
    if (typeof response === 'object' && response !== null) {
      console.warn('[VirtualKeys GET] Response keys:', Object.keys(response));
      console.warn('[VirtualKeys GET] Response.items exists?', 'items' in response);
      console.warn('[VirtualKeys GET] Response is array?', Array.isArray(response));
      
      // Log first few characters if it has numeric keys (might be a string)
      if ('0' in response && '1' in response) {
        const sample = Object.values(response).slice(0, 10).join('');
        console.warn('[VirtualKeys GET] Response sample (first 10 chars):', sample);
      }
    }
    
    // Log full response for debugging
    console.warn('[VirtualKeys GET] Full SDK response:', JSON.stringify(response));
    
    // Check if response is a string that needs to be parsed
    if (typeof response === 'string') {
      console.warn('[VirtualKeys GET] Response is a string, attempting to parse...');
      console.warn('[VirtualKeys GET] String value:', response);
      try {
        const parsed = JSON.parse(response) as PaginatedResponse<VirtualKeyDto> | VirtualKeyDto[];
        console.warn('[VirtualKeys GET] Parsed response:', parsed);
        return NextResponse.json(parsed);
      } catch (e) {
        console.warn('[VirtualKeys GET] Failed to parse string response:', e);
        // If it's a string but not JSON, return it as an error
        return NextResponse.json(
          { error: 'Invalid response format from SDK', details: response },
          { status: 500 }
        );
      }
    }
    
    // Handle both array and paginated response formats
    let virtualKeys: VirtualKeyDto[];
    let paginatedResponse: PaginatedResponse<VirtualKeyDto>;
    
    if (Array.isArray(response)) {
      console.warn('[VirtualKeys GET] Response is array, wrapping in paginated format');
      virtualKeys = response as VirtualKeyDto[];
      paginatedResponse = {
        items: virtualKeys,
        totalCount: virtualKeys.length,
        pageNumber: page,
        pageSize: pageSize,
        totalPages: Math.ceil(virtualKeys.length / pageSize)
      };
    } else if (response && typeof response === 'object' && 'items' in response) {
      const paginatedResp = response as unknown as PaginatedResponse<VirtualKeyDto>;
      virtualKeys = paginatedResp.items;
      paginatedResponse = paginatedResp;
    } else {
      // Fallback for unexpected response format
      return NextResponse.json(response);
    }
    
    // Process virtual keys to parse metadata and add display key
    const processedKeys = virtualKeys.map((key: VirtualKeyDto) => ({
      ...key,
      // Parse metadata JSON if it's a string
      metadata: (() => {
        if (!key.metadata) return null;
        if (typeof key.metadata === 'string') {
          try {
            return JSON.parse(key.metadata) as Record<string, unknown>;
          } catch {
            return null;
          }
        }
        return key.metadata;
      })(),
      // Add display key field for UI (since apiKey is only returned on creation)
      displayKey: key.keyPrefix ?? `key_${String(key.id)}`
    }));
    
    // Return the processed response
    const finalResponse = { ...paginatedResponse, items: processedKeys };
      
    return NextResponse.json(finalResponse);
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