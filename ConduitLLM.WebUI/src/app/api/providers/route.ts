import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { ProviderType, type ProviderCredentialDto, type CreateProviderCredentialDto } from '@knn_labs/conduit-admin-client';
import { providerNameToType } from '@/lib/utils/providerTypeUtils';

// GET /api/providers - List all providers
export async function GET() {

  try {
    const adminClient = getServerAdminClient();
    const providersService = adminClient.providers;
    if (!providersService || typeof providersService.list !== 'function') {
      throw new Error('Providers service not available');
    }
    
    // The SDK list method expects page and pageSize parameters
    const response = await providersService.list(1, 100); // Get up to 100 providers
    
    console.warn('SDK providers.list() response:', response);
    
    // The SDK returns a paginated response object with items array
    let providers: ProviderCredentialDto[];
    
    if (Array.isArray(response)) {
      providers = response;
    } else if (response && typeof response === 'object' && 'items' in response) {
      // Paginated response
      const paginatedResponse = response as { items: ProviderCredentialDto[] };
      providers = paginatedResponse.items;
    } else {
      console.warn('Unexpected response from providers.list():', response);
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
    
    console.warn('Returning providers:', providers);
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
    const providersService = adminClient.providers;
    if (!providersService || typeof providersService.create !== 'function') {
      throw new Error('Providers service not available');
    }
    
    // Ensure isEnabled has a value (default to true if not provided)
    // Convert providerName to providerType if needed
    let providerType: number | undefined;
    const bodyWithType = body as { providerType?: number; providerName?: string; isEnabled?: boolean };
    
    if (bodyWithType.providerType !== undefined) {
      providerType = bodyWithType.providerType;
    } else if (bodyWithType.providerName) {
      // Backward compatibility - convert name to type
      providerType = providerNameToType(bodyWithType.providerName);
    }
    
    const createData = {
      ...body,
      providerType: (providerType ?? ProviderType.OpenAI) as ProviderType, // Default to OpenAI if no type provided
      isEnabled: bodyWithType.isEnabled ?? true,
    };
    
    const provider = await providersService.create(createData);
    return NextResponse.json(provider);
  } catch (error: unknown) {
    return handleSDKError(error);
  }
}