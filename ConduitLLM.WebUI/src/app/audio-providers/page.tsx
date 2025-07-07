'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Button,
  Group,
  Table,
  Badge,
  ActionIcon,
  Modal,
  TextInput,
  PasswordInput,
  Select,
  Switch,
  Tooltip,
  ScrollArea,
  LoadingOverlay,
  Alert,
  JsonInput,
  ThemeIcon,
} from '@mantine/core';
import {
  IconPlus,
  IconEdit,
  IconTrash,
  IconTestPipe,
  IconCloud,
  IconAlertCircle,
  IconRefresh,
  IconMicrophone,
  IconVolume,
  IconLanguage,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/lib/utils/fetch-wrapper';
import { reportError } from '@/lib/utils/logging';

// Audio provider types
type AudioProviderType = 'openai' | 'elevenlabs' | 'azure' | 'google' | 'aws';

interface AudioProvider {
  id: string;
  name: string;
  type: AudioProviderType;
  enabled: boolean;
  capabilities: {
    transcription: boolean;
    textToSpeech: boolean;
    translation: boolean;
    realtime: boolean;
  };
  config: {
    apiKey?: string;
    endpoint?: string;
    region?: string;
    voiceSettings?: Record<string, unknown>;
  };
  statistics: {
    requestsToday: number;
    successRate: number;
    avgLatency: number;
    lastChecked: Date;
  };
  createdAt: Date;
  updatedAt: Date;
}

// Helper function to determine provider type from name
function getProviderTypeFromName(name: string): AudioProviderType {
  const nameLower = name.toLowerCase();
  if (nameLower.includes('openai')) return 'openai';
  if (nameLower.includes('elevenlabs')) return 'elevenlabs';
  if (nameLower.includes('azure')) return 'azure';
  if (nameLower.includes('google')) return 'google';
  if (nameLower.includes('aws')) return 'aws';
  return 'openai'; // default
}

// Transform SDK DTO to UI AudioProvider
function transformAudioProvider(dto: unknown): AudioProvider {
  if (typeof dto !== 'object' || dto === null) {
    throw new Error('Invalid provider data');
  }
  const providerDto = dto as {
    id?: string;
    name?: string;
    isEnabled?: boolean;
    supportedOperations?: string[];
    apiKey?: string;
    baseUrl?: string;
    settings?: { region?: string; voiceSettings?: Record<string, unknown> };
    createdAt?: string;
    updatedAt?: string;
  };
  const providerType = getProviderTypeFromName(providerDto.name || '');
  
  return {
    id: providerDto.id || '',
    name: providerDto.name || '',
    type: providerType,
    enabled: providerDto.isEnabled || false,
    capabilities: {
      transcription: providerDto.supportedOperations?.includes('transcription') || false,
      textToSpeech: providerDto.supportedOperations?.includes('text-to-speech') || false,
      translation: providerDto.supportedOperations?.includes('translation') || false,
      realtime: providerDto.supportedOperations?.includes('realtime') || false,
    },
    config: {
      apiKey: providerDto.apiKey ? '***hidden***' : undefined,
      endpoint: providerDto.baseUrl,
      region: providerDto.settings?.region,
      voiceSettings: providerDto.settings?.voiceSettings,
    },
    statistics: {
      requestsToday: 0, // TODO: Get from usage analytics
      successRate: 99.0, // TODO: Get from health metrics
      avgLatency: 250, // TODO: Get from health metrics
      lastChecked: new Date(),
    },
    createdAt: new Date(providerDto.createdAt || Date.now()),
    updatedAt: new Date(providerDto.updatedAt || Date.now()),
  };
}

// React Query hooks for audio providers
function useAudioProviders() {
  return useQuery({
    queryKey: ['audio-providers'],
    queryFn: async (): Promise<AudioProvider[]> => {
      try {
        const response = await apiFetch('/api/admin/audio-configuration', {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          const error = await response.json();
          throw new Error(error.error || 'Failed to fetch audio providers');
        }

        const data = await response.json();
        return data.map(transformAudioProvider);
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch audio providers');
        throw new Error(error instanceof Error ? error.message : 'Failed to fetch audio providers');
      }
    },
    staleTime: 30 * 1000, // 30 seconds
  });
}

function useCreateAudioProvider() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (data: {
      name: string;
      type: AudioProviderType;
      apiKey: string;
      endpoint: string;
      region?: string;
      capabilities: {
        transcription: boolean;
        textToSpeech: boolean;
        translation: boolean;
        realtime: boolean;
      };
      advancedConfig: string;
    }) => {
      try {
        const supportedOperations: string[] = [];
        if (data.capabilities.transcription) supportedOperations.push('transcription');
        if (data.capabilities.textToSpeech) supportedOperations.push('text-to-speech');
        if (data.capabilities.translation) supportedOperations.push('translation');
        if (data.capabilities.realtime) supportedOperations.push('realtime');
        
        let settings: Record<string, unknown> = {};
        if (data.region) settings.region = data.region;
        if (data.advancedConfig) {
          try {
            const parsedConfig = JSON.parse(data.advancedConfig);
            settings = { ...settings, ...parsedConfig };
          } catch (_e) {
            // Ignore invalid JSON
          }
        }
        
        const request = {
          name: data.name,
          baseUrl: data.endpoint,
          apiKey: data.apiKey,
          isEnabled: true,
          supportedOperations,
          priority: 1,
          timeoutSeconds: 30,
          settings,
        };
        
        const response = await apiFetch('/api/admin/audio-configuration', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(request),
        });

        if (!response.ok) {
          const error = await response.json();
          throw new Error(error.error || 'Failed to create audio provider');
        }

        const responseData = await response.json();
        return transformAudioProvider(responseData);
      } catch (error: unknown) {
        reportError(error, 'Failed to create audio provider');
        throw new Error(error instanceof Error ? error.message : 'Failed to create audio provider');
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['audio-providers'] });
    },
  });
}

