import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { ProviderConnectionTestRequest, ProviderConnectionTestResultDto } from '@knn_labs/conduit-admin-client';

interface TestConfigRequestBody {
  providerName: string;
  apiKey: string;
  apiEndpoint?: string;
  organizationId?: string;
  additionalConfig?: Record<string, unknown>;
}

// POST /api/providers/test-config - Test a provider configuration before saving
export async function POST(req: NextRequest): Promise<NextResponse<ProviderConnectionTestResultDto | any>> {

  try {
    const body: TestConfigRequestBody = await req.json() as TestConfigRequestBody;
    const adminClient = getServerAdminClient();
    
    // Map the incoming data to SDK expected format
    const testRequest: ProviderConnectionTestRequest = {
      providerName: body.providerName,
      apiKey: body.apiKey,
      apiBase: body.apiEndpoint, // Map apiEndpoint to apiBase
      organization: body.organizationId,
    };
    
    const result: ProviderConnectionTestResultDto = await adminClient.providers.testConfig(testRequest as any);
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}