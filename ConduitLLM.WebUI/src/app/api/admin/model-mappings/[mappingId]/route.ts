import { NextRequest } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const GET = createDynamicRouteHandler<{ mappingId: string }>(
  async (request, { params, auth }) => {
    try {
      const { mappingId } = params;
      
      // Get model mapping details
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.modelMappings.get(mappingId),
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
      
      // Update model mapping
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.modelMappings.update(mappingId, {
          modelName: body.modelName,
          providerId: body.providerId,
          providerModelName: body.providerModelName,
          priority: body.priority,
          isEnabled: body.isEnabled,
          metadata: body.metadata,
          capabilities: body.capabilities,
          costMultiplier: body.costMultiplier,
        }),
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
        async () => auth.adminClient!.modelMappings.delete(mappingId),
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