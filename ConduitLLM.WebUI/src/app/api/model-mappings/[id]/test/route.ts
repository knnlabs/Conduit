import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
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
    
    // Test if the model supports chat completions (most common capability)
    const result = await adminClient.modelMappings.testCapability(
      parseInt(id, 10),
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
  } catch (error) {
    return handleSDKError(error);
  }
}
