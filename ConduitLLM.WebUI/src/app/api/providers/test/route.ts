import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { ProviderConnectionTestRequest, ProviderConnectionTestResultDto } from '@knn_labs/conduit-admin-client';

interface TestProviderRequestBody {
  providerName: string;
  apiKey: string;
  apiEndpoint?: string;
  organizationId?: string;
  additionalConfig?: Record<string, unknown>;
}

interface TestProviderResponse {
  success: boolean;
  message: string;
  details: ProviderConnectionTestResultDto;
  tested: boolean;
  timestamp: string;
}

interface TestProviderErrorResponse {
  error: string;
  details: string;
}

/**
 * POST /api/providers/test
 * 
 * Tests a provider configuration before creating it.
 * This allows validating API keys and endpoints without saving.
 */
export async function POST(request: NextRequest): Promise<NextResponse<TestProviderResponse | TestProviderErrorResponse>> {

  try {
    const body: TestProviderRequestBody = await request.json() as TestProviderRequestBody;
    
    // Validate required fields
    if (!body.providerName || !body.apiKey) {
      return NextResponse.json(
        { 
          error: 'Missing required fields',
          details: 'providerName and apiKey are required'
        },
        { status: 400 }
      );
    }

    const adminClient = getServerAdminClient();
    
    // Use the SDK's testConfig method
    const testRequest: ProviderConnectionTestRequest = {
      providerName: body.providerName,
      apiKey: body.apiKey,
      apiBase: body.apiEndpoint, // SDK expects apiBase, but we receive apiEndpoint
      organization: body.organizationId,
    };
    
    const testResult: ProviderConnectionTestResultDto = await adminClient.providers.testConfig(testRequest);
    
    return NextResponse.json({
      success: testResult.success,
      message: testResult.message || (testResult.success ? 'Connection successful' : 'Connection failed'),
      details: testResult,
      tested: true,
      timestamp: new Date().toISOString(),
    });
    
  } catch (error) {
    return handleSDKError(error);
  }
}
