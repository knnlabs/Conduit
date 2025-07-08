import { NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { parseQueryParams } from '@/lib/utils/route-helpers';

export const GET = withSDKAuth(
  async (request, context) => {
    try {
      const params = parseQueryParams(request);
      
      // List all provider metadata
      const result = await withSDKErrorHandling(
        async () => context.adminClient!.providers.list(),
        'list providers'
      );

      // Convert to array and apply filters
      const resultArray = Array.from(result);
      const filteredResult = resultArray.filter(provider => {
        if (params.get('providerName')) {
          return provider.providerName.toLowerCase().includes(params.get('providerName')!.toLowerCase());
        }
        return true;
      });

      // Return the filtered result directly
      return NextResponse.json(filteredResult);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const POST = withSDKAuth(
  async (request, context) => {
    try {
      const body = await request.json();
      
      // Create provider credential (not provider metadata)
      const providerData: any = {
        providerName: body.providerName,
        apiKey: body.apiKey,
        organizationId: body.organizationId,
        additionalConfig: body.additionalSettings ? JSON.stringify(body.additionalSettings) : body.additionalConfig,
        isEnabled: body.isEnabled ?? true,
      };
      
      // Only add apiEndpoint if it has a value
      const endpoint = body.apiUrl || body.apiEndpoint;
      if (endpoint) {
        providerData.apiEndpoint = endpoint;
      }
      
      const result = await withSDKErrorHandling(
        async () => context.adminClient!.providers.create(providerData),
        'create provider credential'
      );

      // Test connection if requested
      if (body.testConnection) {
        try {
          const testData: any = {
            providerName: body.providerName,
            apiKey: body.apiKey,
            organizationId: body.organizationId,
            additionalConfig: body.additionalSettings ? JSON.stringify(body.additionalSettings) : body.additionalConfig,
          };
          
          // Only add apiEndpoint if it has a value
          if (endpoint) {
            testData.apiEndpoint = endpoint;
          }
          
          await withSDKErrorHandling(
            async () => context.adminClient!.providers.testConnection(testData),
            'test provider connection'
          );
        } catch (testError) {
          // Log test failure but still return created provider
          console.warn('Provider credential created but connection test failed:', testError);
        }
      }

      // Return the SDK response directly
      return NextResponse.json(result, { status: 201 });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);