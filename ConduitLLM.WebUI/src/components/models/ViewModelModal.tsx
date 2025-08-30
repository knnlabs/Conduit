'use client';

import { useState, useEffect } from 'react';
import { Modal, Stack, Group, Text, Badge, Divider, Loader, ScrollArea } from '@mantine/core';
import { CodeHighlight } from '@mantine/code-highlight';
import { notifications } from '@mantine/notifications';
import type { ModelDto } from '@knn_labs/conduit-admin-client';
import { useAdminClient } from '@/lib/client/adminClient';
import { getModelPrimaryType, getModelTypeBadgeColor } from '@/utils/modelHelpers';
import { useModelSeriesById } from '@/hooks/useModelSeries';
import { getProviderTypeName } from '@/constants/providers';
import { getErrorMessage, isProviderMapping } from '@/utils/typeGuards';
import { ParameterPreview } from '@/components/parameters/ParameterPreview';

// Extend ModelDto to include modelParameters until SDK types are updated
interface ExtendedModelDto extends ModelDto {
  modelParameters?: string | null;
}

interface ViewModelModalProps {
  isOpen: boolean;
  model: ExtendedModelDto;
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
  const [capabilitiesName, setCapabilitiesName] = useState<string | null>(null);
  const { executeWithAdmin } = useAdminClient();
  const { seriesName, seriesParameters } = useModelSeriesById(model.modelSeriesId);





  useEffect(() => {
    const loadModelDetails = async () => {
      if (!isOpen || !model.id) {
        setProviderMappings([]);
        setCapabilitiesName(null);
        return;
      }

      try {
        setLoadingProviders(true);
        
        // Load all data in parallel
        const promises: Promise<void>[] = [];
        
        // Get model identifiers
        promises.push(
          executeWithAdmin(client => 
            client.models.getIdentifiers(model.id as number)
          ).then(identifiers => {
            // Convert identifiers to provider mappings for display
            const mappings = identifiers.map((identifier, index) => ({
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
            
            // Validate mappings before setting
            const validMappings = mappings.filter(isProviderMapping);
            setProviderMappings(validMappings);
          }).catch((error) => {
            const errorMessage = getErrorMessage(error);
            console.warn('Failed to load model providers:', errorMessage);
            setProviderMappings([]);
            
            notifications.show({
              title: 'Warning',
              message: 'Provider information could not be loaded',
              color: 'yellow',
            });
          })
        );
        
        // Use embedded capabilities from the model
        if (model.capabilities) {
          const capabilities = model.capabilities;
          const capList: string[] = [];
          if (capabilities.supportsChat) capList.push('Chat');
          if (capabilities.supportsVision) capList.push('Vision');
          if (capabilities.supportsImageGeneration) capList.push('Image Gen');
          if (capabilities.supportsVideoGeneration) capList.push('Video Gen');
          if (capabilities.supportsEmbeddings) capList.push('Embeddings');
          if (capabilities.supportsFunctionCalling) capList.push('Functions');
          
          const capabilitiesSummary = capList.length > 0 
            ? capList.join(', ') 
            : 'Standard';
            
          setCapabilitiesName(capabilitiesSummary);
        } else {
          setCapabilitiesName('No capabilities defined');
        }
        
        await Promise.all(promises);
        
      } catch (error) {
        const errorMessage = getErrorMessage(error);
        console.error('Failed to load model details:', errorMessage);
        
        notifications.show({
          title: 'Error',
          message: 'Some model details could not be loaded',
          color: 'red',
        });
      } finally {
        setLoadingProviders(false);
      }
    };

    void loadModelDetails();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, model.id, model.modelCapabilitiesId]);

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
          <Text fw={500}>Series:</Text>
          <Text>{seriesName ?? (model.modelSeriesId ? `Loading...` : '-')}</Text>
        </Group>

        <Group justify="space-between">
          <Text fw={500}>Capabilities:</Text>
          <Text>{capabilitiesName ?? (model.modelCapabilitiesId ? `Loading...` : '-')}</Text>
        </Group>

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
                      {mapping.provider?.providerName ?? (mapping.provider?.providerType ? getProviderTypeName(mapping.provider.providerType) : 'Unknown')}
                    </Badge>
                  ))}
                </Group>
              );
            }
            
            return <Text size="sm" c="dimmed">No provider mappings found</Text>;
          })()}
        </div>

        {(() => {
          // Use model parameters if available, otherwise fall back to series parameters
          const parametersToShow = model.modelParameters ?? seriesParameters;
          
          if (parametersToShow) {
            return (
              <>
                <Divider />
                <Stack gap="xs">
                  <Text fw={500}>UI Parameters:</Text>
                  {model.modelParameters && (
                    <Text size="xs" c="dimmed">
                      Using model-specific parameters
                    </Text>
                  )}
                  {!model.modelParameters && seriesParameters && (
                    <Text size="xs" c="dimmed">
                      Using series default parameters
                    </Text>
                  )}
                  <ParameterPreview 
                    parametersJson={parametersToShow}
                    context="chat"
                    label="Preview UI Components"
                    maxHeight={300}
                  />
                  <ScrollArea h={200}>
                    <CodeHighlight
                      code={(() => {
                        try {
                          return JSON.stringify(JSON.parse(parametersToShow), null, 2);
                        } catch {
                          return parametersToShow;
                        }
                      })()}
                      language="json"
                      withCopyButton={false}
                    />
                  </ScrollArea>
                </Stack>
              </>
            );
          }
          return null;
        })()}

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