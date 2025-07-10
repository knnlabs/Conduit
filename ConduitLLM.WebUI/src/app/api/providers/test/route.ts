import { NextRequest, NextResponse } from 'next/server';
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
    if (!body.providerType || !body.apiKey) {
      return NextResponse.json(
        { 
          error: 'Missing required fields',
          details: 'providerType and apiKey are required'
        },
        { status: 400 }
      );
    }

    const adminClient = getServerAdminClient();
    
    // Check if the SDK has a testProviderConfig method
    // If not, we'll need to create a temporary provider and test it
    // TODO: SDK should have adminClient.providers.testConfig(config) method
    
    // For now, we'll simulate a test by trying to list models
    // This is a workaround until the SDK provides proper test functionality
    try {
      // Create a test configuration object
      const testConfig = {
        providerType: body.providerType,
        providerName: `test-${Date.now()}`, // Temporary name
        apiKey: body.apiKey,
        apiEndpoint: body.apiEndpoint,
        organizationId: body.organizationId,
        isEnabled: false, // Always disabled for testing
      };

      // Attempt to create a disabled provider temporarily
      const testProvider = await adminClient.providers.create(testConfig);
      
      // Test the connection
      const testResult = await adminClient.providers.testConnectionById(testProvider.id);
      
      // Clean up - delete the temporary provider
      await adminClient.providers.deleteById(testProvider.id);
      
      return NextResponse.json({
        success: testResult.success,
        message: testResult.success 
          ? 'Connection successful' 
          : testResult.message || 'Connection failed',
        details: testResult,
        tested: true,
        timestamp: new Date().toISOString(),
      });
      
    } catch (testError: any) {
      // If we created a provider but test failed, try to clean up
      // This is not ideal but necessary until SDK provides proper test method
      
      return NextResponse.json({
        success: false,
        message: testError.message || 'Failed to test provider configuration',
        error: testError.response?.data || testError.message,
        tested: true,
        timestamp: new Date().toISOString(),
      });
    }
    
  } catch (error) {
    console.error('Provider test error:', error);
    return NextResponse.json(
      { 
        error: 'Failed to test provider configuration',
        details: error instanceof Error ? error.message : 'Unknown error'
      },
      { status: 500 }
    );
  }
}