'use client';

import { useEffect } from 'react';
import { useVideoStore } from '../hooks/useVideoStore';
import { useVideoModels } from '../hooks/useVideoModels';
import VideoSettings from './VideoSettings';
import VideoPromptInput from './VideoPromptInput';
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
      <div className="video-interface">
        <div className="video-generation-status">
          Loading video generation models...
        </div>
      </div>
    );
  }

  if (modelsError || !models || !Array.isArray(models) || models.length === 0) {
    return (
      <div className="video-interface">
        <div className="video-generation-status status-error">
          {modelsError 
            ? `Error loading models: ${modelsError.message}`
            : (
              <div>
                <strong>No video generation models available.</strong>
                <p>To use video generation, you need to:</p>
                <ol style={{ marginLeft: '1rem', marginTop: '0.5rem' }}>
                  <li>Configure providers (MiniMax, etc.) in <strong>LLM Providers</strong></li>
                  <li>Add video generation models in <strong>Model Mappings</strong></li>
                  <li>Enable the <strong>"Supports Video Generation"</strong> checkbox for those models</li>
                </ol>
                <p style={{ marginTop: '0.5rem' }}>
                  Example model: <code>minimax-video</code>
                </p>
              </div>
            )
          }
        </div>
      </div>
    );
  }

  return (
    <div className="video-interface">
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
        <div className="video-generation-status status-error">
          {error}
        </div>
      )}

      {/* Settings Panel */}
      {settingsVisible && (
        <VideoSettings models={models || []} />
      )}

      {/* Generation Queue */}
      {currentTask && (
        <VideoQueue />
      )}

      {/* Prompt Input */}
      <VideoPromptInput models={models || []} />

      {/* Video Gallery */}
      <VideoGallery />
    </div>
  );
}