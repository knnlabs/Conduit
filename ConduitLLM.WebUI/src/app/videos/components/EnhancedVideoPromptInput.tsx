'use client';

import { useState, useCallback, useEffect } from 'react';
import { useVideoStore } from '../hooks/useVideoStore';
import { useVideoGeneration } from '../hooks/useVideoGeneration';
import { useEnhancedVideoGeneration } from '../hooks/useEnhancedVideoGeneration';
import { isVideoProgressTrackingEnabled, toggleVideoProgressTracking } from '@/lib/features/video-progress';
import type { VideoModel } from '../types';

interface VideoPromptInputProps {
  models: VideoModel[];
}

export default function EnhancedVideoPromptInput({}: VideoPromptInputProps) {
  const [prompt, setPrompt] = useState('');
  const [progressTrackingEnabled, setProgressTrackingEnabled] = useState(false);
  const { settings, currentTask, setError } = useVideoStore();
  
  // Use legacy hook
  const legacyHook = useVideoGeneration();
  
  // Use enhanced hook
  const enhancedHook = useEnhancedVideoGeneration({
    useProgressTracking: progressTrackingEnabled,
    fallbackToPolling: true,
  });
  
  // Select which hook to use based on feature flag
  const { generateVideo, isGenerating } = progressTrackingEnabled ? enhancedHook : legacyHook;
  
  // Check feature flag on mount
  useEffect(() => {
    setProgressTrackingEnabled(isVideoProgressTrackingEnabled());
  }, []);

  const handleSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!prompt.trim()) {
      setError('Please enter a prompt');
      return;
    }

    if (!settings.model) {
      setError('Please select a model');
      return;
    }

    setError(null);
    
    try {
      await generateVideo({
        prompt: prompt.trim(),
        settings,
      });
      // Clear prompt after successful submission
      setPrompt('');
    } catch (error) {
      // Error is handled in the hook
      console.error('Error in VideoPromptInput:', error);
    }
  }, [prompt, settings, generateVideo, setError]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      void handleSubmit(e as React.FormEvent);
    }
  };

  const handleToggleProgressTracking = () => {
    const newValue = toggleVideoProgressTracking();
    setProgressTrackingEnabled(newValue);
    setError(null); // Clear any errors
  };

  const isDisabled = isGenerating || !!currentTask;

  return (
    <form onSubmit={(e) => { e.preventDefault(); void handleSubmit(e); }} className="video-prompt-section">
      <textarea
        className="video-prompt-input"
        placeholder="Describe the video you want to generate..."
        value={prompt}
        onChange={(e) => setPrompt(e.target.value)}
        onKeyDown={(e) => void handleKeyDown(e)}
        disabled={isDisabled}
        rows={4}
      />
      
      <div className="video-prompt-controls">
        <div className="prompt-info">
          <span className="character-count">{prompt.length} characters</span>
          {currentTask && (
            <span className="generation-status">
              Video generation in progress...
            </span>
          )}
        </div>
        
        <div className="prompt-actions">
          {/* Feature toggle for testing */}
          <label className="feature-toggle" style={{ marginRight: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <input
              type="checkbox"
              checked={progressTrackingEnabled}
              onChange={handleToggleProgressTracking}
              disabled={isGenerating}
            />
            <span style={{ fontSize: '0.875rem' }}>
              Real-time progress
              {enhancedHook.signalRConnected && ' ✓'}
            </span>
          </label>
          
          <button
            type="submit"
            className="btn btn-primary"
            disabled={isDisabled || !prompt.trim()}
          >
            {isGenerating ? 'Generating...' : 'Generate Video'}
          </button>
        </div>
      </div>
    </form>
  );
}