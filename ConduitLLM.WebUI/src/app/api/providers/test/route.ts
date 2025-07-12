import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';

/**
 * POST /api/providers/test
 * 
 * Tests a provider configuration before creating it.
 * This allows validating API keys and endpoints without saving.
 */
export async function POST(request: NextRequest) {
  const auth = requireAuth(request);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const body = await request.json();
    
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
    const testResult = await adminClient.providers.testConfig({
      providerName: body.providerName,
      apiKey: body.apiKey,
      baseUrl: body.baseUrl,
      organizationId: body.organizationId,
      additionalConfig: body.additionalConfig,
    });
    
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
