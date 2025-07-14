'use client';

import { useState, useCallback } from 'react';
import { useVideoStore } from '../hooks/useVideoStore';
import { useVideoGeneration } from '../hooks/useVideoGeneration';
import type { VideoModel } from '../types';

interface VideoPromptInputProps {
  models: VideoModel[];
}

export default function VideoPromptInput({ models }: VideoPromptInputProps) {
  const [prompt, setPrompt] = useState('');
  const { settings, currentTask, setError } = useVideoStore();
  const { generateVideo, isGenerating } = useVideoGeneration();

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
    }
  }, [prompt, settings, generateVideo, setError]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSubmit(e as any);
    }
  };

  const isDisabled = isGenerating || !!currentTask;

  return (
    <form onSubmit={handleSubmit} className="video-prompt-section">
      <textarea
        className="video-prompt-input"
        placeholder="Describe the video you want to generate..."
        value={prompt}
        onChange={(e) => setPrompt(e.target.value)}
        onKeyDown={handleKeyDown}
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
        
        <button
          type="submit"
          className="btn btn-primary"
          disabled={isDisabled || !prompt.trim()}
        >
          {isGenerating ? 'Generating...' : 'Generate Video'}
        </button>
      </div>
    </form>
  );
}