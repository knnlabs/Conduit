import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/providers - List all providers
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    // The SDK list method expects page and pageSize parameters
    const response = await adminClient.providers.list(1, 100); // Get up to 100 providers
    
    console.log('SDK providers.list() response:', response);
    
    // The SDK returns a paginated response object with items array
    const providers = response.items || response;
    
    // Ensure we always return an array
    if (!Array.isArray(providers)) {
      console.error('Unexpected response from providers.list():', response);
      // Try to extract providers from common response structures
      if (response && typeof response === 'object') {
        const responseAny = response as any;
        const possibleProviders = responseAny.data || responseAny.providers || responseAny.result;
        if (Array.isArray(possibleProviders)) {
          return NextResponse.json(possibleProviders);
        }
      }
      return NextResponse.json([]);
    }
    
    console.log('Returning providers:', providers);
    return NextResponse.json(providers);
  } catch (error) {
    console.error('Error fetching providers:', error);
    return handleSDKError(error);
  }
}

// POST /api/providers - Create a new provider
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await req.json();
    const adminClient = getServerAdminClient();
    const provider = await adminClient.providers.create(body);
    return NextResponse.json(provider);
  } catch (error) {
    return handleSDKError(error);
  }
}