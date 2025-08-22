'use client';

import { useEffect } from 'react';
import { Stack, Paper, LoadingOverlay, Text } from '@mantine/core';
import { useVideoStore } from '../hooks/useVideoStore';
import { useVideoModels } from '../hooks/useVideoModels';
import { ErrorDisplay } from '@/components/common/ErrorDisplay';
import { createEnhancedError } from '@/lib/utils/error-enhancement';
import { DynamicParameters } from '@/components/parameters/DynamicParameters';
import { useParameterState } from '@/components/parameters/hooks/useParameterState';
import VideoSettings from './VideoSettings';
import EnhancedVideoPromptInput from './EnhancedVideoPromptInput';
import VideoGallery from './VideoGallery';
import VideoQueue from './VideoQueue';

export default function VideoInterface() {
  const {
    error,
    settingsVisible,
    settings,
    updateSettings,
    toggleSettings,
    setError,
    currentTask,
  } = useVideoStore();

  const { data: models, isLoading: modelsLoading, error: modelsError } = useVideoModels();

  // Find the currently selected model to get its parameters
  const selectedModel = models?.find(m => m.id === settings.model);
  
  // Initialize parameter state with the model's parameters
  const parameterState = useParameterState({
    parameters: selectedModel?.parameters ?? '{}',
    persistKey: `video-params-${settings.model}`,
  });

  // Auto-select first available model
  useEffect(() => {
    if (models && Array.isArray(models) && models.length > 0 && !settings.model) {
      updateSettings({ model: models[0].id });
    }
  }, [models, settings.model, updateSettings]);

  // Handle models loading error
  useEffect(() => {
    if (modelsError) {
      setError(`Failed to load models: ${modelsError.message}`);
    }
  }, [modelsError, setError]);

  if (modelsLoading) {
    return (
      <Stack gap="xl">
        <Paper p="md" withBorder>
          <LoadingOverlay visible={true} overlayProps={{ radius: 'sm', blur: 2 }} />
          <Text c="dimmed">Loading video generation models...</Text>
        </Paper>
      </Stack>
    );
  }

  if (modelsError || !models || !Array.isArray(models) || models.length === 0) {
    const errorInstance = modelsError 
      ? new Error(`Error loading models: ${modelsError.message}`)
      : new Error('No video generation models available. Please configure providers and add video generation models.');
    
    if (modelsError) {
      errorInstance.name = 'ModelLoadError';
    } else {
      errorInstance.name = 'ConfigurationError';
    }

    return (
      <Stack gap="xl">
        <ErrorDisplay 
          error={errorInstance}
          variant="card"
          showDetails={!!modelsError}
          actions={[
            {
              label: 'Configure Providers',
              onClick: () => window.location.href = '/llm-providers',
              color: 'blue',
              variant: 'filled',
            },
            {
              label: 'Add Model Mappings', 
              onClick: () => window.location.href = '/model-mappings',
              color: 'blue',
              variant: 'light',
            }
          ]}
        />
        {!modelsError && (
          <Paper p="md" withBorder>
            <Text size="sm" mb="sm">To use video generation, you need to:</Text>
            <ol style={{ marginLeft: '1rem', marginTop: '0.5rem' }}>
              <li>Configure providers (MiniMax, etc.) in <strong>LLM Providers</strong></li>
              <li>Add video generation models in <strong>Model Mappings</strong></li>
              <li>Enable the <strong>&ldquo;Supports Video Generation&rdquo;</strong> checkbox for those models</li>
            </ol>
            <Text size="sm" mt="sm">
              Example model: <code>minimax-video</code>
            </Text>
          </Paper>
        )}
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      {/* Header */}
      <div className="video-header">
        <h1>🎬 Video Generation</h1>
        <button 
          className="settings-toggle"
          onClick={toggleSettings}
          aria-label="Toggle settings"
        >
          ⚙️ Settings
        </button>
      </div>

      {/* Error Display */}
      {error && (
        <ErrorDisplay 
          error={createEnhancedError(error)}
          variant="inline"
          showDetails={true}
          onRetry={() => setError(null)}
          actions={[
            {
              label: 'Configure Providers',
              onClick: () => window.location.href = '/llm-providers',
              color: 'blue',
              variant: 'light',
            }
          ]}
        />
      )}

      {/* Model Selector - Always Visible */}
      <Paper p="md" withBorder>
        <Stack gap="md">
          <Text fw={600}>Model Selection</Text>
          <select
            value={settings.model}
            onChange={(e) => updateSettings({ model: e.target.value })}
            className="form-select"
            style={{ 
              padding: '8px 12px',
              borderRadius: '4px',
              border: '1px solid #ced4da',
              fontSize: '14px',
              width: '100%'
            }}
          >
            <option value="">Select a model...</option>
            {models?.map((model) => (
              <option key={model.id} value={model.id}>
                {model.displayName} ({model.provider})
              </option>
            ))}
          </select>
        </Stack>
      </Paper>

      {/* Settings Panel */}
      {settingsVisible && (
        <VideoSettings models={models || []} />
      )}

      {/* Dynamic Parameters from Model */}
      {selectedModel?.parameters && selectedModel.parameters !== '{}' && (
        <DynamicParameters
          parameters={selectedModel.parameters}
          values={parameterState.values}
          onChange={parameterState.updateValues}
          context="video"
          title="Video Generation Parameters"
          collapsible={true}
          defaultExpanded={true}
        />
      )}

      {/* Generation Queue */}
      {currentTask && (
        <VideoQueue />
      )}

      {/* Prompt Input */}
      <EnhancedVideoPromptInput 
        models={models || []} 
        dynamicParameters={parameterState.getSubmitValues()}
      />

      {/* Video Gallery */}
      <VideoGallery />
    </Stack>
  );
}