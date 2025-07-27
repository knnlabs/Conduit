import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { ProviderType } from '@knn_labs/conduit-admin-client';

interface RouteParams {
  params: Promise<{
    provider: string;
  }>;
}

// Type guard to check if a number is a valid ProviderType
function isValidProviderType(value: number): value is ProviderType {
  // Get all numeric values from the enum
  const validValues = Object.values(ProviderType).filter(
    (v): v is number => typeof v === 'number'
  );
  return validValues.includes(value);
}

export async function GET(req: NextRequest, { params }: RouteParams) {
  try {
    const { provider } = await params;
    
    // Parse the provider parameter as a number
    const providerNumber = parseInt(provider, 10);
    
    // Validate it's a valid number
    if (isNaN(providerNumber)) {
      return NextResponse.json(
        { error: `Invalid provider type: ${provider}` },
        { status: 400 }
      );
    }
    
    // Check if it's a valid ProviderType using type guard
    if (!isValidProviderType(providerNumber)) {
      return NextResponse.json(
        { error: `Invalid provider type: ${provider}` },
        { status: 400 }
      );
    }
    
    // providerNumber is now typed as ProviderType thanks to the type guard
    const adminClient = getServerAdminClient();
    const result = await adminClient.modelCosts.getByProvider(providerNumber);

    return NextResponse.json(result);
  } catch (error) {
    console.error('[ModelCosts] GET by provider error:', error);
    return handleSDKError(error);
  }
}