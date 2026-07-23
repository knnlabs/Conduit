'use client';

import { Select, Grid, Text } from '@mantine/core';
import { useImageStore } from '../hooks/useImageStore';
import type { DiscoveryModel } from '@/app/chat/hooks/useDiscoveryModels';

interface ImageSettingsProps {
  models: DiscoveryModel[];
}

export default function ImageSettings({ models }: ImageSettingsProps) {
  const { settings, updateSettings } = useImageStore();

  const handleModelChange = (value: string | null) => {
    if (value) updateSettings({ model: value });
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
      </Grid>
    </>
  );
}
