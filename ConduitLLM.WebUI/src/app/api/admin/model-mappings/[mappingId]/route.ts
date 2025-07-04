import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const GET = createDynamicRouteHandler<{ mappingId: string }>(
  async (request, { params, auth }) => {
    try {
      const { mappingId } = params;
      
      // Get model mapping details
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.modelMappings.getById(Number(mappingId)),
        `get model mapping ${mappingId}`
      );

      return transformSDKResponse(result);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const PUT = createDynamicRouteHandler<{ mappingId: string }>(
  async (request, { params, auth }) => {
    try {
      const { mappingId } = params;
      const body = await request.json();
      
      // Update model mapping - Note: SDK update may not allow changing modelId/providerId
      const updateData: Record<string, unknown> = {};
      
      // Only include fields that can be updated - handle both field name formats
      if (body.providerModelName !== undefined || body.internalModelName !== undefined) {
        updateData.providerModelId = body.providerModelName || body.internalModelName;
      }
      if (body.priority !== undefined) updateData.priority = body.priority;
      if (body.isEnabled !== undefined) updateData.isEnabled = body.isEnabled;
      if (body.metadata !== undefined) updateData.metadata = JSON.stringify(body.metadata);
      if (body.capabilities !== undefined) updateData.capabilities = body.capabilities;
      
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.modelMappings.update(Number(mappingId), updateData),
        `update model mapping ${mappingId}`
      );

      return transformSDKResponse(result, {
        meta: {
          updated: true,
          mappingId,
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const DELETE = createDynamicRouteHandler<{ mappingId: string }>(
  async (request, { params, auth }) => {
    try {
      const { mappingId } = params;
      
      // Delete model mapping
      await withSDKErrorHandling(
        async () => auth.adminClient!.modelMappings.deleteById(Number(mappingId)),
        `delete model mapping ${mappingId}`
      );

      return transformSDKResponse(
        { message: 'Model mapping deleted successfully' },
        {
          status: 200,
          meta: {
            deleted: true,
            mappingId,
          }
        }
      );
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);