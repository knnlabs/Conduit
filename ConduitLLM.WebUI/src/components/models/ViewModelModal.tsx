'use client';

import { useState, useEffect } from 'react';
import { Modal, Stack, Group, Text, Badge, Title, Divider, Loader } from '@mantine/core';
import type { ModelDto } from '@knn_labs/conduit-admin-client';
import { useAdminClient } from '@/lib/client/adminClient';
import { notifications } from '@mantine/notifications';

interface ViewModelModalProps {
  isOpen: boolean;
  model: ModelDto;
  onClose: () => void;
}

interface ProviderMapping {
  id: number;
  modelAlias: string;
  providerModelId: string;
  providerId: number;
  modelId: number;
  isEnabled: boolean;
  provider?: {
    id: number;
    providerType: number;
    providerName: string;
  };
}

export function ViewModelModal({ isOpen, model, onClose }: ViewModelModalProps) {
  const [providerMappings, setProviderMappings] = useState<ProviderMapping[]>([]);
  const [loadingProviders, setLoadingProviders] = useState(false);
  const { executeWithAdmin } = useAdminClient();

  const getTypeBadgeColor = (type: number | undefined) => {
    switch (type) {
      case 0: return 'blue'; // Text/Chat
      case 1: return 'purple'; // Image
      case 2: return 'orange'; // Audio
      case 3: return 'pink'; // Video
      case 4: return 'green'; // Embedding
      default: return 'gray';
    }
  };
  
  const getTypeName = (type: number | undefined) => {
    switch (type) {
      case 0: return 'Text';
      case 1: return 'Image';
      case 2: return 'Audio';
      case 3: return 'Video';
      case 4: return 'Embedding';
      default: return 'Unknown';
    }
  };

  const getProviderTypeName = (providerType: number) => {
    switch (providerType) {
      case 1: return 'OpenAI';
      case 2: return 'Groq';
      case 3: return 'Replicate';
      case 4: return 'Fireworks';
      case 5: return 'OpenAI Compatible';
      case 6: return 'MiniMax';
      case 7: return 'Ultravox';
      case 8: return 'ElevenLabs';
      case 9: return 'Cerebras';
      case 10: return 'SambaNova';
      case 11: return 'DeepInfra';
      default: return 'Unknown';
    }
  };

  const getAvailableProviders = (modelName: string): string[] => {
    const name = modelName.toLowerCase();
    const providers: string[] = [];

    // OpenAI models
    if (name.includes('gpt') || name.includes('o1') || name.includes('dall-e') || name.includes('whisper') || name.includes('tts')) {
      providers.push('OpenAI');
    }

    // Claude models
    if (name.includes('claude')) {
      providers.push('Anthropic'); // Note: Not in enum but commonly referenced
    }

    // Llama models - available on multiple providers
    if (name.includes('llama') || name.includes('llama-3')) {
      providers.push('Groq', 'Replicate', 'Fireworks', 'OpenAI Compatible', 'DeepInfra');
    }

    // Groq-specific optimized models
    if (name.includes('gemma') || name.includes('mixtral') || name.includes('whisper')) {
      providers.push('Groq');
    }

    // Video generation models - primarily Replicate
    if (name.includes('veo') || name.includes('runway') || name.includes('pika') || name.includes('video')) {
      providers.push('Replicate');
    }

    // Image generation models
    if (name.includes('flux') || name.includes('sd-') || name.includes('stable-diffusion') || name.includes('midjourney')) {
      providers.push('Replicate');
    }

    // Audio models
    if (name.includes('whisper') || name.includes('tts') || name.includes('speech')) {
      providers.push('OpenAI', 'ElevenLabs');
    }

    // Cerebras optimized models
    if (name.includes('llama-3.1') || name.includes('llama-3-70b')) {
      providers.push('Cerebras');
    }

    // SambaNova optimized models  
    if (name.includes('llama-3.1') || name.includes('llama-3.2')) {
      providers.push('SambaNova');
    }

    // OpenAI Compatible - most open source models
    if (name.includes('qwen') || name.includes('hermes') || name.includes('mistral') || name.includes('wizard')) {
      providers.push('OpenAI Compatible');
    }

    // Remove duplicates and return
    return [...new Set(providers)];
  };

  useEffect(() => {
    if (isOpen && model.name) {
      setLoadingProviders(true);
      
      // Get available providers based on model name
      const availableProviders = getAvailableProviders(model.name);
      
      // Convert to provider mapping format for display
      const providerMappings = availableProviders.map((providerName, index) => ({
        id: index,
        modelAlias: model.name ?? '',
        providerModelId: model.name ?? '',
        providerId: index,
        modelId: model.id ?? 0,
        isEnabled: true,
        provider: {
          id: index,
          providerType: 0,
          providerName: providerName
        }
      }));
      
      setProviderMappings(providerMappings as ProviderMapping[]);
      setLoadingProviders(false);
    } else {
      setProviderMappings([]);
    }
  }, [isOpen, model.name]);

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title={<Title order={3}>{model.name ?? 'Unnamed Model'}</Title>}
      size="lg"
    >
      <Stack>
        <Group justify="space-between">
          <Text fw={500}>Model Name:</Text>
          <Text>{model.name ?? '-'}</Text>
        </Group>

        <Group justify="space-between">
          <Text fw={500}>Type:</Text>
          <Badge color={getTypeBadgeColor(model.modelType)} variant="light">
            {getTypeName(model.modelType)}
          </Badge>
        </Group>

        <Group justify="space-between">
          <Text fw={500}>Status:</Text>
          <Badge color={model.isActive ? 'green' : 'gray'} variant="light">
            {model.isActive ? 'Active' : 'Inactive'}
          </Badge>
        </Group>

        <Divider />

        <Group justify="space-between">
          <Text fw={500}>Series ID:</Text>
          <Text>{model.modelSeriesId ?? '-'}</Text>
        </Group>

        {model.modelCapabilitiesId && (
          <Group justify="space-between">
            <Text fw={500}>Capabilities ID:</Text>
            <Text>{model.modelCapabilitiesId}</Text>
          </Group>
        )}

        <Divider />

        <div>
          <Text fw={500} mb="xs">Available on Providers:</Text>
          {(() => {
            if (loadingProviders) {
              return (
                <Group>
                  <Loader size="sm" />
                  <Text size="sm" c="dimmed">Loading provider information...</Text>
                </Group>
              );
            }
            
            if (providerMappings.length > 0) {
              return (
                <Group gap="xs">
                  {providerMappings.map((mapping) => (
                    <Badge
                      key={mapping.id}
                      color={mapping.isEnabled ? 'blue' : 'gray'}
                      variant="light"
                      title={mapping.isEnabled ? 'Active mapping' : 'Inactive mapping'}
                    >
                      {mapping.provider?.providerName ?? getProviderTypeName(mapping.provider?.providerType ?? 0)}
                    </Badge>
                  ))}
                </Group>
              );
            }
            
            return <Text size="sm" c="dimmed">No provider mappings found</Text>;
          })()}
        </div>

        <Divider />

        <Group justify="space-between">
          <Text fw={500} size="sm" c="dimmed">Created:</Text>
          <Text size="sm" c="dimmed">
            {model.createdAt ? new Date(model.createdAt).toLocaleString() : '-'}
          </Text>
        </Group>

        <Group justify="space-between">
          <Text fw={500} size="sm" c="dimmed">Updated:</Text>
          <Text size="sm" c="dimmed">
            {model.updatedAt ? new Date(model.updatedAt).toLocaleString() : '-'}
          </Text>
        </Group>
      </Stack>
    </Modal>
  );
}