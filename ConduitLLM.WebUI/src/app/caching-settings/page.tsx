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
  size: string; // Changed to string as API returns formatted size
  createdAt: string;
  lastAccessedAt: string;
  expiresAt: string;
  accessCount: number;
  priority: number;
}

interface CacheEntriesResponse {
  regionId: string;
  entries: CacheEntry[];
  totalCount: number;
  skip: number;
  take: number;
  message?: string;
}

export default function CachingSettingsPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [activeTab, setActiveTab] = useState<string | null>('configuration');
  const [cacheConfigs, setCacheConfigs] = useState<CacheConfig[]>([]);
  const [cacheStats, setCacheStats] = useState<Record<string, CacheStats>>({});
  const [selectedCache, setSelectedCache] = useState<string>('');

  useEffect(() => {
    fetchCacheData();
  }, []);

  useEffect(() => {
    if (cacheConfigs.length > 0 && !selectedCache) {
      setSelectedCache(cacheConfigs[0].id);
    }
  }, [cacheConfigs, selectedCache]);

  useEffect(() => {
    if (selectedCache) {
      fetchCacheEntries(selectedCache);
    }
  }, [selectedCache]);

  const fetchCacheData = async () => {
    try {
      const response = await fetch('/api/config/caching');

      if (!response.ok) {
        throw new Error('Failed to fetch cache configuration');
      }

      const data = await response.json();
      
      setCacheConfigs(data.configs || []);
      setCacheStats(data.stats || {});
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

  const fetchCacheEntries = async (cacheId: string) => {
    setIsLoadingEntries(true);
    try {
      const response = await fetch(`/api/config/caching/${cacheId}/entries`);
      
      if (!response.ok) {
        throw new Error('Failed to fetch cache entries');
      }
      
      const data: CacheEntriesResponse = await response.json();
      setCacheEntries(data.entries || []);
      
      // Show message if access is restricted
      if (data.message) {
        notifications.show({
          title: 'Information',
          message: data.message,
          color: 'blue',
        });
      }
    } catch (error) {
      console.error('Error fetching cache entries:', error);
      setCacheEntries([]);
    } finally {
      setIsLoadingEntries(false);
    }
  };

  const handleClearCache = async (cacheId: string) => {
    try {
      const response = await fetch(`/api/config/caching/${cacheId}/clear`, {
        method: 'POST',
      });

      if (!response.ok) {
        throw new Error('Failed to clear cache');
      }

      const data = await response.json();
      
      notifications.show({
        title: 'Cache Cleared',
        message: data.message || `${cacheId} cache has been cleared`,
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

  const [cacheEntries, setCacheEntries] = useState<CacheEntry[]>([]);
  const [isLoadingEntries, setIsLoadingEntries] = useState(false);

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
                {cacheStats && Object.keys(cacheStats).length > 0
                  ? (() => {
                      const totalHits = Object.values(cacheStats).reduce((sum, stat) => sum + stat.hits, 0);
                      const totalMisses = Object.values(cacheStats).reduce((sum, stat) => sum + stat.misses, 0);
                      const totalRate = totalHits + totalMisses > 0
                        ? ((totalHits / (totalHits + totalMisses)) * 100).toFixed(1)
                        : '0.0';
                      return `${totalRate}%`;
                    })()
                  : '0.0%'}
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
                {cacheStats && Object.keys(cacheStats).length > 0
                  ? (() => {
                      const totalSize = Object.values(cacheStats).reduce((sum, stat) => sum + stat.size, 0);
                      return totalSize >= 1024 
                        ? `${(totalSize / 1024).toFixed(1)}GB`
                        : `${totalSize}MB`;
                    })()
                  : '0MB'}
              </Text>
              <Text size="xs" c="dimmed" mt={4}>
                Total usage
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
                {cacheStats && Object.keys(cacheStats).length > 0
                  ? (() => {
                      const avgLatencies = Object.values(cacheStats).map(stat => stat.avgLatency);
                      const avgLatency = avgLatencies.length > 0
                        ? (avgLatencies.reduce((sum, lat) => sum + lat, 0) / avgLatencies.length).toFixed(2)
                        : '0.00';
                      return `${avgLatency}ms`;
                    })()
                  : '0.00ms'}
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
                onChange={(value) => setSelectedCache(value || '')}
                data={cacheConfigs.map(c => ({ value: c.id, label: c.name }))}
                w={250}
                placeholder="Select a cache"
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

                {isLoadingEntries ? (
                  <LoadingOverlay visible={true} />
                ) : cacheEntries.length > 0 ? (
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
                        {cacheEntries.map((entry) => (
                          <Table.Tr key={entry.key}>
                            <Table.Td>
                              <Code style={{ maxWidth: '300px', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            {entry.key}
                          </Code>
                            </Table.Td>
                            <Table.Td>{entry.size}</Table.Td>
                            <Table.Td>
                              {entry.expiresAt 
                                ? formatters.duration(new Date(entry.expiresAt).getTime() - Date.now())
                                : 'No expiry'
                              }
                            </Table.Td>
                            <Table.Td>{entry.accessCount}</Table.Td>
                            <Table.Td>
                              <Text size="xs">{formatters.date(entry.lastAccessedAt, { relativeDays: 7 })}</Text>
                            </Table.Td>
                            <Table.Td>
                              <Text size="xs">{entry.expiresAt ? formatters.date(entry.expiresAt, { relativeDays: 7 }) : 'Never'}</Text>
                            </Table.Td>
                          </Table.Tr>
                        ))}
                      </Table.Tbody>
                    </Table>
                  </ScrollArea>
                ) : (
                  <Alert
                    icon={<IconInfoCircle size={16} />}
                    title="No cache entries available"
                    color="gray"
                  >
                    <Text size="sm">
                      Individual cache entry inspection is not available in the current backend implementation.
                      Use the statistics tab to view aggregate cache performance metrics.
                    </Text>
                  </Alert>
                )}
              </Stack>
            )}
          </Card>
        </Tabs.Panel>
      </Tabs>

      <LoadingOverlay visible={isRefreshing} />
    </Stack>
  );
}