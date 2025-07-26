'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Group,
  Button,
  Badge,
  Table,
  ScrollArea,
  ActionIcon,
  Switch,
  TextInput,
  Select,
  NumberInput,
  SimpleGrid,
  ThemeIcon,
  Progress,
  Tooltip,
  Paper,
  LoadingOverlay,
  Alert,
  Tabs,
  Code,
  Grid,
} from '@mantine/core';
import {
  IconMicrophone,
  IconSettings,
  IconPlus,
  IconEdit,
  IconTrash,
  IconPlayerPlay,
  IconCircleCheck,
  IconAlertCircle,
  IconVolume,
  IconVolumeOff,
  IconRefresh,
  IconDownload,
  IconTestPipe,
  IconMicrophoneOff,
  IconBrandOpenai,
  IconBrain,
  IconApi,
  IconClock,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';

interface AudioProvider {
  id: string;
  name: string;
  type: 'openai' | 'azure' | 'elevenlabs' | 'google' | 'aws' | 'custom';
  enabled: boolean;
  models: AudioModel[];
  config: AudioProviderConfig;
  status: 'active' | 'inactive' | 'error';
  lastChecked?: string;
  totalRequests: number;
  totalMinutes: number;
  avgLatency: number;
  errorRate: number;
}

interface AudioModel {
  id: string;
  name: string;
  type: 'transcription' | 'tts' | 'both';
  languages: string[];
  maxDuration?: number;
  costPerMinute: number;
  enabled: boolean;
}

interface AudioProviderConfig {
  apiKey?: string;
  endpoint?: string;
  region?: string;
  voice?: string;
  sampleRate?: number;
  format?: string;
  maxConcurrency?: number;
  timeout?: number;
}

interface AudioStats {
  providerId: string;
  transcriptionRequests: number;
  ttsRequests: number;
  totalMinutes: number;
  avgProcessingTime: number;
  successRate: number;
  cost: number;
}

export default function AudioProvidersPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [providers, setProviders] = useState<AudioProvider[]>([]);
  const [stats, setStats] = useState<Record<string, AudioStats>>({});
  const [activeTab, setActiveTab] = useState<string | null>('providers');
  const [selectedProvider, setSelectedProvider] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);

  useEffect(() => {
    fetchProviders();
  }, []);

  const fetchProviders = async () => {
    try {
      // Fetch providers
      const providersResponse = await fetch('/api/audio-configuration');
      if (!providersResponse.ok) {
        throw new Error('Failed to fetch providers');
      }
      const providersData = await providersResponse.json();
      
      // Transform API response to match our interface
      const transformedProviders: AudioProvider[] = providersData.map((p: any) => ({
        id: p.id,
        name: p.name,
        type: p.providerType?.toLowerCase() || 'custom',
        enabled: p.isEnabled,
        models: [], // Will be populated separately if needed
        config: {
          apiKey: p.apiKey,
          endpoint: p.endpoint,
          region: p.region,
          ...(p.settings || {})
        },
        status: p.isEnabled ? 'active' : 'inactive',
        lastChecked: p.lastModified,
        totalRequests: 0,
        totalMinutes: 0,
        avgLatency: 0,
        errorRate: 0,
      }));
      
      setProviders(transformedProviders);
      
      // Fetch usage summary for stats (last 30 days)
      const endDate = new Date().toISOString();
      const startDate = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString();
      const summaryResponse = await fetch(`/api/audio-configuration/usage/summary?startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`);
      if (summaryResponse.ok) {
        const summaryData = await summaryResponse.json();
        // Transform summary data to stats format
        const statsMap: Record<string, AudioStats> = {};
        
        // summaryData has usageByProvider array
        if (summaryData.usageByProvider && Array.isArray(summaryData.usageByProvider)) {
          summaryData.usageByProvider.forEach((providerUsage: any) => {
            if (providerUsage.providerId) {
              // Calculate total minutes from duration in seconds
              const totalMinutes = (providerUsage.totalDurationSeconds || 0) / 60;
              
              // Calculate request counts by operation type
              let transcriptionRequests = 0;
              let ttsRequests = 0;
              
              if (providerUsage.usageByOperation && Array.isArray(providerUsage.usageByOperation)) {
                providerUsage.usageByOperation.forEach((op: any) => {
                  if (op.operationType === 'speech-to-text' || op.operationType === 'transcription') {
                    transcriptionRequests += op.requestCount || 0;
                  } else if (op.operationType === 'text-to-speech' || op.operationType === 'tts') {
                    ttsRequests += op.requestCount || 0;
                  }
                });
              }
              
              statsMap[providerUsage.providerId] = {
                providerId: providerUsage.providerId,
                transcriptionRequests,
                ttsRequests,
                totalMinutes,
                avgProcessingTime: 0, // Not available in the summary
                successRate: 100, // Assume 100% for now
                cost: providerUsage.totalCost || 0,
              };
            }
          });
        }
        
        setStats(statsMap);
      }
    } catch (error) {
      console.error('Error fetching audio providers:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load audio providers',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchProviders();
    setIsRefreshing(false);
    notifications.show({
      title: 'Refreshed',
      message: 'Audio provider data updated',
      color: 'green',
    });
  };

  const handleTestProvider = async (providerId: string) => {
    try {
      const response = await fetch(`/api/audio-configuration/${providerId}/test`, {
        method: 'POST',
      });

      if (!response.ok) {
        throw new Error('Test failed');
      }
      
      const result = await response.json();
      
      if (result.isSuccessful) {
        notifications.show({
          title: 'Test Successful',
          message: `Provider ${result.providerName} is working correctly`,
          color: 'green',
        });
      } else {
        notifications.show({
          title: 'Test Failed',
          message: result.errorMessage || 'Failed to connect to audio provider',
          color: 'red',
        });
      }
    } catch (error) {
      notifications.show({
        title: 'Test Failed',
        message: 'Failed to connect to audio provider',
        color: 'red',
      });
    }
  };

  const handleToggleProvider = async (providerId: string, enabled: boolean) => {
    try {
      const provider = providers.find(p => p.id === providerId);
      if (!provider) return;
      
      const response = await fetch(`/api/audio-configuration/${providerId}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          name: provider.name,
          providerType: provider.type.toUpperCase(),
          apiKey: provider.config.apiKey,
          endpoint: provider.config.endpoint,
          region: provider.config.region,
          isEnabled: enabled,
          settings: provider.config,
        }),
      });

      if (!response.ok) {
        throw new Error('Failed to update provider');
      }
      
      // Update local state
      setProviders(providers.map(p => 
        p.id === providerId ? { ...p, enabled, status: enabled ? 'active' : 'inactive' } : p
      ));
      
      notifications.show({
        title: 'Success',
        message: `Provider ${enabled ? 'enabled' : 'disabled'} successfully`,
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to update provider',
        color: 'red',
      });
    }
  };

  const getProviderIcon = (type: string) => {
    switch (type) {
      case 'openai': return <IconBrandOpenai size={20} />;
      case 'elevenlabs': return <IconVolume size={20} />;
      case 'azure': return <IconBrain size={20} />;
      case 'google': return <IconApi size={20} />;
      default: return <IconMicrophone size={20} />;
    }
  };

  const totalStats = {
    totalRequests: Object.values(stats).reduce((acc, s) => acc + s.transcriptionRequests + s.ttsRequests, 0),
    totalMinutes: Object.values(stats).reduce((acc, s) => acc + s.totalMinutes, 0),
    totalCost: Object.values(stats).reduce((acc, s) => acc + s.cost, 0),
    avgSuccessRate: Object.values(stats).reduce((acc, s) => acc + s.successRate, 0) / Object.values(stats).length || 0,
  };

  if (isLoading) {
    return (
      <Stack>
        <Card shadow="sm" p="md" radius="md" pos="relative" mih={200}>
          <LoadingOverlay visible={true} />
        </Card>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Card shadow="sm" p="md" radius="md">
        <Group justify="space-between" align="center">
          <div>
            <Title order={2}>Audio Providers</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Configure speech-to-text and text-to-speech providers
            </Text>
          </div>
          <Group>
            <Button
              variant="light"
              leftSection={<IconRefresh size={16} />}
              onClick={handleRefresh}
              loading={isRefreshing}
            >
              Refresh
            </Button>
            <Button
              variant="filled"
              leftSection={<IconPlus size={16} />}
              onClick={() => {
                setSelectedProvider(null);
                setIsEditing(true);
              }}
            >
              Add Provider
            </Button>
          </Group>
        </Group>
      </Card>

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Total Requests
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {totalStats.totalRequests.toLocaleString()}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Last 30 days
              </Text>
            </div>
            <ThemeIcon color="blue" variant="light" size={48} radius="md">
              <IconMicrophone size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Audio Processed
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {formatters.duration(totalStats.totalMinutes * 60000)}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                {totalStats.totalMinutes.toLocaleString()} minutes
              </Text>
            </div>
            <ThemeIcon color="green" variant="light" size={48} radius="md">
              <IconClock size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Total Cost
              </Text>
              <Text size="xl" fw={700} mt={4}>
                ${totalStats.totalCost.toFixed(2)}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                This month
              </Text>
            </div>
            <ThemeIcon color="orange" variant="light" size={48} radius="md">
              <IconPlayerPlay size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Success Rate
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {totalStats.avgSuccessRate.toFixed(1)}%
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Across all providers
              </Text>
            </div>
            <ThemeIcon color="teal" variant="light" size={48} radius="md">
              <IconCircleCheck size={24} />
            </ThemeIcon>
          </Group>
        </Card>
      </SimpleGrid>

      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="providers" leftSection={<IconMicrophone size={16} />}>
            Providers
          </Tabs.Tab>
          <Tabs.Tab value="models" leftSection={<IconBrain size={16} />}>
            Models
          </Tabs.Tab>
          <Tabs.Tab value="usage" leftSection={<IconPlayerPlay size={16} />}>
            Usage Statistics
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="providers" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Models</Table.Th>
                    <Table.Th>Requests</Table.Th>
                    <Table.Th>Cost</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Actions</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {providers.map((provider) => {
                    const providerStats = stats[provider.id];
                    return (
                      <Table.Tr key={provider.id}>
                        <Table.Td>
                          <Group gap="xs">
                            {getProviderIcon(provider.type)}
                            <div>
                              <Text fw={500}>{provider.name}</Text>
                              <Text size="xs" c="dimmed">
                                {provider.config.endpoint?.replace('https://', '')}
                              </Text>
                            </div>
                          </Group>
                        </Table.Td>
                        <Table.Td>
                          <Group gap="xs">
                            {provider.models.filter(m => m.enabled).length} / {provider.models.length} active
                          </Group>
                        </Table.Td>
                        <Table.Td>
                          {providerStats ? providerStats.transcriptionRequests + providerStats.ttsRequests : 0}
                        </Table.Td>
                        <Table.Td>
                          ${providerStats?.cost.toFixed(2) || '0.00'}
                        </Table.Td>
                        <Table.Td>
                          <Badge
                            leftSection={provider.status === 'active' ? <IconCircleCheck size={12} /> : <IconAlertCircle size={12} />}
                            color={provider.status === 'active' ? 'green' : provider.status === 'error' ? 'red' : 'gray'}
                            variant="light"
                          >
                            {provider.status}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Group gap="xs">
                            <Switch
                              checked={provider.enabled}
                              onChange={(e) => handleToggleProvider(provider.id, e.currentTarget.checked)}
                              size="sm"
                            />
                            <Tooltip label="Test connection">
                              <ActionIcon
                                variant="light"
                                onClick={() => handleTestProvider(provider.id)}
                                disabled={!provider.enabled}
                              >
                                <IconTestPipe size={16} />
                              </ActionIcon>
                            </Tooltip>
                            <Tooltip label="Configure">
                              <ActionIcon
                                variant="light"
                                onClick={() => {
                                  setSelectedProvider(provider.id);
                                  setIsEditing(true);
                                }}
                              >
                                <IconSettings size={16} />
                              </ActionIcon>
                            </Tooltip>
                          </Group>
                        </Table.Td>
                      </Table.Tr>
                    );
                  })}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="models" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Model</Table.Th>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Type</Table.Th>
                    <Table.Th>Languages</Table.Th>
                    <Table.Th>Cost/Min</Table.Th>
                    <Table.Th>Status</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {providers.flatMap(provider => 
                    provider.models.map(model => ({
                      ...model,
                      providerName: provider.name,
                      providerEnabled: provider.enabled,
                    }))
                  ).map((model) => (
                    <Table.Tr key={`${model.providerName}-${model.id}`}>
                      <Table.Td>
                        <Text fw={500}>{model.name}</Text>
                      </Table.Td>
                      <Table.Td>{model.providerName}</Table.Td>
                      <Table.Td>
                        <Badge
                          color={model.type === 'transcription' ? 'blue' : model.type === 'tts' ? 'green' : 'orange'}
                          variant="light"
                        >
                          {model.type === 'transcription' ? 'STT' : model.type === 'tts' ? 'TTS' : 'Both'}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{model.languages.slice(0, 5).join(', ')}{model.languages.length > 5 ? ` +${model.languages.length - 5}` : ''}</Text>
                      </Table.Td>
                      <Table.Td>
                        ${model.costPerMinute.toFixed(3)}
                      </Table.Td>
                      <Table.Td>
                        <Badge
                          color={model.enabled && model.providerEnabled ? 'green' : 'gray'}
                          variant="light"
                        >
                          {model.enabled && model.providerEnabled ? 'Active' : 'Inactive'}
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="usage" pt="md">
          <Grid>
            <Grid.Col span={{ base: 12, md: 6 }}>
              <Card shadow="sm" p="md" radius="md" withBorder>
                <Title order={4} mb="md">Usage by Provider</Title>
                <Stack gap="sm">
                  {providers.map((provider) => {
                    const providerStats = stats[provider.id];
                    if (!providerStats) return null;
                    
                    const percentage = (providerStats.totalMinutes / totalStats.totalMinutes) * 100;
                    
                    return (
                      <Paper key={provider.id} p="sm" withBorder>
                        <Group justify="space-between" mb="xs">
                          <Group gap="xs">
                            {getProviderIcon(provider.type)}
                            <Text fw={500}>{provider.name}</Text>
                          </Group>
                          <Text fw={600}>
                            {formatters.duration(providerStats.totalMinutes * 60000)}
                          </Text>
                        </Group>
                        <Progress
                          value={percentage}
                          color="blue"
                          size="sm"
                          radius="md"
                        />
                        <Group justify="space-between" mt="xs">
                          <Text size="xs" c="dimmed">
                            {providerStats.transcriptionRequests + providerStats.ttsRequests} requests
                          </Text>
                          <Text size="xs" c="dimmed">
                            ${providerStats.cost.toFixed(2)}
                          </Text>
                        </Group>
                      </Paper>
                    );
                  })}
                </Stack>
              </Card>
            </Grid.Col>

            <Grid.Col span={{ base: 12, md: 6 }}>
              <Card shadow="sm" p="md" radius="md" withBorder>
                <Title order={4} mb="md">Request Types</Title>
                <Stack gap="md">
                  <Paper p="md" withBorder>
                    <Group justify="space-between" mb="xs">
                      <Text fw={500}>Speech-to-Text</Text>
                      <Text fw={600}>
                        {Object.values(stats).reduce((acc, s) => acc + s.transcriptionRequests, 0).toLocaleString()}
                      </Text>
                    </Group>
                    <Progress
                      value={70}
                      color="blue"
                      size="lg"
                      radius="md"
                    />
                  </Paper>
                  <Paper p="md" withBorder>
                    <Group justify="space-between" mb="xs">
                      <Text fw={500}>Text-to-Speech</Text>
                      <Text fw={600}>
                        {Object.values(stats).reduce((acc, s) => acc + s.ttsRequests, 0).toLocaleString()}
                      </Text>
                    </Group>
                    <Progress
                      value={30}
                      color="green"
                      size="lg"
                      radius="md"
                    />
                  </Paper>
                </Stack>
              </Card>
            </Grid.Col>
          </Grid>
        </Tabs.Panel>
      </Tabs>

      <LoadingOverlay visible={isRefreshing} />
    </Stack>
  );
}