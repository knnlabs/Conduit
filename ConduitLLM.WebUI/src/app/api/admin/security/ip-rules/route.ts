import { NextRequest } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse, transformPaginatedResponse, extractPagination } from '@/lib/utils/sdk-transforms';
import { parseQueryParams, validateRequiredFields, createValidationError } from '@/lib/utils/route-helpers';
import type { FilterType } from '@knn_labs/conduit-admin-client';

export const GET = withSDKAuth(
  async (request, { auth }) => {
    try {
      const params = parseQueryParams(request);
      
      // Get IP rules with filtering
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.ipFilters.list({
          pageNumber: params.page,
          pageSize: params.pageSize,
          filterType: params.get('type') === 'Allow' ? 'whitelist' : params.get('type') === 'Deny' ? 'blacklist' : undefined,
          isEnabled: params.get('status') === 'active' ? true : params.get('status') === 'inactive' ? false : undefined,
          nameContains: params.search,
          sortBy: params.sortBy && params.sortOrder ? {
            field: params.sortBy,
            direction: params.sortOrder as 'asc' | 'desc'
          } : undefined,
        }),
        'list IP rules'
      );

      // The SDK returns an array directly
      return transformPaginatedResponse(result, {
        page: params.page,
        pageSize: params.pageSize,
        total: result.length,
      });
    } catch (error: any) {
      // Return empty result for 404
      if (error.statusCode === 404 || error.type === 'NOT_FOUND') {
        return transformPaginatedResponse([], {
          page: 1,
          pageSize: 20,
          total: 0,
        });
      }
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const POST = withSDKAuth(
  async (request, { auth }) => {
    try {
      const body = await request.json();
      
      // Validate required fields
      const validation = validateRequiredFields(body, ['ipAddress', 'action']);
      if (!validation.isValid) {
        return createValidationError(
          'Missing required fields',
          { missingFields: validation.missingFields }
        );
      }

      // Validate action type
      if (!['allow', 'deny'].includes(body.action)) {
        return createValidationError(
          'Invalid action. Must be either "allow" or "deny"',
          { providedAction: body.action }
        );
      }

      // Create IP rule
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.ipFilters.create({
          name: body.name || `IP Rule for ${body.ipAddress}`,
          ipAddressOrCidr: body.ipAddress,
          filterType: body.action === 'allow' ? 'whitelist' : 'blacklist',
          description: body.description,
          isEnabled: body.isEnabled ?? true,
        }),
        'create IP rule'
      );

      return transformSDKResponse(result, {
        status: 201,
        meta: {
          created: true,
          ruleId: result.id,
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

// Bulk operations endpoint
export const PUT = withSDKAuth(
  async (request, { auth }) => {
    try {
      const body = await request.json();
      const { operation, rules } = body;

      // Validate operation
      if (!['enable', 'disable', 'delete'].includes(operation)) {
        return createValidationError(
          'Invalid operation. Must be one of: enable, disable, delete',
          { providedOperation: operation }
        );
      }

      // Validate rules array
      if (!Array.isArray(rules) || rules.length === 0) {
        return createValidationError(
          'Rules must be a non-empty array',
          { providedRules: rules }
        );
      }

      // Perform operations individually since bulk operations don't exist in SDK
      const results = await Promise.allSettled(
        rules.map(async (ruleId) => {
          switch (operation) {
            case 'enable':
              return auth.adminClient!.ipFilters.enableFilter(ruleId);
            case 'disable':
              return auth.adminClient!.ipFilters.disableFilter(ruleId);
            case 'delete':
              return auth.adminClient!.ipFilters.deleteById(ruleId);
            default:
              throw new Error('Invalid operation');
          }
        })
      );

      const successful = results.filter(r => r.status === 'fulfilled').length;
      const failed = results.filter(r => r.status === 'rejected').length;

      return transformSDKResponse({
        successful,
        failed,
        total: rules.length,
        details: results.map((r, i) => ({
          ruleId: rules[i],
          status: r.status,
          error: r.status === 'rejected' ? r.reason?.message : undefined,
        })),
      }, {
        meta: {
          operation,
          affectedCount: successful,
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);