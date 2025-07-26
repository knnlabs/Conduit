import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { VirtualKeyMaintenanceResponse } from '@knn_labs/conduit-admin-client';

// POST /api/virtualkeys/maintenance - Run maintenance on virtual keys
export async function POST() {

  try {
    const adminClient = getServerAdminClient();
    const result = await adminClient.virtualKeys.maintenance() as VirtualKeyMaintenanceResponse;
    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}