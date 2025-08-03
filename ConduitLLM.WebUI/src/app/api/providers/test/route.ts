import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { ApiKeyTestResult, type ProviderSettings } from '@knn_labs/conduit-admin-client';
import { providerNameToType } from '@/lib/utils/providerTypeUtils';

interface TestProviderRequest {
  providerName: string;
  apiKey: string;
  apiEndpoint?: string;
  organizationId?: string;
  additionalConfig?: ProviderSettings;
}

/**
 * POST /api/providers/test
 * 
 * Tests a provider configuration before creating it.
 * This allows validating API keys and endpoints without saving.
 */
export async function POST(request: NextRequest) {

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
      providerType: providerNameToType(body.providerName),
      apiKey: body.apiKey,
      baseUrl: body.apiEndpoint,
      organizationId: body.organizationId,
      additionalConfig: body.additionalConfig
    });
    
    // Convert new response format to legacy format for backward compatibility
    const isSuccess = testResult.result === ApiKeyTestResult.SUCCESS;
    
    return NextResponse.json({
      success: isSuccess,
      message: testResult.message,
      details: testResult,
      tested: true,
      timestamp: new Date().toISOString(),
    });
    
  } catch (error) {
    return handleSDKError(error);
  }
}
