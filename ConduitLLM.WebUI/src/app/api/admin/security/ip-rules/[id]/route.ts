import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const GET = createDynamicRouteHandler<{ id: string }>(
  async (request, { params, adminClient }) => {
    try {
      const { id } = params;
      
      // Get IP rule details
      const result = await withSDKErrorHandling(
        async () => adminClient!.ipFilters.getById(Number(id)),
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
  async (request, { params, adminClient }) => {
    try {
      const { id } = params;
      const body = await request.json();
      
      // Update IP rule - build update data with required id field
      const result = await withSDKErrorHandling(
        async () => adminClient!.ipFilters.update(Number(id), {
          id: Number(id),
          ...(body.ipAddress !== undefined && { ipAddressOrCidr: body.ipAddress }),
          ...(body.action !== undefined && { filterType: body.action === 'allow' ? 'whitelist' : 'blacklist' }),
          ...(body.description !== undefined && { description: body.description }),
          ...(body.isEnabled !== undefined && { isEnabled: body.isEnabled }),
          ...(body.name !== undefined && { name: body.name }),
        }), // SDK type should accept this
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
  async (request, { params, adminClient }) => {
    try {
      const { id } = params;
      
      // Delete IP rule
      await withSDKErrorHandling(
        async () => adminClient!.ipFilters.deleteById(Number(id)),
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