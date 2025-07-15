import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// POST /api/providers/test-config - Test a provider configuration before saving
export async function POST(req: NextRequest) {

  try {
    const body = await req.json();
    const adminClient = getServerAdminClient();
    
    // Map the incoming data to SDK expected format
    const result = await adminClient.providers.testConfig({
      providerName: body.providerName,
      apiKey: body.apiKey,
      baseUrl: body.apiEndpoint, // Map apiEndpoint to baseUrl
      organizationId: body.organizationId,
      additionalConfig: body.additionalConfig,
    });
    
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}