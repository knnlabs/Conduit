'use client';

import { useState } from 'react';
import { useImageStore } from '../hooks/useImageStore';

export default function ImagePromptInput() {
  const { 
    prompt, 
    status, 
    setPrompt, 
    generateImages, 
    clearResults 
  } = useImageStore();

  const [localPrompt, setLocalPrompt] = useState(prompt);

  const handlePromptChange = (value: string) => {
    setLocalPrompt(value);
    setPrompt(value);
  };

  const handleGenerate = async () => {
    if (!localPrompt.trim()) {
      return;
    }
    await generateImages();
  };

  const handleClear = () => {
    clearResults();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      handleGenerate();
    }
  };

  const isGenerating = status === 'generating';

  return (
    <div className="image-prompt-section">
      <label htmlFor="prompt-input" className="block text-sm font-medium mb-2">
        Image Prompt
      </label>
      
      <textarea
        id="prompt-input"
        value={localPrompt}
        onChange={(e) => handlePromptChange(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="Describe the image you want to generate... (Ctrl+Enter to generate)"
        className="image-prompt-input"
        disabled={isGenerating}
        rows={4}
      />
      
      <div className="image-prompt-controls">
        <div className="text-sm text-gray-500">
          {localPrompt.length > 0 && `${localPrompt.length} characters`}
          {localPrompt.length > 1000 && ' (very long prompt)'}
        </div>
        
        <div className="flex gap-2">
          {status === 'completed' && (
            <button
              onClick={handleClear}
              className="btn btn-secondary"
              disabled={isGenerating}
            >
              Clear Results
            </button>
          )}
          
          <button
            onClick={handleGenerate}
            disabled={!localPrompt.trim() || isGenerating}
            className="btn btn-primary"
          >
            {isGenerating ? (
              <>
                <span className="animate-spin">⏳</span>
                Generating...
              </>
            ) : (
              <>
                🎨 Generate Images
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}