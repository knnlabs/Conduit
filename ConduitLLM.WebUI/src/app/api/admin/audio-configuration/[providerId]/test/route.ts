import { NextResponse } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const POST = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, adminClient }) => {
    try {
      const { providerId } = params;
      
      // Test audio provider connection
      const result = await withSDKErrorHandling(
        async () => adminClient!.audioConfiguration.testProvider(providerId),
        'test audio provider'
      );

      return NextResponse.json(result);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);