import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { ProviderType, type ProviderCredentialDto, type CreateProviderCredentialDto } from '@knn_labs/conduit-admin-client';

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
    // Validate request body
    if (!body || typeof body !== 'object') {
      return NextResponse.json(
        { error: 'Invalid request body' },
        { status: 400 }
      );
    }
    
    const adminClient = getServerAdminClient();
    const providersService = adminClient.providers;
    if (!providersService || typeof providersService.create !== 'function') {
      throw new Error('Providers service not available');
    }
    
    const bodyWithType = body as { providerType: number; isEnabled?: boolean; apiKey?: string; apiEndpoint?: string; organizationId?: string };
    const { providerType } = bodyWithType;
    
    // Extract API key from request (it's not part of provider creation)
    const apiKey = bodyWithType.apiKey;
    const apiEndpoint = bodyWithType.apiEndpoint;
    const organizationId = bodyWithType.organizationId;
    
    // Create provider data without API key
    const createData = {
      providerType: providerType as ProviderType,
      providerName: `Provider ${providerType}`, // Generate a default name
      baseUrl: apiEndpoint,
      isEnabled: bodyWithType.isEnabled ?? true,
    };
    
    const provider = await providersService.create(createData);
    
    // If API key was provided, add it to the provider
    if (apiKey && provider.id) {
      try {
        const keyData = {
          apiKey: apiKey,
          keyName: 'Primary Key',
          organization: organizationId,
          baseUrl: apiEndpoint,
        };
        
        // Create the API key for the provider
        await adminClient.providers.createKey(provider.id, keyData);
      } catch (keyError) {
        console.error('[API Route] Failed to add API key:', keyError);
        // Note: Provider was created successfully, but key addition failed
        // We'll return the provider anyway and let the user know
      }
    }
    
    return NextResponse.json(provider);
  } catch (error: unknown) {
    return handleSDKError(error);
  }
}