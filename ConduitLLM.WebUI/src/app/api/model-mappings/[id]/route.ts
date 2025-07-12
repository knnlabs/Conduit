import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/model-mappings/[id] - Get a single model mapping
export async function GET(
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
    const mapping = await adminClient.modelMappings.getById(parseInt(id, 10));
    return NextResponse.json(mapping);
  } catch (error) {
    return handleSDKError(error);
  }
}

// PUT /api/model-mappings/[id] - Update a model mapping
export async function PUT(
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
    const body = await req.json();
    
    console.log('[PUT /api/model-mappings] Request body:', JSON.stringify(body, null, 2));
    console.log('[PUT /api/model-mappings] Mapping ID:', id);
    
    // First get the existing mapping to preserve the modelId
    const existingMapping = await adminClient.modelMappings.getById(parseInt(id, 10));
    console.log('[PUT /api/model-mappings] Existing mapping:', existingMapping);
    
    // Transform frontend data to match SDK UpdateModelProviderMappingDto
    // Note: Backend seems to require modelId even though SDK type doesn't include it
    // Only include fields that are defined to avoid sending nulls
    const transformedBody: any = {
      id: parseInt(id, 10), // Backend requires ID in body to match route ID
      modelId: existingMapping.modelId, // Backend requires this even for updates
    };
    
    if (body.providerId !== undefined) {
      transformedBody.providerId = body.providerId; // Already a string (provider name)
    }
    if (body.providerModelId !== undefined) {
      transformedBody.providerModelId = body.providerModelId;
    }
    if (body.priority !== undefined) {
      transformedBody.priority = body.priority;
    }
    if (body.isEnabled !== undefined) {
      transformedBody.isEnabled = body.isEnabled;
    }
    if (body.supportsVision !== undefined) {
      transformedBody.supportsVision = body.supportsVision;
    }
    if (body.supportsImageGeneration !== undefined) {
      transformedBody.supportsImageGeneration = body.supportsImageGeneration;
    }
    if (body.supportsAudioTranscription !== undefined) {
      transformedBody.supportsAudioTranscription = body.supportsAudioTranscription;
    }
    if (body.supportsTextToSpeech !== undefined) {
      transformedBody.supportsTextToSpeech = body.supportsTextToSpeech;
    }
    if (body.supportsRealtimeAudio !== undefined) {
      transformedBody.supportsRealtimeAudio = body.supportsRealtimeAudio;
    }
    if (body.supportsFunctionCalling !== undefined) {
      transformedBody.supportsFunctionCalling = body.supportsFunctionCalling;
    }
    if (body.supportsStreaming !== undefined) {
      transformedBody.supportsStreaming = body.supportsStreaming;
    }
    if (body.maxContextLength !== undefined && body.maxContextLength !== null) {
      transformedBody.maxContextLength = body.maxContextLength;
    }
    if (body.maxOutputTokens !== undefined && body.maxOutputTokens !== null) {
      transformedBody.maxOutputTokens = body.maxOutputTokens;
    }
    
    // Don't build capabilities string - let backend handle individual boolean fields
    // The capabilities field is legacy and the backend should derive it from the boolean flags
    
    if (body.metadata !== undefined) {
      transformedBody.metadata = body.metadata;
    }
    
    console.log('[PUT /api/model-mappings] Transformed body:', JSON.stringify(transformedBody, null, 2));
    
    await adminClient.modelMappings.update(parseInt(id, 10), transformedBody);
    
    // The update returns 204 No Content, so we need to fetch the updated mapping
    const updatedMapping = await adminClient.modelMappings.getById(parseInt(id, 10));
    
    console.log('[PUT /api/model-mappings] Update successful:', updatedMapping);
    return NextResponse.json(updatedMapping);
  } catch (error: any) {
    console.error('[PUT /api/model-mappings] Error:', error);
    console.error('[PUT /api/model-mappings] Error type:', error?.constructor?.name);
    console.error('[PUT /api/model-mappings] Error message:', error?.message);
    console.error('[PUT /api/model-mappings] Error response:', error?.response);
    console.error('[PUT /api/model-mappings] Error details:', error?.details);
    
    // Check if it's a validation error from the SDK
    if (error?.response?.status === 400) {
      const responseText = await error.response.text();
      console.error('[PUT /api/model-mappings] 400 response text:', responseText);
    }
    
    return handleSDKError(error);
  }
}

// DELETE /api/model-mappings/[id] - Delete a model mapping
export async function DELETE(
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
    await adminClient.modelMappings.deleteById(parseInt(id, 10));
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return handleSDKError(error);
  }
}