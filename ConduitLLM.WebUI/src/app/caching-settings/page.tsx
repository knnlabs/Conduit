'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Group,
  Button,
  Badge,
  ThemeIcon,
  Paper,
  SimpleGrid,
  Progress,
  Switch,
  NumberInput,
  Select,
  Table,
  ScrollArea,
  LoadingOverlay,
  Alert,
  ActionIcon,
  Tooltip,
  Code,
  Tabs,
} from '@mantine/core';
import {
  IconDatabase,
  IconRefresh,
  IconTrash,
  IconSettings,
  IconServer2,
  IconClock,
  IconActivity,
  IconAlertCircle,
  IconCircleCheck,
  IconChartBar,
  IconCpu,
  IconBolt,
  IconInfoCircle,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';

interface CacheConfig {
  id: string;
  name: string;
  type: 'redis' | 'memory' | 'distributed';
  enabled: boolean;
  ttl: number;
  maxSize: number;
  evictionPolicy: 'lru' | 'lfu' | 'ttl' | 'random';
  compression: boolean;
  persistent: boolean;
}

interface CacheStats {
  hits: number;
  misses: number;
  evictions: number;
  size: number;
  entries: number;
  hitRate: number;
  avgLatency: number;
}

interface CacheEntry {
  key: string;
  size: number;
  ttl: number;
  hits: number;
  lastAccessed: string;
  expires: string;
}

export default function CachingSettingsPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [activeTab, setActiveTab] = useState<string | null>('configuration');
  const [cacheConfigs, setCacheConfigs] = useState<CacheConfig[]>([]);
  const [cacheStats, setCacheStats] = useState<Record<string, CacheStats>>({});
  const [selectedCache, setSelectedCache] = useState<string>('provider-responses');

  useEffect(() => {
    fetchCacheData();
  }, []);

  const fetchCacheData = async () => {
    try {
      const response = await fetch('/api/config/caching', {
        headers: {
          'X-Admin-Auth-Key': localStorage.getItem('adminAuthKey') || '',
        },
      });

      if (!response.ok) {
        throw new Error('Failed to fetch cache configuration');
      }

      const data = await response.json();
      
      // Mock data for development
      const mockConfigs: CacheConfig[] = [
        {
          id: 'provider-responses',
          name: 'Provider Responses',
          type: 'redis',
          enabled: true,
          ttl: 3600,
          maxSize: 1024,
          evictionPolicy: 'lru',
          compression: true,
          persistent: true,
        },
        {
          id: 'embeddings',
          name: 'Embeddings Cache',
          type: 'redis',
          enabled: true,
          ttl: 86400,
          maxSize: 2048,
          evictionPolicy: 'lfu',
          compression: true,
          persistent: true,
        },
        {
          id: 'model-metadata',
          name: 'Model Metadata',
          type: 'memory',
          enabled: true,
          ttl: 600,
          maxSize: 256,
          evictionPolicy: 'ttl',
          compression: false,
          persistent: false,
        },
        {
          id: 'rate-limits',
          name: 'Rate Limit Counters',
          type: 'memory',
          enabled: true,
          ttl: 60,
          maxSize: 128,
          evictionPolicy: 'ttl',
          compression: false,
          persistent: false,
        },
        {
          id: 'auth-tokens',
          name: 'Auth Token Cache',
          type: 'distributed',
          enabled: true,
          ttl: 1800,
          maxSize: 512,
          evictionPolicy: 'ttl',
          compression: false,
          persistent: true,
        },
      ];

      const mockStats: Record<string, CacheStats> = {
        'provider-responses': {
          hits: 45678,
          misses: 12345,
          evictions: 890,
          size: 768,
          entries: 3456,
          hitRate: 78.7,
          avgLatency: 0.45,
        },
        'embeddings': {
          hits: 23456,
          misses: 5678,
          evictions: 234,
          size: 1536,
          entries: 1890,
          hitRate: 80.5,
          avgLatency: 0.38,
        },
        'model-metadata': {
          hits: 98765,
          misses: 8765,
          evictions: 1234,
          size: 128,
          entries: 234,
          hitRate: 91.8,
          avgLatency: 0.12,
        },
        'rate-limits': {
          hits: 234567,
          misses: 12345,
          evictions: 4567,
          size: 64,
          entries: 890,
          hitRate: 95.0,
          avgLatency: 0.08,
        },
        'auth-tokens': {
          hits: 56789,
          misses: 6789,
          evictions: 456,
          size: 256,
          entries: 678,
          hitRate: 89.3,
          avgLatency: 0.22,
        },
      };

      setCacheConfigs(data.configs || mockConfigs);
      setCacheStats(data.stats || mockStats);
    } catch (error) {
      console.error('Error fetching cache data:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load cache configuration',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchCacheData();
    setIsRefreshing(false);
    notifications.show({
      title: 'Refreshed',
      message: 'Cache data updated',
      color: 'green',
    });
  };

  const handleClearCache = async (cacheId: string) => {
    try {
      const response = await fetch(`/api/config/caching/${cacheId}/clear`, {
        method: 'POST',
        headers: {
          'X-Admin-Auth-Key': localStorage.getItem('adminAuthKey') || '',
        },
      });

      if (!response.ok) {
        throw new Error('Failed to clear cache');
      }

      notifications.show({
        title: 'Cache Cleared',
        message: `${cacheId} cache has been cleared`,
        color: 'green',
      });

      await fetchCacheData();
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to clear cache',
        color: 'red',
      });
    }
  };

  const handleConfigUpdate = async (cacheId: string, updates: Partial<CacheConfig>) => {
    try {
      const response = await fetch('/api/config/caching', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          'X-Admin-Auth-Key': localStorage.getItem('adminAuthKey') || '',
        },
        body: JSON.stringify({ cacheId, updates }),
      });

      if (!response.ok) {
        throw new Error('Failed to update cache configuration');
      }

      notifications.show({
        title: 'Configuration Updated',
        message: 'Cache settings have been updated',
        color: 'green',
      });

      await fetchCacheData();
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to update cache configuration',
        color: 'red',
      });
    }
  };

  // Mock cache entries for detail view
  const mockCacheEntries: CacheEntry[] = [
    {
      key: 'openai:gpt-4:chat:abc123',
      size: 2048,
      ttl: 3542,
      hits: 234,
      lastAccessed: '2024-01-10T12:30:00Z',
      expires: '2024-01-10T13:30:00Z',
    },
    {
      key: 'anthropic:claude-3:complete:def456',
      size: 1536,
      ttl: 2890,
      hits: 189,
      lastAccessed: '2024-01-10T12:28:00Z',
      expires: '2024-01-10T13:15:00Z',
    },
    {
      key: 'embeddings:text-embedding-ada-002:xyz789',
      size: 4096,
      ttl: 85234,
      hits: 567,
      lastAccessed: '2024-01-10T12:25:00Z',
      expires: '2024-01-11T12:00:00Z',
    },
  ];

  const getTypeColor = (type: string) => {
    switch (type) {
      case 'redis': return 'red';
      case 'memory': return 'blue';
      case 'distributed': return 'green';
      default: return 'gray';
    }
  };

  const getEvictionPolicyLabel = (policy: string) => {
    switch (policy) {
      case 'lru': return 'Least Recently Used';
      case 'lfu': return 'Least Frequently Used';
      case 'ttl': return 'Time To Live';
      case 'random': return 'Random';
      default: return policy;
    }
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

  const selectedConfig = cacheConfigs.find(c => c.id === selectedCache);
  const selectedStats = selectedCache ? cacheStats[selectedCache] : null;

  return (
    <Stack gap="xl">
      <Card shadow="sm" p="md" radius="md">
        <Group justify="space-between" align="center">
          <div>
            <Title order={2}>Caching Settings</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Configure and monitor caching behavior
            </Text>
          </div>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
            loading={isRefreshing}
          >
            Refresh
          </Button>
        </Group>
      </Card>

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Total Hit Rate
              </Text>
              <Text size="xl" fw={700} mt={4}>
                85.4%
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Across all caches
              </Text>
            </div>
            <ThemeIcon color="green" variant="light" size={48} radius="md">
              <IconChartBar size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Memory Usage
              </Text>
              <Text size="xl" fw={700} mt={4}>
                2.8GB
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Of 4GB allocated
              </Text>
            </div>
            <ThemeIcon color="blue" variant="light" size={48} radius="md">
              <IconCpu size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Avg Latency
              </Text>
              <Text size="xl" fw={700} mt={4}>
                0.28ms
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Cache response time
              </Text>
            </div>
            <ThemeIcon color="teal" variant="light" size={48} radius="md">
              <IconBolt size={24} />
            </ThemeIcon>
          </Group>
        </Card>

        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                Active Caches
              </Text>
              <Text size="xl" fw={700} mt={4}>
                {cacheConfigs.filter(c => c.enabled).length} / {cacheConfigs.length}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Caches enabled
              </Text>
            </div>
            <ThemeIcon color="orange" variant="light" size={48} radius="md">
              <IconDatabase size={24} />
            </ThemeIcon>
          </Group>
        </Card>
      </SimpleGrid>

      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="configuration" leftSection={<IconSettings size={16} />}>
            Configuration
          </Tabs.Tab>
          <Tabs.Tab value="statistics" leftSection={<IconActivity size={16} />}>
            Statistics
          </Tabs.Tab>
          <Tabs.Tab value="details" leftSection={<IconDatabase size={16} />}>
            Cache Details
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="configuration" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">Cache Configuration</Title>
            <Stack gap="md">
              {cacheConfigs.map((config) => {
                const stats = cacheStats[config.id];
                return (
                  <Paper key={config.id} p="md" withBorder>
                    <Group justify="space-between" mb="md">
                      <Group>
                        <Text fw={600}>{config.name}</Text>
                        <Badge color={getTypeColor(config.type)} variant="light">
                          {config.type}
                        </Badge>
                        <Badge
                          color={config.enabled ? 'green' : 'gray'}
                          variant="light"
                          leftSection={config.enabled ? <IconCircleCheck size={12} /> : null}
                        >
                          {config.enabled ? 'Enabled' : 'Disabled'}
                        </Badge>
                      </Group>
                      <Group gap="xs">
                        <Tooltip label="Clear all entries">
                          <ActionIcon
                            variant="light"
                            color="red"
                            onClick={() => handleClearCache(config.id)}
                          >
                            <IconTrash size={16} />
                          </ActionIcon>
                        </Tooltip>
                      </Group>
                    </Group>

                    <SimpleGrid cols={{ base: 1, sm: 2, md: 3 }} spacing="sm">
                      <div>
                        <Text size="xs" c="dimmed">TTL (seconds)</Text>
                        <NumberInput
                          value={config.ttl}
                          onChange={(value) => handleConfigUpdate(config.id, { ttl: Number(value) })}
                          min={0}
                          size="xs"
                          mt={4}
                        />
                      </div>
                      <div>
                        <Text size="xs" c="dimmed">Max Size (MB)</Text>
                        <NumberInput
                          value={config.maxSize}
                          onChange={(value) => handleConfigUpdate(config.id, { maxSize: Number(value) })}
                          min={0}
                          size="xs"
                          mt={4}
                        />
                      </div>
                      <div>
                        <Text size="xs" c="dimmed">Eviction Policy</Text>
                        <Select
                          value={config.evictionPolicy}
                          onChange={(value) => handleConfigUpdate(config.id, { evictionPolicy: value as any })}
                          data={[
                            { value: 'lru', label: 'LRU' },
                            { value: 'lfu', label: 'LFU' },
                            { value: 'ttl', label: 'TTL' },
                            { value: 'random', label: 'Random' },
                          ]}
                          size="xs"
                          mt={4}
                        />
                      </div>
                    </SimpleGrid>

                    <Group gap="lg" mt="md">
                      <Switch
                        label="Enabled"
                        checked={config.enabled}
                        onChange={(e) => handleConfigUpdate(config.id, { enabled: e.currentTarget.checked })}
                        size="sm"
                      />
                      <Switch
                        label="Compression"
                        checked={config.compression}
                        onChange={(e) => handleConfigUpdate(config.id, { compression: e.currentTarget.checked })}
                        size="sm"
                      />
                      <Switch
                        label="Persistent"
                        checked={config.persistent}
                        onChange={(e) => handleConfigUpdate(config.id, { persistent: e.currentTarget.checked })}
                        size="sm"
                      />
                    </Group>

                    {stats && (
                      <Group gap="xs" mt="md">
                        <Text size="xs" c="dimmed">
                          Size: {stats.size}MB / {config.maxSize}MB
                        </Text>
                        <Text size="xs" c="dimmed">•</Text>
                        <Text size="xs" c="dimmed">
                          Entries: {stats.entries.toLocaleString()}
                        </Text>
                        <Text size="xs" c="dimmed">•</Text>
                        <Text size="xs" c="dimmed">
                          Hit Rate: {stats.hitRate}%
                        </Text>
                      </Group>
                    )}
                  </Paper>
                );
              })}
            </Stack>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="statistics" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Title order={4} mb="md">Cache Performance Statistics</Title>
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Cache</Table.Th>
                    <Table.Th>Hits</Table.Th>
                    <Table.Th>Misses</Table.Th>
                    <Table.Th>Hit Rate</Table.Th>
                    <Table.Th>Evictions</Table.Th>
                    <Table.Th>Size</Table.Th>
                    <Table.Th>Avg Latency</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {cacheConfigs.map((config) => {
                    const stats = cacheStats[config.id];
                    if (!stats) return null;
                    
                    return (
                      <Table.Tr key={config.id}>
                        <Table.Td>
                          <Group gap="xs">
                            <Text fw={500}>{config.name}</Text>
                            <Badge size="xs" color={getTypeColor(config.type)} variant="light">
                              {config.type}
                            </Badge>
                          </Group>
                        </Table.Td>
                        <Table.Td>{stats.hits.toLocaleString()}</Table.Td>
                        <Table.Td>{stats.misses.toLocaleString()}</Table.Td>
                        <Table.Td>
                          <Badge
                            color={stats.hitRate > 80 ? 'green' : stats.hitRate > 60 ? 'yellow' : 'red'}
                            variant="light"
                          >
                            {stats.hitRate}%
                          </Badge>
                        </Table.Td>
                        <Table.Td>{stats.evictions.toLocaleString()}</Table.Td>
                        <Table.Td>
                          <Group gap={4}>
                            <Text size="sm">{stats.size}MB</Text>
                            <Progress
                              value={(stats.size / config.maxSize) * 100}
                              size="xs"
                              w={50}
                              color={stats.size / config.maxSize > 0.9 ? 'red' : 'blue'}
                            />
                          </Group>
                        </Table.Td>
                        <Table.Td>
                          <Badge
                            color={stats.avgLatency < 0.5 ? 'green' : stats.avgLatency < 1 ? 'yellow' : 'red'}
                            variant="light"
                          >
                            {stats.avgLatency}ms
                          </Badge>
                        </Table.Td>
                      </Table.Tr>
                    );
                  })}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="details" pt="md">
          <Card shadow="sm" p="md" radius="md" withBorder>
            <Group justify="space-between" mb="md">
              <Title order={4}>Cache Entries</Title>
              <Select
                value={selectedCache}
                onChange={(value) => setSelectedCache(value || 'provider-responses')}
                data={cacheConfigs.map(c => ({ value: c.id, label: c.name }))}
                w={250}
              />
            </Group>
            
            {selectedConfig && selectedStats && (
              <Stack gap="md">
                <Alert
                  icon={<IconInfoCircle size={16} />}
                  title="Cache Information"
                  color="blue"
                >
                  <Text size="sm">
                    Type: <Code>{selectedConfig.type}</Code> • 
                    Policy: <Code>{getEvictionPolicyLabel(selectedConfig.evictionPolicy)}</Code> • 
                    Entries: <Code>{selectedStats.entries}</Code> • 
                    Size: <Code>{selectedStats.size}MB / {selectedConfig.maxSize}MB</Code>
                  </Text>
                </Alert>

                <ScrollArea>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Key</Table.Th>
                        <Table.Th>Size</Table.Th>
                        <Table.Th>TTL</Table.Th>
                        <Table.Th>Hits</Table.Th>
                        <Table.Th>Last Accessed</Table.Th>
                        <Table.Th>Expires</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {mockCacheEntries.map((entry) => (
                        <Table.Tr key={entry.key}>
                          <Table.Td>
                            <Code>{entry.key}</Code>
                          </Table.Td>
                          <Table.Td>{formatters.fileSize(entry.size)}</Table.Td>
                          <Table.Td>{formatters.duration(entry.ttl * 1000)}</Table.Td>
                          <Table.Td>{entry.hits}</Table.Td>
                          <Table.Td>
                            <Text size="xs">{formatters.date(entry.lastAccessed, { relativeDays: 7 })}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Text size="xs">{formatters.date(entry.expires, { relativeDays: 7 })}</Text>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </ScrollArea>
              </Stack>
            )}
          </Card>
        </Tabs.Panel>
      </Tabs>

      <LoadingOverlay visible={isRefreshing} />
    </Stack>
  );
}