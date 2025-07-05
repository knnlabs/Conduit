import { NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse } from '@/lib/errors/sdk-errors';
import { parseQueryParams } from '@/lib/utils/route-helpers';
import { config, getAdminApiUrl } from '@/config';

// TODO: Replace with SDK methods when security service is added (see issue #274)
// This is a temporary implementation that maintains authentication consistency
// while we await SDK support for security endpoints

const adminApiUrl = getAdminApiUrl();
const masterKey = config.auth.masterKey;

export const GET = withSDKAuth(
  async (request) => {
    try {
      const params = parseQueryParams(request);
      const status = params.get('status');
      const severity = params.get('severity');
      const page = params.page || 1;
      const pageSize = params.pageSize || 20;

      // Build query parameters
      const queryParams = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
        ...(status && { status }),
        ...(severity && { severity }),
      });

      // Temporary: Direct API call until SDK support is added
      const response = await fetch(
        `${adminApiUrl}/v1/security/threats?${queryParams}`,
        {
          method: 'GET',
          headers: {
            'Authorization': `Bearer ${masterKey}`,
            'Content-Type': 'application/json',
          },
        }
      );

      if (!response.ok) {
        // If the endpoint doesn't exist yet, return empty data structure
        if (response.status === 404) {
          return NextResponse.json({
            items: [],
            totalCount: 0,
            pageNumber: page,
            pageSize: pageSize,
            totalPages: 0,
          });
        }
        
        const error = await response.text();
        throw new Error(error || 'Failed to fetch threat detections');
      }

      const data = await response.json();
      return NextResponse.json(data);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const PUT = withSDKAuth(
  async (request) => {
    try {
      const { threatId, action } = await request.json();

      if (!threatId || !action) {
        return NextResponse.json(
          { error: 'Threat ID and action are required' },
          { status: 400 }
        );
      }

      // Temporary: Direct API call until SDK support is added
      const response = await fetch(
        `${adminApiUrl}/v1/security/threats/${threatId}/action`,
        {
          method: 'PUT',
          headers: {
            'Authorization': `Bearer ${masterKey}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ action }),
        }
      );

      if (!response.ok) {
        const error = await response.text();
        throw new Error(error || 'Failed to update threat status');
      }

      const data = await response.json();
      return NextResponse.json(data);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);