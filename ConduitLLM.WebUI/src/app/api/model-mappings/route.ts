import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/model-mappings - List all model mappings
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const searchParams = req.nextUrl.searchParams;
    const page = parseInt(searchParams.get('page') || '1', 10);
    const pageSize = parseInt(searchParams.get('pageSize') || '50', 10);
    
    // SDK returns paginated response
    const response = await adminClient.modelMappings.list(page, pageSize);
    
    // For backward compatibility, return just the items array
    // Components expect an array, not a paginated response
    return NextResponse.json(response.items || []);
  } catch (error) {
    return handleSDKError(error);
  }
}

// POST /api/model-mappings - Create a new model mapping
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const body = await req.json();
    
    // Log the incoming data for debugging
    console.log('[Model Mappings] Creating with data:', JSON.stringify(body, null, 2));
    
    // Transform frontend data to match SDK DTO expectations
    const transformedBody = {
      modelId: body.modelAlias,
      providerModelId: body.providerModelName,
      providerId: body.providerId.toString(), // Backend expects string
      priority: body.priority || 0,
      isEnabled: true, // Default to enabled
      supportsVision: body.supportsVision || false,
      supportsImageGeneration: body.supportsImageGeneration || false,
      supportsAudioTranscription: body.supportsAudioTranscription || false,
      supportsTextToSpeech: body.supportsTextToSpeech || false,
      supportsRealtimeAudio: body.supportsRealtimeAudio || false,
      supportsFunctionCalling: body.supportsFunctionCalling || false,
      supportsStreaming: body.supportsStreaming || false,
      maxContextLength: body.maxInputTokens || null,
      maxOutputTokens: body.maxOutputTokens || null,
      metadata: body.notes || null,
    };
    
    console.log('[Model Mappings] Transformed data:', JSON.stringify(transformedBody, null, 2));
    
    const mapping = await adminClient.modelMappings.create(transformedBody);
    return NextResponse.json(mapping, { status: 201 });
  } catch (error) {
    console.error('[Model Mappings] Creation error:', error);
    return handleSDKError(error);
  }
}