function useUpdateAudioProvider() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (data: {
      id: string;
      name: string;
      type: AudioProviderType;
      apiKey: string;
      endpoint: string;
      region?: string;
      capabilities: {
        transcription: boolean;
        textToSpeech: boolean;
        translation: boolean;
        realtime: boolean;
      };
      advancedConfig: string;
    }) => {
      try {
        const supportedOperations: string[] = [];
        if (data.capabilities.transcription) supportedOperations.push('transcription');
        if (data.capabilities.textToSpeech) supportedOperations.push('text-to-speech');
        if (data.capabilities.translation) supportedOperations.push('translation');
        if (data.capabilities.realtime) supportedOperations.push('realtime');
        
        let settings: Record<string, unknown> = {};
        if (data.region) settings.region = data.region;
        if (data.advancedConfig) {
          try {
            const parsedConfig = JSON.parse(data.advancedConfig);
            settings = { ...settings, ...parsedConfig };
          } catch (_e) {
            // Ignore invalid JSON
          }
        }
        
        const request = {
          name: data.name,
          baseUrl: data.endpoint,
          apiKey: data.apiKey,
          isEnabled: true,
          supportedOperations,
          priority: 1,
          timeoutSeconds: 30,
          settings,
        };
        
        const response = await apiFetch(`/api/admin/audio-configuration/${data.id}`, {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(request),
        });

        if (!response.ok) {
          const error = await response.json();
          throw new Error(error.error || 'Failed to update audio provider');
        }

        const responseData = await response.json();
        return transformAudioProvider(responseData);
      } catch (error: unknown) {
        reportError(error, 'Failed to update audio provider');
        throw new Error(error instanceof Error ? error.message : 'Failed to update audio provider');
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['audio-providers'] });
    },
  });
}

function useDeleteAudioProvider() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (providerId: string) => {
      try {
        const response = await apiFetch(`/api/admin/audio-configuration/${providerId}`, {
          method: 'DELETE',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          const error = await response.json();
          throw new Error(error.error || 'Failed to delete audio provider');
        }
      } catch (error: unknown) {
        reportError(error, 'Failed to delete audio provider');
        throw new Error(error instanceof Error ? error.message : 'Failed to delete audio provider');
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['audio-providers'] });
    },
  });
}

function useTestAudioProvider() {
  return useMutation({
    mutationFn: async (providerId: string) => {
      try {
        const response = await apiFetch(`/api/admin/audio-configuration/${providerId}/test`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          const error = await response.json();
          throw new Error(error.error || 'Failed to test audio provider');
        }

        return response.json();
      } catch (error: unknown) {
        reportError(error, 'Failed to test audio provider');
        throw new Error(error instanceof Error ? error.message : 'Failed to test audio provider');
      }
    },
  });
}

