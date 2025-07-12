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
    
    // Transform frontend data to match backend DTO expectations
    const transformedBody = {
      id: parseInt(id, 10),
      modelId: body.modelAlias,
      providerModelId: body.providerModelName,
      providerId: body.providerId.toString(),
      priority: body.priority || 0,
      isEnabled: body.isEnabled !== undefined ? body.isEnabled : true,
      supportsVision: body.supportsVision || false,
      supportsImageGeneration: body.supportsImageGeneration || false,
      supportsAudioTranscription: body.supportsAudioTranscription || false,
      supportsTextToSpeech: body.supportsTextToSpeech || false,
      supportsRealtimeAudio: body.supportsRealtimeAudio || false,
      supportsVideoGeneration: false,
      supportsEmbeddings: false,
      capabilities: JSON.stringify({
        functionCalling: body.supportsFunctionCalling || false,
        streaming: body.supportsStreaming || false,
      }),
      maxContextLength: body.maxInputTokens || null,
      notes: body.notes || null,
    };
    
    const mapping = await adminClient.modelMappings.update(parseInt(id, 10), transformedBody);
    return NextResponse.json(mapping);
  } catch (error) {
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