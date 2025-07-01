import { NextRequest } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';

export const POST = withSDKAuth(
  async (request, { auth }) => {
    try {
      const body = await request.json();
      
      // Test connection with provider configuration
      // This tests a configuration before saving it
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.testConnection({
          providerName: body.providerType || body.providerName,
          apiKey: body.apiKey,
          apiEndpoint: body.apiUrl || body.apiEndpoint,
          organizationId: body.organizationId,
          additionalConfig: body.additionalSettings ? JSON.stringify(body.additionalSettings) : body.additionalConfig,
        }),
        'test provider connection'
      );

      return transformSDKResponse(result, {
        meta: {
          tested: true,
          timestamp: new Date().toISOString(),
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);