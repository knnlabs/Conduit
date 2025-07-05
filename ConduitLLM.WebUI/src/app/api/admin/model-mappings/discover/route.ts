import { NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';

export const POST = withSDKAuth(
  async (request, context) => {
    try {
      const body = await request.json();
      
      // Get available providers metadata
      const allProviders = await withSDKErrorHandling(
        async () => context.adminClient!.providers.list(),
        'list providers'
      );
      
      // Convert to array and filter
      const providers = Array.from(allProviders);

      // For each provider, get available models using the SDK
      const discoveryResults = await Promise.all(
        providers
          .filter(p => !body.providerIds || body.providerIds.includes(p.providerName))
          .map(async (provider) => {
            try {
              // Get available models from the provider using the SDK
              const modelsResponse = await withSDKErrorHandling(
                async () => context.adminClient!.providerModels.getProviderModels(
                  provider.providerName.toLowerCase(),
                  { forceRefresh: body.forceRefresh || false }
                ),
                `get models for ${provider.providerName}`
              );
              
              // Transform the models to the expected format
              const models = modelsResponse.data.map(model => ({
                modelId: model.id,
                modelName: model.id,
                capabilities: determineCapabilities(model.id),
              }));
              
              return {
                providerId: provider.providerName,
                providerName: provider.providerName,
                models,
                status: 'success' as const,
              };
            } catch (error) {
              // If provider models fails, try using discovery service
              try {
                const discoveryModels = await withSDKErrorHandling(
                  async () => context.adminClient!.discovery.getProviderModels(provider.providerName.toLowerCase()),
                  `discover models for ${provider.providerName}`
                );
                
                const models = discoveryModels.models?.map(model => ({
                  modelId: model.name,
                  modelName: model.name,
                  capabilities: model.capabilities || determineCapabilities(model.name),
                })) || [];
                
                return {
                  providerId: provider.providerName,
                  providerName: provider.providerName,
                  models,
                  status: 'success' as const,
                };
              } catch (_discoveryError) {
                return {
                  providerId: provider.providerName,
                  providerName: provider.providerName,
                  models: [],
                  status: 'error' as const,
                  error: error instanceof Error ? error.message : 'Failed to retrieve models',
                };
              }
            }
          })
      );

      const allModels = discoveryResults.flatMap(r => r.models);
      const successfulProviders = discoveryResults.filter(r => r.status === 'success').length;

      return NextResponse.json({
        providers: discoveryResults,
        summary: {
          providersChecked: providers.length,
          successfulProviders,
          modelsFound: allModels.length,
          mappingsCreated: 0, // Would be set if autoCreateMappings was true
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

// Helper function to determine capabilities based on model name
function determineCapabilities(modelId: string): string[] {
  const lowerModel = modelId.toLowerCase();
  const capabilities: string[] = [];
  
  // Chat/completion models
  if (lowerModel.includes('gpt') || lowerModel.includes('claude') || lowerModel.includes('gemini') ||
      lowerModel.includes('llama') || lowerModel.includes('mixtral') || lowerModel.includes('mistral')) {
    capabilities.push('chat', 'completion');
  }
  
  // Image generation models
  if (lowerModel.includes('dall-e') || lowerModel.includes('stable-diffusion') || 
      lowerModel.includes('midjourney') || lowerModel.includes('imagen')) {
    capabilities.push('image-generation');
  }
  
  // Vision models
  if (lowerModel.includes('vision') || lowerModel.includes('gpt-4v')) {
    capabilities.push('vision');
  }
  
  // Audio models
  if (lowerModel.includes('whisper')) {
    capabilities.push('transcription');
  }
  if (lowerModel.includes('tts')) {
    capabilities.push('text-to-speech');
  }
  
  // Embedding models
  if (lowerModel.includes('embedding') || lowerModel.includes('ada')) {
    capabilities.push('embeddings');
  }
  
  // Default to chat if no specific capabilities detected
  if (capabilities.length === 0) {
    capabilities.push('chat');
  }
  
  return capabilities;
}