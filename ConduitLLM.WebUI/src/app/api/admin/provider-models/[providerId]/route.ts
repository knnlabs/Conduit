import { NextResponse } from 'next/server';
import { createDynamicRouteHandler } from '@/lib/utils/route-helpers';
import { mapSDKErrorToResponse } from '@/lib/errors/sdk-errors';

export const GET = createDynamicRouteHandler<{ providerId: string }>(
  async (request, { params, adminClient }) => {
    try {
      const { providerId } = params;
      const { searchParams } = new URL(request.url);
      const forceRefresh = searchParams.get('forceRefresh') === 'true';
      
      // Check if adminClient is available
      if (!adminClient) {
        return NextResponse.json(
          { error: 'Admin client not initialized' },
          { status: 500 }
        );
      }
      
      try {
        // Use Admin SDK to discover provider models
        const discoveredModels = await adminClient.modelMappings.discoverProviderModels(providerId);
        
        // Transform the discovery response to our expected format
        const models = (discoveredModels || []).map((model) => {
          // Extract capabilities from the model data
          const capabilities: string[] = [];
          
          // Map discovery capabilities to our UI capabilities using ACTUAL API structure
          if (model.capabilities) {
            const caps = model.capabilities;
            if (caps.chat || caps.chatStream) capabilities.push('streaming');
            if (caps.vision) capabilities.push('vision');
            if (caps.functionCalling) capabilities.push('function_calling');
            if (caps.imageGeneration) capabilities.push('image_generation');
            // Note: These capabilities don't exist in the actual API response
            // if (caps.audioTranscription) capabilities.push('audio_transcription');
            // if (caps.textToSpeech) capabilities.push('text_to_speech');
            // if (caps.realtimeAudio) capabilities.push('realtime_audio');
          }
          
          // Fall back to inferring from model name if no capabilities provided
          if (capabilities.length === 0) {
            capabilities.push(...determineCapabilities(model.modelId || model.displayName || ''));
          }
          
          return {
            id: model.modelId,
            name: model.displayName || model.modelId,
            capabilities,
            metadata: {},
          };
        });
        
        // If no models found, try the known models fallback
        if (models.length === 0) {
          const knownModels = getKnownModelsForProvider(providerId);
          knownModels.forEach(modelId => {
            models.push({
              id: modelId,
              name: modelId,
              capabilities: determineCapabilities(modelId),
              metadata: { source: 'known-models' },
            });
          });
        }
        
        return NextResponse.json({
          provider: providerId,
          models,
          source: models.length > 0 ? 'discovery-api' : 'known-models',
          cached: !forceRefresh,
        });
      } catch (error) {
        // If discovery fails, use known models as fallback
        const knownModels = getKnownModelsForProvider(providerId);
        const models = knownModels.map(modelId => ({
          id: modelId,
          name: modelId,
          capabilities: determineCapabilities(modelId),
          metadata: { source: 'fallback' },
        }));
        
        return NextResponse.json({
          provider: providerId,
          models,
          source: 'fallback',
          error: error instanceof Error ? error.message : 'Failed to retrieve models',
        }, { status: 200 }); // Still return 200 but with models from fallback
      }
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

// Helper function to get known models for a provider (fallback)
function getKnownModelsForProvider(provider: string): string[] {
  const knownModels: Record<string, string[]> = {
    openai: ['gpt-4-turbo-preview', 'gpt-4', 'gpt-3.5-turbo', 'dall-e-3', 'dall-e-2', 'text-embedding-ada-002'],
    anthropic: ['claude-3-opus-20240229', 'claude-3-sonnet-20240229', 'claude-3-haiku-20240307', 'claude-2.1', 'claude-2.0'],
    google: ['gemini-1.5-pro', 'gemini-1.5-flash', 'gemini-pro'],
    minimax: ['abab6.5-chat', 'abab6.5s-chat', 'abab5.5-chat', 'image-01', 'video-01'],
    openrouter: [
      'anthropic/claude-3-opus', 'anthropic/claude-3-sonnet', 'anthropic/claude-3-haiku',
      'openai/gpt-4-turbo', 'openai/gpt-4', 'openai/gpt-3.5-turbo',
      'google/gemini-pro', 'google/gemini-pro-vision',
      'meta-llama/llama-3-70b-instruct', 'mistralai/mistral-large'
    ],
  };
  
  return knownModels[provider.toLowerCase()] || [];
}

// Helper function to determine capabilities based on model name
function determineCapabilities(modelId: string): string[] {
  const lowerModel = modelId.toLowerCase();
  const capabilities: string[] = [];
  
  // Always include streaming as a default capability
  capabilities.push('streaming');
  
  // Vision models
  if (lowerModel.includes('vision') || lowerModel.includes('gpt-4v') || 
      lowerModel.includes('claude-3') || lowerModel.includes('gemini-pro-vision')) {
    capabilities.push('vision');
  }
  
  // Function calling models
  if (lowerModel.includes('gpt-4') || lowerModel.includes('gpt-3.5-turbo') ||
      lowerModel.includes('claude-3') || lowerModel.includes('gemini')) {
    capabilities.push('function_calling');
  }
  
  // Image generation models
  if (lowerModel.includes('dall-e') || lowerModel.includes('stable-diffusion') || 
      lowerModel.includes('midjourney') || lowerModel.includes('imagen')) {
    capabilities.push('image_generation');
  }
  
  // Audio models
  if (lowerModel.includes('whisper')) {
    capabilities.push('audio_transcription');
  }
  if (lowerModel.includes('tts') || lowerModel.includes('eleven')) {
    capabilities.push('text_to_speech');
  }
  
  // Realtime audio (for OpenAI realtime models)
  if (lowerModel.includes('realtime')) {
    capabilities.push('realtime_audio');
  }
  
  return capabilities;
}