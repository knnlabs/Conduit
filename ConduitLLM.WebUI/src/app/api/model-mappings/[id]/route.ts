import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { UpdateModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

// GET /api/model-mappings/[id] - Get a single model mapping
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

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

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    const body = await req.json() as UpdateModelProviderMappingDto & { supportsChat?: boolean };
    
    // First get the existing mapping to preserve required fields
    const existingMapping = await adminClient.modelMappings.getById(parseInt(id, 10));
    
    // The backend expects the ID in the body to match the route ID
    const transformedBody: UpdateModelProviderMappingDto & { supportsChat?: boolean } = {
      id: parseInt(id, 10), // Backend requires ID in body to match route ID
      modelId: body.modelId ?? existingMapping.modelId, // Backend requires modelId even for updates
      providerId: body.providerId, // Already a number from frontend
      providerModelId: body.providerModelId,
      priority: body.priority,
      isEnabled: body.isEnabled,
      supportsVision: body.supportsVision,
      supportsImageGeneration: body.supportsImageGeneration,
      supportsAudioTranscription: body.supportsAudioTranscription,
      supportsTextToSpeech: body.supportsTextToSpeech,
      supportsRealtimeAudio: body.supportsRealtimeAudio,
      supportsFunctionCalling: body.supportsFunctionCalling,
      supportsStreaming: body.supportsStreaming,
      supportsVideoGeneration: body.supportsVideoGeneration,
      supportsEmbeddings: body.supportsEmbeddings,
      supportsChat: body.supportsChat,
      maxContextLength: body.maxContextLength,
      maxOutputTokens: body.maxOutputTokens,
      isDefault: body.isDefault,
      metadata: body.metadata,
    };
    
    
    // Update returns void (204 No Content), so we need to fetch the updated mapping
    await adminClient.modelMappings.update(parseInt(id, 10), transformedBody);
    const updatedMapping = await adminClient.modelMappings.getById(parseInt(id, 10));
    
    return NextResponse.json(updatedMapping);
  } catch (error: unknown) {
    
    return handleSDKError(error);
  }
}

// DELETE /api/model-mappings/[id] - Delete a model mapping
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {

  try {
    const { id } = await params;
    const adminClient = getServerAdminClient();
    await adminClient.modelMappings.deleteById(parseInt(id, 10));
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    return handleSDKError(error);
  }
}