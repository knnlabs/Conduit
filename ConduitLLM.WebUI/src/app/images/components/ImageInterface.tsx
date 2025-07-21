'use client';

import { useEffect } from 'react';
import { useImageStore } from '../hooks/useImageStore';
import { useImageModels } from '../hooks/useImageModels';
import ImageSettings from './ImageSettings';
import ImagePromptInput from './ImagePromptInput';
import ImageGallery from './ImageGallery';

export default function ImageInterface() {
  const {
    status,
    error,
    settingsVisible,
    settings,
    updateSettings,
    toggleSettings,
    setError,
  } = useImageStore();

  const { data: models, isLoading: modelsLoading, error: modelsError } = useImageModels();

  // Auto-select first available model
  useEffect(() => {
    if (models && models.length > 0 && !settings.model) {
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
      <div className="image-interface">
        <div className="image-generation-status">
          Loading image generation models...
        </div>
      </div>
    );
  }

  if (modelsError || !models || models.length === 0) {
    return (
      <div className="image-interface">
        <div className="image-generation-status status-error">
          {modelsError 
            ? `Error loading models: ${modelsError.message}`
            : (
              <div>
                <strong>No image generation models available.</strong>
                <p>To use image generation, you need to:</p>
                <ol style={{ marginLeft: '1rem', marginTop: '0.5rem' }}>
                  <li>Configure providers (OpenAI, MiniMax, etc.) in <strong>LLM Providers</strong></li>
                  <li>Add image generation models in <strong>Model Mappings</strong></li>
                  <li>Enable the <strong>&quot;Supports Image Generation&quot;</strong> checkbox for those models</li>
                </ol>
                <p style={{ marginTop: '0.5rem' }}>
                  Example models: <code>dall-e-2</code>, <code>dall-e-3</code>, <code>minimax-image</code>
                </p>
              </div>
            )
          }
        </div>
      </div>
    );
  }

  return (
    <div className="image-interface">
      {/* Header */}
      <div className="image-header">
        <h1>🎨 Image Generation</h1>
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
        <div className="image-generation-status status-error">
          {error}
        </div>
      )}

      {/* Settings Panel */}
      {settingsVisible && (
        <ImageSettings models={models} />
      )}

      {/* Status Display */}
      {status !== 'idle' && (
        <div className={`image-generation-status ${(() => {
          if (status === 'generating') return 'status-generating';
          if (status === 'completed') return 'status-completed';
          return 'status-error';
        })()}`}>
          {status === 'generating' && '🎨 Generating images...'}
          {status === 'completed' && '✅ Images generated successfully!'}
          {status === 'error' && error && `❌ ${error}`}
        </div>
      )}

      {/* Prompt Input */}
      <ImagePromptInput />

      {/* Image Gallery */}
      <ImageGallery />
    </div>
  );
}