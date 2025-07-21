'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  SimpleGrid,
  LoadingOverlay,
  Alert,
  Select,
  TextInput,
  Badge,
  Tabs,
  JsonInput,
  ScrollArea,
  ActionIcon,
  Tooltip,
  Paper,
  Divider,
  Code,
  Box,
} from '@mantine/core';
import {
  IconKey,
  IconEye,
  IconAlertCircle,
  IconSearch,
  IconRefresh,
  IconCode,
  IconFilter,
  IconApi,
  IconCopy,
  IconCheck,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import type { VirtualKeyDto } from '@knn_labs/conduit-admin-client';
import { useClipboard } from '@mantine/hooks';

interface DiscoveredModel {
  id: string;
  provider?: string;
  displayName: string;
  capabilities: Record<string, any>;
}

interface DiscoveryPreviewResponse {
  data: DiscoveredModel[];
  count: number;
}

const CAPABILITY_FILTERS = [
  { value: '', label: 'All capabilities' },
  { value: 'chat', label: 'Chat' },
  { value: 'vision', label: 'Vision' },
  { value: 'audio_transcription', label: 'Audio Transcription' },
  { value: 'text_to_speech', label: 'Text to Speech' },
  { value: 'realtime_audio', label: 'Realtime Audio' },
  { value: 'video_generation', label: 'Video Generation' },
  { value: 'image_generation', label: 'Image Generation' },
  { value: 'embeddings', label: 'Embeddings' },
  { value: 'function_calling', label: 'Function Calling' },
  { value: 'tool_use', label: 'Tool Use' },
  { value: 'json_mode', label: 'JSON Mode' },
];

export default function VirtualKeyDiscoveryPreviewPage() {
  const [virtualKeys, setVirtualKeys] = useState<VirtualKeyDto[]>([]);
  const [selectedKeyId, setSelectedKeyId] = useState<string>('');
  const [selectedCapability, setSelectedCapability] = useState<string>('');
  const [discoveryData, setDiscoveryData] = useState<DiscoveryPreviewResponse | null>(null);
  const [isLoadingKeys, setIsLoadingKeys] = useState(true);
  const [isLoadingDiscovery, setIsLoadingDiscovery] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [activeTab, setActiveTab] = useState<string | null>('formatted');
  const clipboard = useClipboard({ timeout: 2000 });

  // Fetch virtual keys on mount
  useEffect(() => {
    void fetchVirtualKeys();
  }, []);

  const fetchVirtualKeys = async () => {
    try {
      setIsLoadingKeys(true);
      setError(null);
      
      const response = await fetch('/api/virtualkeys');
      
      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to fetch virtual keys: ${errorText}`);
      }
      
      const result = await response.json() as { items?: VirtualKeyDto[] } | VirtualKeyDto[];
      const data = Array.isArray(result) ? result : (result.items ?? []);
      
      setVirtualKeys(data);
    } catch (err) {
      console.error('Error fetching virtual keys:', err);
      setError(err as Error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load virtual keys',
        color: 'red',
      });
    } finally {
      setIsLoadingKeys(false);
    }
  };

  const fetchDiscoveryPreview = useCallback(async () => {
    if (!selectedKeyId) {
      notifications.show({
        title: 'No key selected',
        message: 'Please select a virtual key first',
        color: 'yellow',
      });
      return;
    }

    try {
      setIsLoadingDiscovery(true);
      setError(null);
      
      const params = new URLSearchParams();
      if (selectedCapability) {
        params.append('capability', selectedCapability);
      }
      
      const url = `/api/virtualkeys/${selectedKeyId}/discovery-preview${params.toString() ? `?${params.toString()}` : ''}`;
      const response = await fetch(url);
      
      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to fetch discovery preview: ${errorText}`);
      }
      
      const data = await response.json() as DiscoveryPreviewResponse;
      setDiscoveryData(data);
    } catch (err) {
      console.error('Error fetching discovery preview:', err);
      setError(err as Error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load discovery preview',
        color: 'red',
      });
    } finally {
      setIsLoadingDiscovery(false);
    }
  }, [selectedKeyId, selectedCapability]);

  const handleKeyChange = (value: string | null) => {
    setSelectedKeyId(value ?? '');
    setDiscoveryData(null); // Clear previous data
  };

  const handleCapabilityChange = (value: string | null) => {
    setSelectedCapability(value ?? '');
  };

  const selectedKey = virtualKeys.find(k => k.id?.toString() === selectedKeyId);

  const copyJson = () => {
    if (discoveryData) {
      clipboard.copy(JSON.stringify(discoveryData, null, 2));
      notifications.show({
        title: 'Copied',
        message: 'JSON response copied to clipboard',
        color: 'green',
      });
    }
  };

  const renderCapability = (name: string, capability: any) => {
    if (typeof capability !== 'object' || !capability) return null;
    
    const isSupported = capability.supported === true;
    if (!isSupported) return null;

    return (
      <Box key={name}>
        <Group gap="xs" mb="xs">
          <Badge color="blue" variant="light" size="sm">
            {name.replace(/_/g, ' ')}
          </Badge>
          {capability.supported_languages && (
            <Text size="xs" c="dimmed">
              {capability.supported_languages.length} languages
            </Text>
          )}
          {capability.supported_voices && (
            <Text size="xs" c="dimmed">
              {capability.supported_voices.length} voices
            </Text>
          )}
          {capability.supported_formats && (
            <Text size="xs" c="dimmed">
              {capability.supported_formats.length} formats
            </Text>
          )}
        </Group>
      </Box>
    );
  };

  return (
    <Stack>
      <Group justify="space-between" align="flex-start">
        <div>
          <Title order={2}>Virtual Key Discovery Preview</Title>
          <Text c="dimmed" size="sm" mt="xs">
            Preview what models and capabilities a virtual key would see when calling the discovery endpoint
          </Text>
        </div>
      </Group>

      {error && (
        <Alert icon={<IconAlertCircle size="1rem" />} color="red" variant="light">
          {error.message}
        </Alert>
      )}

      <Card shadow="sm" p="lg" radius="md" withBorder>
        <LoadingOverlay visible={isLoadingKeys} />
        
        <Stack gap="md">
          <Group align="flex-end">
            <Select
              label="Select Virtual Key"
              placeholder="Choose a virtual key"
              data={virtualKeys.map(key => ({
                value: key.id?.toString() ?? '',
                label: `${key.keyName} ${key.isActive ? '' : '(Inactive)'}`,
                disabled: !key.isActive,
              }))}
              value={selectedKeyId}
              onChange={handleKeyChange}
              searchable
              nothingFoundMessage="No virtual keys found"
              leftSection={<IconKey size="1rem" />}
              style={{ flex: 1 }}
            />
            
            <Select
              label="Filter by Capability"
              placeholder="All capabilities"
              data={CAPABILITY_FILTERS}
              value={selectedCapability}
              onChange={handleCapabilityChange}
              clearable
              leftSection={<IconFilter size="1rem" />}
              style={{ flex: 1 }}
            />
            
            <Button
              onClick={() => void fetchDiscoveryPreview()}
              leftSection={<IconEye size="1rem" />}
              disabled={!selectedKeyId}
              loading={isLoadingDiscovery}
            >
              Preview Discovery
            </Button>
          </Group>

          {selectedKey && (
            <Paper p="md" radius="md" withBorder>
              <Group justify="space-between">
                <div>
                  <Text size="sm" fw={500}>Selected Key Details</Text>
                  <Text size="xs" c="dimmed" mt="xs">
                    Name: {selectedKey.keyName}
                  </Text>
                  {selectedKey.allowedModels && (
                    <Text size="xs" c="dimmed">
                      Allowed Models: {selectedKey.allowedModels}
                    </Text>
                  )}
                </div>
                <Badge color={selectedKey.isActive ? 'green' : 'red'}>
                  {selectedKey.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </Group>
            </Paper>
          )}
        </Stack>
      </Card>

      {discoveryData && (
        <Card shadow="sm" p="lg" radius="md" withBorder>
          <Stack gap="md">
            <Group justify="space-between">
              <Group>
                <Title order={4}>Discovery Results</Title>
                <Badge size="lg" variant="filled">
                  {discoveryData.count} models
                </Badge>
              </Group>
              
              <Tooltip label={clipboard.copied ? 'Copied!' : 'Copy JSON'}>
                <ActionIcon
                  variant="light"
                  onClick={copyJson}
                  color={clipboard.copied ? 'green' : 'blue'}
                >
                  {clipboard.copied ? <IconCheck size="1rem" /> : <IconCopy size="1rem" />}
                </ActionIcon>
              </Tooltip>
            </Group>

            <Tabs value={activeTab} onChange={setActiveTab}>
              <Tabs.List>
                <Tabs.Tab value="formatted" leftSection={<IconApi size="1rem" />}>
                  Formatted View
                </Tabs.Tab>
                <Tabs.Tab value="json" leftSection={<IconCode size="1rem" />}>
                  Raw JSON
                </Tabs.Tab>
              </Tabs.List>

              <Tabs.Panel value="formatted" pt="md">
                <SimpleGrid cols={{ base: 1, md: 2, lg: 3 }} spacing="md">
                  {discoveryData.data.map((model) => (
                    <Card key={model.id} shadow="sm" p="md" radius="md" withBorder>
                      <Stack gap="sm">
                        <div>
                          <Text fw={500} size="sm">{model.displayName}</Text>
                          <Group gap="xs" mt="xs">
                            <Badge size="xs" variant="dot">
                              {model.id}
                            </Badge>
                            {model.provider && (
                              <Badge size="xs" color="gray" variant="light">
                                {model.provider}
                              </Badge>
                            )}
                          </Group>
                        </div>
                        
                        <Divider />
                        
                        <div>
                          <Text size="xs" fw={500} mb="xs">Capabilities:</Text>
                          <Stack gap="xs">
                            {Object.entries(model.capabilities).map(([name, capability]) => 
                              renderCapability(name, capability)
                            )}
                          </Stack>
                        </div>
                      </Stack>
                    </Card>
                  ))}
                </SimpleGrid>
              </Tabs.Panel>

              <Tabs.Panel value="json" pt="md">
                <ScrollArea h={500}>
                  <JsonInput
                    value={JSON.stringify(discoveryData, null, 2)}
                    readOnly
                    autosize
                    minRows={10}
                    maxRows={30}
                  />
                </ScrollArea>
              </Tabs.Panel>
            </Tabs>
          </Stack>
        </Card>
      )}
    </Stack>
  );
}