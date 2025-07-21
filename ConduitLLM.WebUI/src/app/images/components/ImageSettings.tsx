'use client';

import { useImageStore } from '../hooks/useImageStore';
import { ImageModel } from '../hooks/useImageModels';

interface ImageSettingsProps {
  models: ImageModel[];
}

export default function ImageSettings({ models }: ImageSettingsProps) {
  const { settings, updateSettings } = useImageStore();

  const handleModelChange = (modelId: string) => {
    updateSettings({ model: modelId });
  };

  const handleSizeChange = (size: string) => {
    updateSettings({ size });
  };

  const handleQualityChange = (quality: 'standard' | 'hd') => {
    updateSettings({ quality });
  };

  const handleStyleChange = (style: 'vivid' | 'natural') => {
    updateSettings({ style });
  };

  const handleCountChange = (n: number) => {
    updateSettings({ n });
  };

  const handleResponseFormatChange = (responseFormat: 'url' | 'b64_json') => {
    updateSettings({ responseFormat });
  };

  // Get size options based on selected model
  const getSizeOptions = () => {
    const selectedModel = models.find(m => m.id === settings.model);
    const provider = selectedModel?.providerId?.toLowerCase();
    
    if (provider === 'openai') {
      if (settings.model === 'dall-e-2') {
        return ['256x256', '512x512', '1024x1024'];
      } else if (settings.model === 'dall-e-3') {
        return ['1024x1024', '1792x1024', '1024x1792'];
      }
    } else if (provider === 'minimax') {
      return ['1024x1024', '1792x1024', '1024x1792'];
    }
    
    // Default fallback
    return ['1024x1024', '1792x1024', '1024x1792'];
  };

  // Get max count based on model
  const getMaxCount = () => {
    const selectedModel = models.find(m => m.id === settings.model);
    const provider = selectedModel?.providerId?.toLowerCase();
    
    if (provider === 'openai') {
      if (settings.model === 'dall-e-2') {
        return 10;
      } else if (settings.model === 'dall-e-3') {
        return 1;
      }
    } else if (provider === 'minimax') {
      return 4;
    }
    
    return 1; // Safe default
  };

  // Check if quality is supported
  const supportsQuality = () => {
    const selectedModel = models.find(m => m.id === settings.model);
    const provider = selectedModel?.providerId?.toLowerCase();
    return provider === 'openai' && settings.model === 'dall-e-3' || provider === 'minimax';
  };

  // Check if style is supported (DALL-E 3 only)
  const supportsStyle = () => {
    return settings.model === 'dall-e-3';
  };

  const sizeOptions = getSizeOptions();
  const maxCount = getMaxCount();

  return (
    <div className="image-settings-panel">
      <div className="image-settings-grid">
        {/* Model Selection */}
        <div>
          <label htmlFor="model-select" className="block text-sm font-medium mb-1">
            Model
          </label>
          <select
            id="model-select"
            value={settings.model}
            onChange={(e) => handleModelChange(e.target.value)}
            className="w-full p-2 border border-gray-300 rounded"
          >
            {models.map((model) => (
              <option key={model.id} value={model.id}>
                {model.displayName}
              </option>
            ))}
          </select>
        </div>

        {/* Size Selection */}
        <div>
          <label htmlFor="size-select" className="block text-sm font-medium mb-1">
            Size
          </label>
          <select
            id="size-select"
            value={settings.size}
            onChange={(e) => handleSizeChange(e.target.value)}
            className="w-full p-2 border border-gray-300 rounded"
          >
            {sizeOptions.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </div>

        {/* Quality Selection (if supported) */}
        {supportsQuality() && (
          <div>
            <label htmlFor="quality-select" className="block text-sm font-medium mb-1">
              Quality
            </label>
            <select
              id="quality-select"
              value={settings.quality}
              onChange={(e) => handleQualityChange(e.target.value as 'standard' | 'hd')}
              className="w-full p-2 border border-gray-300 rounded"
            >
              <option value="standard">Standard</option>
              <option value="hd">HD</option>
            </select>
          </div>
        )}

        {/* Style Selection (DALL-E 3 only) */}
        {supportsStyle() && (
          <div>
            <label htmlFor="style-select" className="block text-sm font-medium mb-1">
              Style
            </label>
            <select
              id="style-select"
              value={settings.style}
              onChange={(e) => handleStyleChange(e.target.value as 'vivid' | 'natural')}
              className="w-full p-2 border border-gray-300 rounded"
            >
              <option value="vivid">Vivid</option>
              <option value="natural">Natural</option>
            </select>
          </div>
        )}

        {/* Count Selection */}
        <div>
          <label htmlFor="count-input" className="block text-sm font-medium mb-1">
            Number of Images (max {maxCount})
          </label>
          <input
            id="count-input"
            type="number"
            min={1}
            max={maxCount}
            value={settings.n}
            onChange={(e) => handleCountChange(Math.min(maxCount, Math.max(1, parseInt(e.target.value) || 1)))}
            className="w-full p-2 border border-gray-300 rounded"
          />
        </div>

        {/* Response Format */}
        <div>
          <label htmlFor="format-select" className="block text-sm font-medium mb-1">
            Response Format
          </label>
          <select
            id="format-select"
            value={settings.responseFormat}
            onChange={(e) => handleResponseFormatChange(e.target.value as 'url' | 'b64_json')}
            className="w-full p-2 border border-gray-300 rounded"
          >
            <option value="url">URL</option>
            <option value="b64_json">Base64</option>
          </select>
        </div>
      </div>
    </div>
  );
}