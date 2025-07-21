import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { ProviderConnectionTestResultDto, ProviderSettings } from '@knn_labs/conduit-admin-client';

interface TestProviderRequest {
  providerName: string;
  apiKey: string;
  apiEndpoint?: string;
  organizationId?: string;
  additionalConfig?: ProviderSettings;
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
    const body = await request.json() as TestProviderRequest;
    
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
    
    // Use the SDK's testConfig method with ProviderConfig interface
    const testResult = await adminClient.providers.testConfig({
      providerName: body.providerName,
      apiKey: body.apiKey,
      baseUrl: body.apiEndpoint,
      organizationId: body.organizationId,
      additionalConfig: body.additionalConfig
    });
    
    return NextResponse.json({
      success: testResult.success,
      message: testResult.message || (testResult.success ? 'Connection successful' : 'Connection failed'),
      details: testResult,
      tested: true,
      timestamp: new Date().toISOString(),
    });
    
  } catch (error) {
    return handleSDKError(error) as NextResponse<TestProviderErrorResponse>;
  }
}
