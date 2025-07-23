import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { 
  VirtualKeyDto, 
  CreateVirtualKeyRequest
} from '@knn_labs/conduit-admin-client';

// GET /api/virtualkeys - List all virtual keys
export async function GET(req: NextRequest) {

  try {
    const { searchParams } = new URL(req.url);
    const page = parseInt(searchParams.get('page') ?? '1', 10);
    const pageSize = parseInt(searchParams.get('pageSize') ?? '100', 10);
    
    console.error('[VirtualKeys GET] Fetching virtual keys - page:', page, 'pageSize:', pageSize);
    
    const adminClient = getServerAdminClient();
    const response = await adminClient.virtualKeys.list(page, pageSize);
    
    console.error('[VirtualKeys GET] SDK response type:', typeof response);
    
    // If response is an object, check its structure
    if (typeof response === 'object' && response !== null) {
      console.error('[VirtualKeys GET] Response keys:', Object.keys(response));
      console.error('[VirtualKeys GET] Response.items exists?', 'items' in response);
      console.error('[VirtualKeys GET] Response is array?', Array.isArray(response));
      
      // Log first few characters if it has numeric keys (might be a string)
      if ('0' in response && '1' in response) {
        const sample = Object.values(response).slice(0, 10).join('');
        console.error('[VirtualKeys GET] Response sample (first 10 chars):', sample);
      }
    }
    
    // Log full response for debugging
    console.error('[VirtualKeys GET] Full SDK response:', JSON.stringify(response));
    
    // Check if response is a string that needs to be parsed
    if (typeof response === 'string') {
      console.error('[VirtualKeys GET] Response is a string, attempting to parse...');
      console.error('[VirtualKeys GET] String value:', response);
      try {
        const parsed = JSON.parse(response) as unknown;
        console.error('[VirtualKeys GET] Parsed response:', parsed);
        return NextResponse.json(parsed);
      } catch (e) {
        console.error('[VirtualKeys GET] Failed to parse string response:', e);
        // If it's a string but not JSON, return it as an error
        return NextResponse.json(
          { error: 'Invalid response format from SDK', details: response },
          { status: 500 }
        );
      }
    }
    
    // Handle both array and paginated response formats
    let virtualKeys: VirtualKeyDto[];
    let paginatedResponse: { items: VirtualKeyDto[]; totalItems: number; pageSize: number; currentPage: number; totalPages: number };
    
    if (Array.isArray(response)) {
      console.error('[VirtualKeys GET] Response is array, wrapping in paginated format');
      virtualKeys = response as VirtualKeyDto[];
      paginatedResponse = {
        items: virtualKeys,
        totalItems: virtualKeys.length,
        currentPage: page,
        pageSize: pageSize,
        totalPages: Math.ceil(virtualKeys.length / pageSize)
      };
    } else if (response && typeof response === 'object' && 'items' in response) {
      const paginatedResp = response as { items: VirtualKeyDto[]; totalItems?: number; pageSize?: number; currentPage?: number; totalPages?: number };
      virtualKeys = paginatedResp.items;
      paginatedResponse = {
        items: paginatedResp.items,
        totalItems: paginatedResp.totalItems ?? paginatedResp.items.length,
        pageSize: paginatedResp.pageSize ?? pageSize,
        currentPage: paginatedResp.currentPage ?? page,
        totalPages: paginatedResp.totalPages ?? Math.ceil((paginatedResp.totalItems ?? paginatedResp.items.length) / pageSize)
      };
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
        return key.metadata as Record<string, unknown>;
      })(),
      // Add display key field for UI (since apiKey is only returned on creation)
      displayKey: key.keyPrefix ?? `key_${String(key.id)}`
    }));
    
    // Return the processed response
    const finalResponse = { ...paginatedResponse, items: processedKeys };
      
    return NextResponse.json(finalResponse);
  } catch (error) {
    console.error('[VirtualKeys GET] Error:', error);
    return handleSDKError(error);
  }
}

// POST /api/virtualkeys - Create a new virtual key
export async function POST(req: NextRequest) {

  try {
    const body = await req.json() as CreateVirtualKeyRequest;
    console.error('[VirtualKeys] Creating virtual key with data:', body);
    
    const adminClient = getServerAdminClient();
    const virtualKey = await adminClient.virtualKeys.create(body);
    
    console.error('[VirtualKeys] Virtual key created successfully:', virtualKey);
    return NextResponse.json(virtualKey);
  } catch (error) {
    console.error('[VirtualKeys] Error creating virtual key:', error);
    return handleSDKError(error);
  }
}