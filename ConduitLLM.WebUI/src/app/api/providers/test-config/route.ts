import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { ProviderSettings } from '@knn_labs/conduit-admin-client';
import { providerNameToType } from '@/lib/utils/providerTypeUtils';

interface RequestBody {
  providerType?: number;
  providerName?: string; // For backward compatibility
  apiKey: string;
  apiEndpoint?: string;
  organizationId?: string;
  additionalConfig?: ProviderSettings;
}

// POST /api/providers/test-config - Test a provider configuration before saving
export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as RequestBody;
    const adminClient = getServerAdminClient();
    
    // Handle both providerType and providerName for backward compatibility
    let providerType: number;
    if (body.providerType !== undefined) {
      providerType = body.providerType;
    } else if (body.providerName) {
      providerType = providerNameToType(body.providerName);
    } else {
      throw new Error('Either providerType or providerName must be provided');
    }
    
    const testRequest = {
      providerType,
      apiKey: body.apiKey,
      baseUrl: body.apiEndpoint,
      organizationId: body.organizationId,
      additionalConfig: body.additionalConfig
    };
    
    const result = await adminClient.providers.testConfig(testRequest);
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}