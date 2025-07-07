import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';
import { getServerAdminClient } from '@/lib/clients/server';

export const POST = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params }) => {
    try {
      const { providerId } = params;
      
      // Convert string ID to number for the SDK
      const numericId = parseInt(providerId, 10);
      if (isNaN(numericId)) {
        throw new Error('Invalid provider ID: must be a number');
      }
      
      const adminClient = getServerAdminClient();
      
      const result = await withSDKErrorHandling(
        async () => {
          return await adminClient.providers.testConnectionById(numericId);
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