'use client';

import { Paper, Title, Select, NumberInput, TextInput, SimpleGrid } from '@mantine/core';
import { useVideoStore } from '../hooks/useVideoStore';
import { VideoResolutions, type VideoModel } from '../types';

interface VideoSettingsProps {
  models: VideoModel[];
}

export default function VideoSettings({ models }: VideoSettingsProps) {
  const { settings, updateSettings } = useVideoStore();
  
  const selectedModel = models.find(m => m.id === settings.model);
  const capabilities = selectedModel?.capabilities;

  return (
    <Paper p="md" withBorder>
      <Title order={3} mb="md">Video Generation Settings</Title>
      <SimpleGrid cols={{ base: 1, sm: 2, md: 3 }} spacing="md">
        {/* Model Selection */}
        <Select
          label="Model"
          value={settings.model}
          onChange={(value) => value && updateSettings({ model: value })}
          data={models.map(model => ({
            value: model.id,
            label: model.displayName ?? model.id
          }))}
          required
        />

        {/* Duration */}
        <NumberInput
          label="Duration (seconds)"
          description={capabilities?.maxDuration ? `Maximum: ${capabilities.maxDuration}s` : undefined}
          min={1}
          max={capabilities?.maxDuration ?? 60}
          value={settings.duration}
          onChange={(value) => updateSettings({ duration: typeof value === 'number' ? value : 5 })}
          required
        />

        {/* Resolution */}
        <Select
          label="Resolution"
          value={settings.size}
          onChange={(value) => value && updateSettings({ size: value })}
          data={
            capabilities?.supportedResolutions 
              ? capabilities.supportedResolutions.map((res) => ({
                  value: res,
                  label: `${res} ${getResolutionLabel(res)}`
                }))
              : Object.entries(VideoResolutions).map(([key, value]) => ({
                  value: value,
                  label: `${value} (${key.replace(/_/g, ' ')})`
                }))
          }
          required
        />

        {/* FPS */}
        <Select
          label="Frames Per Second"
          value={settings.fps.toString()}
          onChange={(value) => value && updateSettings({ fps: parseInt(value) })}
          data={
            capabilities?.supportedFps 
              ? capabilities.supportedFps.map((fps) => ({
                  value: fps.toString(),
                  label: `${fps} FPS`
                }))
              : [24, 30, 60].map((fps) => ({
                  value: fps.toString(),
                  label: `${fps} FPS`
                }))
          }
          required
        />

        {/* Style */}
        {capabilities?.supportsCustomStyles !== false && (
          <TextInput
            label="Style"
            description="Optional style modifier"
            value={settings.style ?? ''}
            onChange={(e) => updateSettings({ style: e.currentTarget.value || undefined })}
            placeholder="e.g., cinematic, anime, realistic"
          />
        )}

        {/* Response Format */}
        <Select
          label="Response Format"
          value={settings.responseFormat}
          onChange={(value) => value && updateSettings({ responseFormat: value as 'url' | 'b64_json' })}
          data={[
            { value: 'url', label: 'URL (Recommended)' },
            { value: 'b64_json', label: 'Base64 JSON' }
          ]}
          required
        />
      </SimpleGrid>
    </Paper>
  );
}

function getResolutionLabel(resolution: string): string {
  const resolutionLabels = new Map([
    ['1280x720', '(HD)'],
    ['1920x1080', '(Full HD)'],
    ['720x1280', '(Vertical HD)'],
    ['1080x1920', '(Vertical Full HD)'],
    ['720x720', '(Square)'],
    ['720x480', '(SD)']
  ]);
  return resolutionLabels.get(resolution) ?? '';
}