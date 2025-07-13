import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/audio-configuration - List all audio providers
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const providers = await adminClient.audio.getProviders();
    
    return NextResponse.json(providers);
  } catch (error) {
    console.error('Error fetching audio providers:', error);
    return handleSDKError(error);
  }
}

// POST /api/audio-configuration - Create a new audio provider
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const body = await req.json();
    
    const provider = await adminClient.audio.createProvider(body);
    
    return NextResponse.json(provider);
  } catch (error) {
    console.error('Error creating audio provider:', error);
    return handleSDKError(error);
  }
}