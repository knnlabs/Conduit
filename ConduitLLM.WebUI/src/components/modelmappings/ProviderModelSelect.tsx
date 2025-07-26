'use client';

import { useState, useEffect, useCallback } from 'react';
import {
  Select,
  TextInput,
  Group,
  ActionIcon,
  Loader,
} from '@mantine/core';
import { IconRefresh } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';

interface ProviderModel {
  id: string;
  name: string;
  capabilities?: string[];
}

interface ProviderModelSelectProps {
  providerId: string | undefined;
  value: string;
  onChange: (value: string) => void;
  onCapabilitiesDetected?: (capabilities: Record<string, boolean>) => void;
  label?: string;
  description?: string;
  placeholder?: string;
  required?: boolean;
  error?: string;
}

export function ProviderModelSelect({
  providerId,
  value,
  onChange,
  onCapabilitiesDetected,
  label = "Provider Model ID",
  description = "The actual model ID to use with the selected provider",
  placeholder = "Select or enter a model ID",
  required = false,
  error,
}: ProviderModelSelectProps) {
  const [models, setModels] = useState<ProviderModel[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [useCustomInput, setUseCustomInput] = useState(false);

  const fetchModels = useCallback(async () => {
    if (!providerId) {
      setModels([]);
      return;
    }

    setIsLoading(true);
    try {
      const response = await fetch(`/api/provider-models/${providerId}`);
      if (!response.ok) {
        throw new Error('Failed to fetch models');
      }
      const data = await response.json() as ProviderModel[];
      setModels(data);
      
      // If current value is not in the fetched models, switch to custom input
      if (value && data.length > 0 && !data.find((m: ProviderModel) => m.id === value)) {
        setUseCustomInput(true);
      }
    } catch (error) {
      console.error('Failed to fetch provider models:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to fetch provider models. You can still enter a model ID manually.',
        color: 'orange',
      });
      setUseCustomInput(true);
    } finally {
      setIsLoading(false);
    }
  }, [providerId, value]);

  useEffect(() => {
    void fetchModels();
  }, [providerId, fetchModels]);

  const handleRefresh = () => {
    void fetchModels();
  };

  const detectCapabilities = (modelId: string) => {
    // Detect capabilities based on model name patterns
    const capabilities: Record<string, boolean> = {
      supportsStreaming: true, // Most models support streaming by default
      supportsVision: false,
      supportsFunctionCalling: false,
      supportsImageGeneration: false,
      supportsAudioTranscription: false,
      supportsTextToSpeech: false,
      supportsRealtimeAudio: false,
    };

    const lowerModel = modelId.toLowerCase();

    // Vision models
    if (lowerModel.includes('vision') || lowerModel.includes('gpt-4o') || lowerModel.includes('claude-3')) {
      capabilities.supportsVision = true;
    }

    // Function calling
    if (lowerModel.includes('gpt-4') || lowerModel.includes('gpt-3.5-turbo') || lowerModel.includes('claude')) {
      capabilities.supportsFunctionCalling = true;
    }

    // Image generation
    if (lowerModel.includes('dall-e') || lowerModel.includes('stable-diffusion') || lowerModel.includes('midjourney')) {
      capabilities.supportsImageGeneration = true;
    }

    // Audio models
    if (lowerModel.includes('whisper')) {
      capabilities.supportsAudioTranscription = true;
    }
    if (lowerModel.includes('tts') || lowerModel.includes('eleven')) {
      capabilities.supportsTextToSpeech = true;
    }
    if (lowerModel.includes('realtime')) {
      capabilities.supportsRealtimeAudio = true;
    }

    if (onCapabilitiesDetected) {
      onCapabilitiesDetected(capabilities);
    }
  };

  const handleChange = (newValue: string | null) => {
    if (newValue) {
      onChange(newValue);
      detectCapabilities(newValue);
    }
  };

  if (!providerId) {
    return (
      <TextInput
        label={label}
        description="Select a provider first to see available models"
        placeholder={placeholder}
        disabled
        error={error}
      />
    );
  }

  if (useCustomInput || models.length === 0) {
    return (
      <Group align="flex-end" gap="xs">
        <TextInput
          label={label}
          description={description}
          placeholder={placeholder}
          required={required}
          error={error}
          value={value}
          onChange={(e) => handleChange(e.currentTarget.value)}
          style={{ flex: 1 }}
        />
        <ActionIcon
          variant="subtle"
          onClick={() => {
            setUseCustomInput(false);
            handleRefresh();
          }}
          loading={isLoading}
          disabled={isLoading}
          size="lg"
          mb={error ? 24 : 0}
        >
          <IconRefresh size={16} />
        </ActionIcon>
      </Group>
    );
  }

  const selectData = models.map(model => ({
    value: model.id,
    label: model.name || model.id,
  }));

  // Add option to use custom input
  selectData.push({
    value: '__custom__',
    label: '➕ Enter custom model ID...',
  });

  return (
    <Group align="flex-end" gap="xs">
      <Select
        label={label}
        description={description}
        placeholder={placeholder}
        required={required}
        error={error}
        data={selectData}
        value={value}
        onChange={(val) => {
          if (val === '__custom__') {
            setUseCustomInput(true);
          } else {
            handleChange(val);
          }
        }}
        searchable
        style={{ flex: 1 }}
        rightSection={isLoading ? <Loader size={16} /> : null}
      />
      <ActionIcon
        variant="subtle"
        onClick={handleRefresh}
        loading={isLoading}
        disabled={isLoading}
        size="lg"
        mb={error ? 24 : 0}
      >
        <IconRefresh size={16} />
      </ActionIcon>
    </Group>
  );
}