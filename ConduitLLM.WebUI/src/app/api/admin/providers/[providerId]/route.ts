import { NextRequest, NextResponse } from 'next/server';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';

export const GET = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, auth }) => {
    try {
      const { providerId } = params;
      
      // Get provider details
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.getProviderDataByNameAsync(providerId),
        `get provider ${providerId}`
      );

      return NextResponse.json(result);
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
      
      // Provider metadata cannot be updated directly.
      // To manage provider settings, use provider credentials API instead.
      return NextResponse.json(
        { 
          error: 'Provider metadata cannot be updated directly',
          message: 'Use provider credentials API to manage provider configurations' 
        },
        { status: 400 }
      );
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
      
      // Provider metadata cannot be deleted directly.
      // To remove provider access, use provider credentials API instead.
      return NextResponse.json(
        { 
          error: 'Provider metadata cannot be deleted directly',
          message: 'Use provider credentials API to remove provider configurations' 
        },
        { status: 400 }
      );
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);