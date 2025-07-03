import { NextRequest } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const POST = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, auth }) => {
    try {
      const { providerId } = params;
      
      // To test a provider connection, we need to get the credential first
      // since we can't test connection for provider metadata directly
      const result = await withSDKErrorHandling(
        async () => {
          // This endpoint expects a credential ID, not a provider name
          // We should return an error indicating the correct endpoint to use
          throw new Error('Use /api/admin/providers/test-connection with provider configuration to test connections');
        },
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