import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/unified-error-handler';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';
import { withLogging } from '@/lib/utils/logging';

export const POST = withLogging('POST /api/providers-test/[id]', 
  createDynamicRouteHandler<{ id: string }>(
    async (request, { params, adminClient }) => {
    try {
      const { id } = params;
      
      // Convert string ID to number for the SDK
      const numericId = parseInt(id, 10);
      if (isNaN(numericId)) {
        throw new Error('Invalid provider ID: must be a number');
      }
      
      const result = await withSDKErrorHandling(
        async () => {
          return await adminClient!.providers.testConnectionById(numericId);
        },
        `test provider connection ${id}`
      );

      return transformSDKResponse(result, {
        meta: {
          tested: true,
          providerId: id,
          timestamp: new Date().toISOString(),
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
    },
    { requireAdmin: true }
  )
);