'use client';

import { useEffect } from 'react';
import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Alert,
  LoadingOverlay,
  Paper,
} from '@mantine/core';
import { IconAlertCircle, IconSettings } from '@tabler/icons-react';
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
      <Stack gap="xl">
        <Paper p="md" withBorder>
          <LoadingOverlay visible={true} overlayProps={{ radius: 'sm', blur: 2 }} />
          <Text c="dimmed">Loading image generation models...</Text>
        </Paper>
      </Stack>
    );
  }

  if (modelsError || !models || models.length === 0) {
    return (
      <Stack gap="xl">
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="No image generation models available"
          color="red"
        >
          {modelsError 
            ? `Error loading models: ${modelsError.message}`
            : (
              <div>
                <Text size="sm">To use image generation, you need to:</Text>
                <ol style={{ marginLeft: '1rem', marginTop: '0.5rem' }}>
                  <li>Configure providers (OpenAI, MiniMax, etc.) in <strong>LLM Providers</strong></li>
                  <li>Add image generation models in <strong>Model Mappings</strong></li>
                  <li>Enable the <strong>&quot;Supports Image Generation&quot;</strong> checkbox for those models</li>
                </ol>
                <Text size="sm" mt="sm">
                  Example models: <code>dall-e-2</code>, <code>dall-e-3</code>, <code>minimax-image</code>
                </Text>
              </div>
            )
          }
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      {/* Header */}
      <Group justify="space-between">
        <div>
          <Title order={1}>Image Generation</Title>
          <Text c="dimmed">Create AI-generated images from text prompts</Text>
        </div>
        <Button 
          variant="light"
          leftSection={<IconSettings size={16} />}
          onClick={toggleSettings}
        >
          Settings
        </Button>
      </Group>

      {/* Error Display */}
      {error && (
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Error"
          color="red"
          onClose={() => setError(undefined)}
          withCloseButton
        >
          {error}
        </Alert>
      )}

      {/* Status Display */}
      {status !== 'idle' && (
        <Alert
          color={(() => {
            if (status === 'generating') return 'blue';
            if (status === 'completed') return 'green';
            return 'red';
          })()}
          title={(() => {
            if (status === 'generating') return 'Generating images...';
            if (status === 'completed') return 'Images generated successfully!';
            return 'Generation failed';
          })()}
        />
      )}

      {/* Settings Panel */}
      {settingsVisible && (
        <Paper p="md" withBorder>
          <ImageSettings models={models} />
        </Paper>
      )}

      {/* Prompt Input */}
      <Paper p="md" withBorder>
        <ImagePromptInput />
      </Paper>

      {/* Image Gallery */}
      <ImageGallery />
    </Stack>
  );
}