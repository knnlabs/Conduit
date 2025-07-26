'use client';

import { Select, NumberInput, Grid, Text } from '@mantine/core';
import { useImageStore } from '../hooks/useImageStore';
import { ImageModel } from '../hooks/useImageModels';

interface ImageSettingsProps {
  models: ImageModel[];
}

export default function ImageSettings({ models }: ImageSettingsProps) {
  const { settings, updateSettings } = useImageStore();

  const handleModelChange = (value: string | null) => {
    if (value) updateSettings({ model: value });
  };

  const handleSizeChange = (value: string | null) => {
    if (value) updateSettings({ size: value as '256x256' | '512x512' | '1024x1024' | '1792x1024' | '1024x1792' });
  };

  const handleQualityChange = (value: string | null) => {
    if (value) updateSettings({ quality: value as 'standard' | 'hd' });
  };

  const handleStyleChange = (value: string | null) => {
    if (value) updateSettings({ style: value as 'vivid' | 'natural' });
  };

  const handleCountChange = (value: string | number) => {
    updateSettings({ n: Number(value) });
  };

  const handleResponseFormatChange = (value: string | null) => {
    if (value) updateSettings({ responseFormat: value as 'url' | 'b64_json' });
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

  const modelOptions = models.map((model) => ({
    value: model.id,
    label: model.displayName,
  }));

  const sizeSelectOptions = sizeOptions.map((size) => ({
    value: size,
    label: size,
  }));

  return (
    <>
      <Text fw={600} mb="md">Settings</Text>
      <Grid>
        {/* Model Selection */}
        <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
          <Select
            label="Model"
            value={settings.model}
            onChange={handleModelChange}
            data={modelOptions}
            required
          />
        </Grid.Col>

        {/* Size Selection */}
        <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
          <Select
            label="Size"
            value={settings.size}
            onChange={handleSizeChange}
            data={sizeSelectOptions}
            required
          />
        </Grid.Col>

        {/* Quality Selection (if supported) */}
        {supportsQuality() && (
          <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
            <Select
              label="Quality"
              value={settings.quality}
              onChange={handleQualityChange}
              data={[
                { value: 'standard', label: 'Standard' },
                { value: 'hd', label: 'HD' },
              ]}
              required
            />
          </Grid.Col>
        )}

        {/* Style Selection (DALL-E 3 only) */}
        {supportsStyle() && (
          <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
            <Select
              label="Style"
              value={settings.style}
              onChange={handleStyleChange}
              data={[
                { value: 'vivid', label: 'Vivid' },
                { value: 'natural', label: 'Natural' },
              ]}
              required
            />
          </Grid.Col>
        )}

        {/* Count Selection */}
        <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
          <NumberInput
            label={`Number of Images (max ${maxCount})`}
            value={settings.n}
            onChange={handleCountChange}
            min={1}
            max={maxCount}
            required
          />
        </Grid.Col>

        {/* Response Format */}
        <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
          <Select
            label="Response Format"
            value={settings.responseFormat}
            onChange={handleResponseFormatChange}
            data={[
              { value: 'url', label: 'URL' },
              { value: 'b64_json', label: 'Base64' },
            ]}
            required
          />
        </Grid.Col>
      </Grid>
    </>
  );
}