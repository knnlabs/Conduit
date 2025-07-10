import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// POST /api/model-mappings/[id]/test - Test a model mapping
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    
    // Get the model mapping details
    const mapping = await adminClient.modelMappings.getById(parseInt(id, 10));
    
    if (!mapping) {
      return NextResponse.json(
        { error: 'Model mapping not found' },
        { status: 404 }
      );
    }
    
    // Check if the mapping is enabled
    if (!mapping.isEnabled) {
      return NextResponse.json({
        isSuccessful: false,
        message: 'Model mapping is disabled. Enable it first to test.',
      });
    }
    
    // Try to test a basic capability using the model
    try {
      // Test if the model supports chat completions (most common capability)
      const result = await adminClient.modelMappings.testCapability(
        mapping.modelId,
        'chat'
      );
      
      return NextResponse.json({
        isSuccessful: result.isSupported,
        message: result.isSupported 
          ? `Model mapping "${mapping.modelId}" is working correctly. Provider: ${mapping.providerId}`
          : `Model mapping test failed: Model does not support chat capability`,
        details: {
          modelId: mapping.modelId,
          providerId: mapping.providerId,
          providerModelId: mapping.providerModelId,
          capability: 'chat',
          isSupported: result.isSupported
        }
      });
    } catch (testError: any) {
      // Capability test failed
      return NextResponse.json({
        isSuccessful: false,
        message: `Failed to test model mapping: ${testError.message || 'Unknown error'}`,
        details: {
          modelId: mapping.modelId,
          providerId: mapping.providerId,
          providerModelId: mapping.providerModelId,
          error: testError.message
        }
      });
    }
  } catch (error: any) {
    console.error('Error testing model mapping:', error);
    return NextResponse.json(
      { 
        error: 'Failed to test model mapping',
        message: error.message || 'Unknown error occurred'
      },
      { status: 500 }
    );
  }
}