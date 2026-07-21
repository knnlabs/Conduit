import { useEffect, useCallback } from 'react';
import { useDiscoveryModels, type DiscoveryModel, type DiscoveryResponse } from '@/app/chat/hooks/useDiscoveryModels';
import { useParameterState } from '@/components/parameters/hooks/useParameterState';
import type { ModelCapability } from '@/lib/gateway-api';

interface UseMediaInterfaceOptions {
  /** The model capability to filter by (e.g., ImageGeneration, VideoGeneration) */
  capability: ModelCapability;
  /** The currently selected model ID */
  currentModel: string | undefined;
  /** Callback when model selection changes */
  onModelChange: (model: string) => void;
  /** Callback when an error occurs */
  onError: (error: string) => void;
  /** Prefix for parameter persistence key */
  parameterPersistPrefix: 'image' | 'video';
}

interface UseMediaInterfaceReturn {
  /** Discovery response data containing available models */
  discoveryData: DiscoveryResponse | undefined;
  /** Whether models are currently loading */
  modelsLoading: boolean;
  /** Error from loading models, if any */
  modelsError: Error | null;
  /** The currently selected model from discovery data */
  selectedDiscoveryModel: DiscoveryModel | undefined;
  /** Parameter state for the selected model */
  parameterState: ReturnType<typeof useParameterState>;
  /** Whether there's a configuration error (no models available) */
  isConfigurationError: boolean;
  /** Whether models are available */
  hasModels: boolean;
}

/**
 * Shared hook for media interface pages (Image and Video generation).
 * Handles model discovery, parameter state, and auto-selection.
 */
export function useMediaInterface(options: UseMediaInterfaceOptions): UseMediaInterfaceReturn {
  const { capability, currentModel, onModelChange, onError, parameterPersistPrefix } = options;

  // Fetch models with capability from discovery endpoint
  const { data: discoveryData, isLoading: modelsLoading, error: modelsError } = useDiscoveryModels(capability);

  // Find selected model
  const selectedDiscoveryModel = discoveryData?.data?.find(m => m.id === currentModel);

  // Initialize parameter state with the model's parameters
  const parameterState = useParameterState({
    parameters: selectedDiscoveryModel?.parameters ?? '{}',
    persistKey: `${parameterPersistPrefix}-params-${currentModel ?? 'default'}`,
  });

  // Memoize the model change handler to prevent unnecessary effect triggers
  const handleModelChange = useCallback((model: string) => {
    onModelChange(model);
  }, [onModelChange]);

  // Auto-select first available model
  useEffect(() => {
    if (discoveryData?.data && discoveryData.data.length > 0 && !currentModel) {
      handleModelChange(discoveryData.data[0].id);
    }
  }, [discoveryData, currentModel, handleModelChange]);

  // Handle models loading error
  useEffect(() => {
    if (modelsError) {
      onError(`Failed to load models: ${modelsError.message}`);
    }
  }, [modelsError, onError]);

  return {
    discoveryData,
    modelsLoading,
    modelsError,
    selectedDiscoveryModel,
    parameterState,
    isConfigurationError: !modelsError && (!discoveryData?.data || discoveryData.data.length === 0),
    hasModels: !!discoveryData?.data && discoveryData.data.length > 0,
  };
}
