'use client';

import { useState, useEffect } from 'react';
import { Modal, Stack, Group, Text, Badge, Divider, Loader } from '@mantine/core';
import type { ModelDto } from '@knn_labs/conduit-admin-client';
import { useAdminClient } from '@/lib/client/adminClient';
import { getModelPrimaryType, getModelTypeBadgeColor } from '@/utils/modelHelpers';

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



  useEffect(() => {
    const loadModelProviders = async () => {
      if (!isOpen || !model.id) {
        setProviderMappings([]);
        return;
      }

      try {
        setLoadingProviders(true);
        
        // Get model identifiers from the database
        const identifiers = await executeWithAdmin(client => 
          client.models.getIdentifiers(model.id as number)
        );
        
        // Convert identifiers to provider mappings for display
        const providerMappings = identifiers.map((identifier, index) => ({
          id: index,
          modelAlias: identifier.identifier,
          providerModelId: identifier.identifier,
          providerId: index,
          modelId: model.id ?? 0,
          isEnabled: true,
          provider: {
            id: index,
            providerType: 0,
            providerName: identifier.provider.charAt(0).toUpperCase() + identifier.provider.slice(1)
          }
        }));
        
        setProviderMappings(providerMappings as ProviderMapping[]);
        
      } catch (error) {
        console.error('Failed to load model providers:', error);
        setProviderMappings([]);
      } finally {
        setLoadingProviders(false);
      }
    };

    void loadModelProviders();
  }, [isOpen, model.id, executeWithAdmin]);

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title={model.name ?? 'Unnamed Model'}
      size="lg"
    >
      <Stack>
        <Group justify="space-between">
          <Text fw={500}>Model Name:</Text>
          <Text>{model.name ?? '-'}</Text>
        </Group>

        <Group justify="space-between">
          <Text fw={500}>Type:</Text>
          <Badge color={getModelTypeBadgeColor(getModelPrimaryType(model.capabilities))} variant="light">
            {getModelPrimaryType(model.capabilities)}
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