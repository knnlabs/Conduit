import { NextRequest } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';

export const POST = withSDKAuth(
  async (request, { auth }) => {
    try {
      const body = await request.json();
      
      // Get available providers
      const providers = await withSDKErrorHandling(
        async () => auth.adminClient!.providers.list({
          isEnabled: !body.includeDisabledProviders,
        }),
        'list providers'
      );

      // For each provider, get available models and suggest mappings
      const discoveryResults = await Promise.all(
        providers
          .filter(p => !body.providerIds || body.providerIds.includes(p.id))
          .map(async (provider) => {
            try {
              // Get available models from provider (this would need actual provider API calls)
              // For now, return sample data
              return {
                providerId: provider.id,
                providerName: provider.providerName,
                models: getSampleModelsForProvider(provider.providerName),
                status: 'success' as const,
              };
            } catch (error) {
              return {
                providerId: provider.id,
                providerName: provider.providerName,
                models: [],
                status: 'error' as const,
                error: error instanceof Error ? error.message : 'Unknown error',
              };
            }
          })
      );

      const allModels = discoveryResults.flatMap(r => r.models);
      const successfulProviders = discoveryResults.filter(r => r.status === 'success').length;

      return transformSDKResponse({
        providers: discoveryResults,
        summary: {
          providersChecked: providers.length,
          successfulProviders,
          modelsFound: allModels.length,
          mappingsCreated: 0, // Would be set if autoCreateMappings was true
        }
      }, {
        meta: {
          discovered: true,
          timestamp: new Date().toISOString(),
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

// Sample models for demonstration
function getSampleModelsForProvider(providerName: string): Array<{
  modelId: string;
  modelName: string;
  capabilities: string[];
}> {
  const models: Record<string, Array<{ modelId: string; modelName: string; capabilities: string[] }>> = {
    'OpenAI': [
      { modelId: 'gpt-4', modelName: 'GPT-4', capabilities: ['chat', 'completion'] },
      { modelId: 'gpt-3.5-turbo', modelName: 'GPT-3.5 Turbo', capabilities: ['chat', 'completion'] },
      { modelId: 'dall-e-3', modelName: 'DALL-E 3', capabilities: ['image-generation'] },
    ],
    'Anthropic': [
      { modelId: 'claude-3-opus-20240229', modelName: 'Claude 3 Opus', capabilities: ['chat', 'completion'] },
      { modelId: 'claude-3-sonnet-20240229', modelName: 'Claude 3 Sonnet', capabilities: ['chat', 'completion'] },
    ],
    'Google': [
      { modelId: 'gemini-pro', modelName: 'Gemini Pro', capabilities: ['chat', 'completion'] },
      { modelId: 'gemini-pro-vision', modelName: 'Gemini Pro Vision', capabilities: ['chat', 'vision'] },
    ],
  };
  
  return models[providerName] || [];
}