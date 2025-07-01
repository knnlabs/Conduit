import { NextRequest } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const GET = createDynamicRouteHandler<{ id: string }>(
  async (request, { params, auth }) => {
    try {
      const { id } = params;
      
      // Get IP rule details
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.ipFilters.get(id),
        `get IP rule ${id}`
      );

      return transformSDKResponse(result);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const PUT = createDynamicRouteHandler<{ id: string }>(
  async (request, { params, auth }) => {
    try {
      const { id } = params;
      const body = await request.json();
      
      // Update IP rule
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.ipFilters.update(id, {
          ipAddress: body.ipAddress,
          action: body.action,
          description: body.description,
          expiresAt: body.expiresAt,
          metadata: body.metadata,
          isEnabled: body.isEnabled,
        }),
        `update IP rule ${id}`
      );

      return transformSDKResponse(result, {
        meta: {
          updated: true,
          ruleId: id,
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const DELETE = createDynamicRouteHandler<{ id: string }>(
  async (request, { params, auth }) => {
    try {
      const { id } = params;
      
      // Delete IP rule
      await withSDKErrorHandling(
        async () => auth.adminClient!.ipFilters.delete(id),
        `delete IP rule ${id}`
      );

      return transformSDKResponse(
        { success: true, message: 'IP rule deleted successfully' },
        {
          status: 200,
          meta: {
            deleted: true,
            ruleId: id,
          }
        }
      );
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);