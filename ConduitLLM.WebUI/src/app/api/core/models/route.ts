import { NextRequest } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';

export const GET = withSDKAuth(
  async (request, { auth }) => {
    try {
      // Use the admin client to get available models from model mappings
      const result = await withSDKErrorHandling(
        async () => auth.adminClient!.modelMappings.list({
          pageNumber: 1,
          pageSize: 100, // Get all models in one page
          isEnabled: true, // Only get enabled models
        }),
        'list available models'
      );

      // Transform the model mappings to a simple model list
      // The SDK returns an array directly
      const models = result.map((mapping: any) => ({
        id: mapping.modelId,
        object: 'model',
        created: new Date(mapping.createdAt).getTime() / 1000,
        owned_by: mapping.providerId,
      }));

      // Return in OpenAI compatible format
      return transformSDKResponse({
        object: 'list',
        data: models,
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);