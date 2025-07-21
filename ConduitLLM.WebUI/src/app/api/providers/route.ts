import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { ProviderCredentialDto, CreateProviderCredentialDto } from '@knn_labs/conduit-admin-client';

// GET /api/providers - List all providers
export async function GET() {

  try {
    const adminClient = getServerAdminClient();
    // The SDK list method expects page and pageSize parameters
    const response = await adminClient.providers.list(1, 100); // Get up to 100 providers
    
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
        const responseObject = response as Record<string, unknown>; // Fallback parsing for unexpected response format
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
export async function POST(request: Request) {

  try {
    const body = await request.json() as CreateProviderCredentialDto;
    const adminClient = getServerAdminClient();
    
    // Ensure isEnabled has a value (default to true if not provided)
    const createData = {
      providerName: body.providerName,
      apiBase: body.apiBase,
      apiKey: body.apiKey,
      isEnabled: body.isEnabled ?? true,
      organization: body.organization
    };
    
    const provider: ProviderCredentialDto = await adminClient.providers.create(createData);
    return NextResponse.json(provider);
  } catch (error) {
    return handleSDKError(error);
  }
}