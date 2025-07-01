import { NextRequest } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const GET = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, auth }) => {
    try {
      const { providerId } = params;
      
      // Get provider details
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.get(providerId),
        `get provider ${providerId}`
      );

      return transformSDKResponse(result);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const PUT = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, auth }) => {
    try {
      const { providerId } = params;
      const body = await request.json();
      
      // Update provider configuration
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.update(providerId, {
          providerName: body.providerName,
          providerType: body.providerType,
          apiKey: body.apiKey,
          apiUrl: body.apiUrl,
          organizationId: body.organizationId,
          additionalSettings: body.additionalSettings,
          isEnabled: body.isEnabled,
          priority: body.priority,
          metadata: body.metadata,
        }),
        `update provider ${providerId}`
      );

      return transformSDKResponse(result, {
        meta: {
          updated: true,
          providerId,
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const DELETE = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, auth }) => {
    try {
      const { providerId } = params;
      
      // Delete provider
      await withSDKErrorHandling(
        async () => auth.adminClient!.providers.delete(providerId),
        `delete provider ${providerId}`
      );

      return transformSDKResponse(
        { message: 'Provider deleted successfully' },
        {
          status: 200,
          meta: {
            deleted: true,
            providerId,
          }
        }
      );
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);