const providerTypeInfo = {
  openai: { label: 'OpenAI', color: 'blue', icon: IconCloud },
  elevenlabs: { label: 'ElevenLabs', color: 'purple', icon: IconVolume },
  azure: { label: 'Azure', color: 'cyan', icon: IconCloud },
  google: { label: 'Google Cloud', color: 'red', icon: IconCloud },
  aws: { label: 'AWS', color: 'orange', icon: IconCloud },
};

export default function AudioProvidersPage() {
  const { data: providers = [], isLoading: providersLoading, error: providersError } = useAudioProviders();
  const createProvider = useCreateAudioProvider();
  const updateProvider = useUpdateAudioProvider();
  const deleteProvider = useDeleteAudioProvider();
  const testProvider = useTestAudioProvider();
  
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);
  const [deleteModalOpened, { open: openDeleteModal, close: closeDeleteModal }] = useDisclosure(false);
  const [editingProvider, setEditingProvider] = useState<AudioProvider | null>(null);
  const [providerToDelete, setProviderToDelete] = useState<AudioProvider | null>(null);

  // Form state
  const [formData, setFormData] = useState({
    name: '',
    type: 'openai' as AudioProviderType,
    apiKey: '',
    endpoint: '',
    region: '',
    transcription: true,
    textToSpeech: true,
    translation: false,
    realtime: false,
    advancedConfig: '{}',
  });

  const handleAddProvider = () => {
    setEditingProvider(null);
    setFormData({
      name: '',
      type: 'openai',
      apiKey: '',
      endpoint: '',
      region: '',
      transcription: true,
      textToSpeech: true,
      translation: false,
      realtime: false,
      advancedConfig: '{}',
    });
    openModal();
  };

  const handleEditProvider = (provider: AudioProvider) => {
    setEditingProvider(provider);
    setFormData({
      name: provider.name,
      type: provider.type,
      apiKey: '', // Don't populate actual API key for security
      endpoint: provider.config.endpoint || '',
      region: provider.config.region || '',
      transcription: provider.capabilities.transcription,
      textToSpeech: provider.capabilities.textToSpeech,
      translation: provider.capabilities.translation,
      realtime: provider.capabilities.realtime,
      advancedConfig: JSON.stringify(provider.config.voiceSettings || {}, null, 2),
    });
    openModal();
  };

  const handleDeleteProvider = (provider: AudioProvider) => {
    setProviderToDelete(provider);
    openDeleteModal();
  };

  const confirmDelete = async () => {
    if (providerToDelete) {
      try {
        await deleteProvider.mutateAsync(providerToDelete.id);
        notifications.show({
          title: 'Provider Deleted',
          message: `${providerToDelete.name} has been removed`,
          color: 'green',
        });
        closeDeleteModal();
      } catch (error: unknown) {
        notifications.show({
          title: 'Delete Failed',
          message: error instanceof Error ? error.message : 'Failed to delete provider',
          color: 'red',
        });
      }
    }
  };

  const handleSaveProvider = async () => {
    try {
      const data = {
        name: formData.name,
        type: formData.type,
        apiKey: formData.apiKey,
        endpoint: formData.endpoint,
        region: formData.region,
        capabilities: {
          transcription: formData.transcription,
          textToSpeech: formData.textToSpeech,
          translation: formData.translation,
          realtime: formData.realtime,
        },
        advancedConfig: formData.advancedConfig,
      };
      
      if (editingProvider) {
        await updateProvider.mutateAsync({ ...data, id: editingProvider.id });
        notifications.show({
          title: 'Provider Updated',
          message: `${formData.name} has been updated successfully`,
          color: 'green',
        });
      } else {
        await createProvider.mutateAsync(data);
        notifications.show({
          title: 'Provider Added',
          message: `${formData.name} has been added successfully`,
          color: 'green',
        });
      }
      
      closeModal();
    } catch (error: unknown) {
      notifications.show({
        title: 'Save Failed',
        message: error instanceof Error ? error.message : 'Failed to save provider',
        color: 'red',
      });
    }
  };

  const handleTestProvider = async (provider: AudioProvider) => {
    try {
      notifications.show({
        title: 'Testing Provider',
        message: `Testing connection to ${provider.name}...`,
        color: 'blue',
        loading: true,
      });

      await testProvider.mutateAsync(provider.id);
      
      notifications.update({
        id: 'test-notification',
        title: 'Test Successful',
        message: `${provider.name} is responding correctly`,
        color: 'green',
        loading: false,
      });
    } catch (error: unknown) {
      notifications.update({
        id: 'test-notification',
        title: 'Test Failed',
        message: error instanceof Error ? error.message : 'Test failed',
        color: 'red',
        loading: false,
      });
    }
  };

  const _handleToggleProvider = async (_provider: AudioProvider) => {
    // Note: This would require an enable/disable endpoint in the SDK
    notifications.show({
      title: 'Feature Not Available',
      message: 'Provider enable/disable functionality needs to be implemented in the backend',
      color: 'yellow',
    });
  };

  const handleRefreshStats = () => {
    notifications.show({
      title: 'Refreshing Statistics',
      message: 'Updating provider statistics...',
      color: 'blue',
    });
  };

  if (providersError) {
    return (
      <Stack gap="md">
        <Group justify="space-between">
          <div>
            <Title order={1}>Audio Providers</Title>
            <Text c="dimmed">Configure audio service providers for transcription and text-to-speech</Text>
          </div>
        </Group>
        <Alert variant="light" color="red" icon={<IconAlertCircle size={16} />}>
          Failed to load audio providers: {(providersError as Error).message}
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Audio Providers</Title>
          <Text c="dimmed">Configure audio service providers for transcription and text-to-speech</Text>
        </div>
        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefreshStats}
          >
            Refresh Stats
          </Button>
          <Button
            leftSection={<IconPlus size={16} />}
            onClick={handleAddProvider}
          >
            Add Provider
          </Button>
        </Group>
      </Group>

      {/* Provider List */}
      <Card withBorder>
        <LoadingOverlay visible={providersLoading} />
        <ScrollArea>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Provider</Table.Th>
                <Table.Th>Type</Table.Th>
                <Table.Th>Capabilities</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Statistics</Table.Th>
                <Table.Th>Actions</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {providers.map((provider) => {
                const typeInfo = providerTypeInfo[provider.type];
                const Icon = typeInfo.icon;
                
                return (
                  <Table.Tr key={provider.id}>
                    <Table.Td>
                      <Group gap="xs">
                        <ThemeIcon size="sm" variant="light" color={typeInfo.color}>
                          <Icon size={16} />
                        </ThemeIcon>
                        <div>
                          <Text fw={500}>{provider.name}</Text>
                          <Text size="xs" c="dimmed">
                            Created {provider.createdAt.toLocaleDateString()}
                          </Text>
                        </div>
                      </Group>
                    </Table.Td>
                    <Table.Td>
                      <Badge variant="light" color={typeInfo.color}>
                        {typeInfo.label}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Group gap="xs">
                        {provider.capabilities.transcription && (
                          <Tooltip label="Transcription">
                            <ThemeIcon size="xs" variant="light" color="blue">
                              <IconMicrophone size={12} />
                            </ThemeIcon>
                          </Tooltip>
                        )}
                        {provider.capabilities.textToSpeech && (
                          <Tooltip label="Text-to-Speech">
                            <ThemeIcon size="xs" variant="light" color="green">
                              <IconVolume size={12} />
                            </ThemeIcon>
                          </Tooltip>
                        )}
                        {provider.capabilities.translation && (
                          <Tooltip label="Translation">
                            <ThemeIcon size="xs" variant="light" color="purple">
                              <IconLanguage size={12} />
                            </ThemeIcon>
                          </Tooltip>
                        )}
                        {provider.capabilities.realtime && (
                          <Badge size="xs" variant="light" color="orange">
                            Realtime
                          </Badge>
                        )}
                      </Group>
                    </Table.Td>
                    <Table.Td>
                      <Group gap="xs">
                        {provider.enabled ? (
                          <Badge color="green" variant="light">Active</Badge>
                        ) : (
                          <Badge color="gray" variant="light">Inactive</Badge>
                        )}
                      </Group>
                    </Table.Td>
                    <Table.Td>
                      <Stack gap={4}>
                        <Text size="xs">
                          {provider.statistics.requestsToday} requests today
                        </Text>
                        <Group gap="xs">
                          <Text size="xs" c="dimmed">
                            {provider.statistics.successRate}% success
                          </Text>
                          <Text size="xs" c="dimmed">
                            {provider.statistics.avgLatency}ms avg
                          </Text>
                        </Group>
                      </Stack>
                    </Table.Td>
                    <Table.Td>
                      <Group gap="xs">
                        <ActionIcon
                          variant="subtle"
                          size="sm"
                          onClick={() => handleTestProvider(provider)}
                          loading={testProvider.isPending}
                        >
                          <IconTestPipe size={16} />
                        </ActionIcon>
                        <ActionIcon
                          variant="subtle"
                          size="sm"
                          onClick={() => handleEditProvider(provider)}
                        >
                          <IconEdit size={16} />
                        </ActionIcon>
                        <ActionIcon
                          variant="subtle"
                          size="sm"
                          color="red"
                          onClick={() => handleDeleteProvider(provider)}
                        >
                          <IconTrash size={16} />
                        </ActionIcon>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                );
              })}
            </Table.Tbody>
          </Table>
        </ScrollArea>
      </Card>

      {/* Add/Edit Provider Modal */}
      <Modal
        opened={modalOpened}
        onClose={closeModal}
        title={editingProvider ? 'Edit Audio Provider' : 'Add Audio Provider'}
        size="lg"
      >
        <Stack gap="md">
          <TextInput
            label="Provider Name"
            placeholder="Enter provider name"
            value={formData.name}
            onChange={(e) => setFormData({ ...formData, name: e.currentTarget.value })}
            required
          />

          <Select
            label="Provider Type"
            data={Object.entries(providerTypeInfo).map(([value, info]) => ({
              value,
              label: info.label,
            }))}
            value={formData.type}
            onChange={(value) => setFormData({ ...formData, type: value as AudioProviderType })}
            required
          />

          <PasswordInput
            label="API Key"
            placeholder="Enter API key"
            value={formData.apiKey}
            onChange={(e) => setFormData({ ...formData, apiKey: e.currentTarget.value })}
            required
          />

          <TextInput
            label="API Endpoint"
            placeholder="https://api.example.com/v1"
            value={formData.endpoint}
            onChange={(e) => setFormData({ ...formData, endpoint: e.currentTarget.value })}
          />

          {(formData.type === 'azure' || formData.type === 'aws') && (
            <TextInput
              label="Region"
              placeholder="e.g., us-east-1"
              value={formData.region}
              onChange={(e) => setFormData({ ...formData, region: e.currentTarget.value })}
            />
          )}

          <div>
            <Text size="sm" fw={500} mb="xs">Capabilities</Text>
            <Stack gap="xs">
              <Switch
                label="Transcription"
                checked={formData.transcription}
                onChange={(e) => setFormData({ ...formData, transcription: e.currentTarget.checked })}
              />
              <Switch
                label="Text-to-Speech"
                checked={formData.textToSpeech}
                onChange={(e) => setFormData({ ...formData, textToSpeech: e.currentTarget.checked })}
              />
              <Switch
                label="Translation"
                checked={formData.translation}
                onChange={(e) => setFormData({ ...formData, translation: e.currentTarget.checked })}
              />
              <Switch
                label="Realtime Processing"
                checked={formData.realtime}
                onChange={(e) => setFormData({ ...formData, realtime: e.currentTarget.checked })}
              />
            </Stack>
          </div>

          <JsonInput
            label="Advanced Configuration"
            placeholder="Enter JSON configuration"
            value={formData.advancedConfig}
            onChange={(value) => setFormData({ ...formData, advancedConfig: value })}
            minRows={4}
            formatOnBlur
            validationError="Invalid JSON"
          />

          <Group justify="flex-end" mt="md">
            <Button variant="light" onClick={closeModal}>
              Cancel
            </Button>
            <Button 
              onClick={handleSaveProvider} 
              loading={createProvider.isPending || updateProvider.isPending}
            >
              {editingProvider ? 'Update Provider' : 'Add Provider'}
            </Button>
          </Group>
        </Stack>
      </Modal>

      {/* Delete Confirmation Modal */}
      <Modal
        opened={deleteModalOpened}
        onClose={closeDeleteModal}
        title="Delete Provider"
        size="sm"
      >
        <Stack gap="md">
          <Text>
            Are you sure you want to delete <strong>{providerToDelete?.name}</strong>? 
            This action cannot be undone.
          </Text>
          <Group justify="flex-end">
            <Button variant="light" onClick={closeDeleteModal}>
              Cancel
            </Button>
            <Button 
              color="red" 
              onClick={confirmDelete}
              loading={deleteProvider.isPending}
            >
              Delete Provider
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}