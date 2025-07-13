import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// GET /api/audio-configuration/[providerId] - Get specific audio provider
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ providerId: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { providerId } = await params;
    const adminClient = getServerAdminClient();
    const provider = await adminClient.audio.getProvider(providerId);
    
    return NextResponse.json(provider);
  } catch (error) {
    console.error('Error fetching audio provider:', error);
    return handleSDKError(error);
  }
}

// PUT /api/audio-configuration/[providerId] - Update audio provider
export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ providerId: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { providerId } = await params;
    const adminClient = getServerAdminClient();
    const body = await req.json();
    
    const provider = await adminClient.audio.updateProvider(providerId, body);
    
    return NextResponse.json(provider);
  } catch (error) {
    console.error('Error updating audio provider:', error);
    return handleSDKError(error);
  }
}

// DELETE /api/audio-configuration/[providerId] - Delete audio provider
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ providerId: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { providerId } = await params;
    const adminClient = getServerAdminClient();
    await adminClient.audio.deleteProvider(providerId);
    
    return NextResponse.json({ success: true });
  } catch (error) {
    console.error('Error deleting audio provider:', error);
    return handleSDKError(error);
  }
}