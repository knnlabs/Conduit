import { NextRequest } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const POST = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, auth }) => {
    try {
      const { providerId } = params;
      
      // Test provider connection
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.testConnection(providerId),
        `test provider connection ${providerId}`
      );

      return transformSDKResponse(result, {
        meta: {
          tested: true,
          providerId,
          timestamp: new Date().toISOString(),
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);