import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const POST = createDynamicRouteHandler<{ mappingId: string }>(
  async (request, { params, auth }) => {
    try {
      const { mappingId } = params;
      const body = await request.json();
      
      // Test model mapping with optional test prompt
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.modelMappings.test(mappingId, {
          testPrompt: body.testPrompt || 'Hello, can you respond?',
          testParameters: body.testParameters,
        }),
        `test model mapping ${mappingId}`
      );

      return transformSDKResponse(result, {
        meta: {
          tested: true,
          mappingId,
          timestamp: new Date().toISOString(),
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);