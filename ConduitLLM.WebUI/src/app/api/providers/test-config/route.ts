import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { ProviderSettings } from '@knn_labs/conduit-admin-client';
import { providerNameToType } from '@/lib/utils/providerTypeUtils';

interface RequestBody {
  providerName: string;
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
    
    const testRequest = {
      providerType: providerNameToType(body.providerName),
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