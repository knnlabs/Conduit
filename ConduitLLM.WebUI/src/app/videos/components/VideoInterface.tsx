'use client';

import { useEffect } from 'react';
import { Stack, Title, Text, Group, Button, LoadingOverlay, Alert } from '@mantine/core';
import { IconSettings } from '@tabler/icons-react';
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
      <Stack h="100vh" p="md" pos="relative">
        <LoadingOverlay visible={true} overlayProps={{ radius: 'sm', blur: 2 }} />
      </Stack>
    );
  }

  if (modelsError || !models || !Array.isArray(models) || models.length === 0) {
    return (
      <Stack h="100vh" p="md">
        <Alert variant="light" color="red" title={modelsError ? 'Error loading models' : 'No video generation models available'}>
          {modelsError 
            ? modelsError.message
            : (
              <Stack gap="sm">
                <Text>To use video generation, you need to:</Text>
                <ol style={{ marginLeft: '1rem', marginTop: '0.5rem' }}>
                  <li>Configure providers (MiniMax, etc.) in <strong>LLM Providers</strong></li>
                  <li>Add video generation models in <strong>Model Mappings</strong></li>
                  <li>Enable the <strong>&ldquo;Supports Video Generation&rdquo;</strong> checkbox for those models</li>
                </ol>
                <Text size="sm" c="dimmed">
                  Example model: <code>minimax-video</code>
                </Text>
              </Stack>
            )
          }
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack h="100vh" p="md" gap="md">
      {/* Header */}
      <Group justify="space-between">
        <div>
          <Title order={1}>Video Generation</Title>
          <Text c="dimmed">Create AI-generated videos from text prompts</Text>
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
        <Alert variant="light" color="red" onClose={() => setError('')} withCloseButton>
          {error}
        </Alert>
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
    </Stack>
  );
}