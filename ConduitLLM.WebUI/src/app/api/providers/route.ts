import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { ProviderCredentialDto, CreateProviderCredentialDto, PaginatedResponse } from '@knn_labs/conduit-admin-client';

// GET /api/providers - List all providers
export async function GET(): Promise<NextResponse<ProviderCredentialDto[] | any>> {

  try {
    const adminClient = getServerAdminClient();
    // The SDK list method expects page and pageSize parameters
    const response = await adminClient.providers.list(1, 100) as any; // Get up to 100 providers
    
    console.error('SDK providers.list() response:', response);
    
    // The SDK returns a paginated response object with items array
    let providers: ProviderCredentialDto[];
    
    if (Array.isArray(response)) {
      providers = response;
    } else if (response && typeof response === 'object' && 'items' in response) {
      // Paginated response
      providers = response.items;
    } else {
      console.error('Unexpected response from providers.list():', response);
      // Try to extract providers from common response structures
      if (response && typeof response === 'object') {
        const responseObject = response as unknown as Record<string, unknown>;
        const possibleProviders = responseObject.data ?? responseObject.providers ?? responseObject.result;
        if (Array.isArray(possibleProviders)) {
          return NextResponse.json(possibleProviders as ProviderCredentialDto[]);
        }
      }
      return NextResponse.json([]);
    }
    
    console.error('Returning providers:', providers);
    return NextResponse.json(providers);
  } catch (error) {
    console.error('Error fetching providers:', error);
    return handleSDKError(error);
  }
}

// POST /api/providers - Create a new provider
export async function POST(request: Request): Promise<NextResponse<ProviderCredentialDto | any>> {

  try {
    const body: CreateProviderCredentialDto = await request.json() as CreateProviderCredentialDto;
    const adminClient = getServerAdminClient();
    const provider: ProviderCredentialDto = await adminClient.providers.create(body as any);
    return NextResponse.json(provider);
  } catch (error) {
    return handleSDKError(error);
  }
}