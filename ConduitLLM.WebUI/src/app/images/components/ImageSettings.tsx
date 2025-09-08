'use client';

import { Select, Grid, Text } from '@mantine/core';
import { useImageStore } from '../hooks/useImageStore';
import { useModelMetadata } from '../hooks/useModelMetadata';
import type { DiscoveryModel } from '@/app/chat/hooks/useDiscoveryModels';

interface ImageMetadata {
  sizes?: string[];
  qualityOptions?: string[];
  styleOptions?: string[];
  maxImages?: number;
}

interface ImageSettingsProps {
  models: DiscoveryModel[];
}

export default function ImageSettings({ models }: ImageSettingsProps) {
  const { settings, updateSettings } = useImageStore();

  const handleModelChange = (value: string | null) => {
    if (value) updateSettings({ model: value });
  };

  // Removed handlers for Size, N, and ResponseFormat as these are now handled
  // by custom parameters or hardcoded defaults

  const handleQualityChange = (value: string | null) => {
    if (value) updateSettings({ quality: value as 'standard' | 'hd' });
  };

  const handleStyleChange = (value: string | null) => {
    if (value) updateSettings({ style: value as 'vivid' | 'natural' });
  };

  // Get model metadata
  const { data: metadataResponse } = useModelMetadata(settings.model ?? null);
  const imageMetadata = (metadataResponse as { metadata?: { image?: ImageMetadata } } | null)?.metadata?.image;

  // Check if quality is supported from metadata
  const supportsQuality = () => {
    return !!imageMetadata?.qualityOptions && imageMetadata.qualityOptions.length > 0;
  };

  // Check if style is supported from metadata
  const supportsStyle = () => {
    return !!imageMetadata?.styleOptions && imageMetadata.styleOptions.length > 0;
  };

  const modelOptions = models.map((model) => ({
    value: model.id,
    label: model.display_name ?? model.id,
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

        {/* Quality Selection (if supported) */}
        {supportsQuality() && (
          <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
            <Select
              label="Quality"
              value={settings.quality}
              onChange={handleQualityChange}
              data={imageMetadata?.qualityOptions?.map(q => ({
                value: q,
                label: q.charAt(0).toUpperCase() + q.slice(1)
              })) ?? []}
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
              data={imageMetadata?.styleOptions?.map(s => ({
                value: s,
                label: s.charAt(0).toUpperCase() + s.slice(1)
              })) ?? []}
              required
            />
          </Grid.Col>
        )}
      </Grid>
    </>
  );
}