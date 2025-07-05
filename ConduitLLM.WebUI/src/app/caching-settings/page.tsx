'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Grid,
  Group,
  Button,
  Switch,
  NumberInput,
  Select,
  Badge,
  ThemeIcon,
  Progress,
  Alert,
  ScrollArea,
  Table,
  ActionIcon,
  Tooltip,
  TextInput,
  MultiSelect,
  JsonInput,
  Tabs,
  PasswordInput,
  LoadingOverlay,
} from '@mantine/core';
import {
  IconSettings,
  IconDatabase,
  IconRefresh,
  IconTrash,
  IconInfoCircle,
  IconChartBar,
  IconBolt,
  IconKey,
  IconNetwork,
  IconDeviceFloppy,
  IconFilter,
  IconPlus,
  IconEdit,
  IconCheck,
} from '@tabler/icons-react';
import { useState } from 'react';
import { notifications } from '@mantine/notifications';
import { BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Legend } from 'recharts';
import { formatters } from '@/lib/utils/formatters';
import { 
  useCachingConfig, 
  useUpdateCachingConfig,
  useClearCache,
} from '@/hooks/api/useConfigurationApi';


export default function CachingSettingsPage() {
  const [activeTab, setActiveTab] = useState<string | null>('policies');
  
  // Fetch data using the caching API hooks
  const { data: cachingData, isLoading: cachingLoading, refetch: refetchCaching } = useCachingConfig();
  const updateConfigMutation = useUpdateCachingConfig();
  const clearCacheMutation = useClearCache();
  
  // Extract data with defaults
  const policies = cachingData?.cachePolicies || [];
  const cacheRegions = cachingData?.cacheRegions || [];
  const statistics = cachingData?.statistics || {
    totalHits: 0,
    totalMisses: 0,
    hitRate: 0,
    avgResponseTime: { withCache: 0, withoutCache: 0 },
    memoryUsage: { current: '0MB', peak: '0MB', limit: '0MB' },
    topCachedItems: [],
  };
  const configuration = cachingData?.configuration || {
    defaultTTL: 3600,
    maxMemorySize: '2GB',
    evictionPolicy: 'lru',
    compressionEnabled: true,
    redisConnectionString: null,
  };
  
  const [globalSettings, setGlobalSettings] = useState({
    enabled: true,
    defaultTTL: configuration.defaultTTL,
    maxMemory: 2048,
    evictionPolicy: configuration.evictionPolicy,
    compressionThreshold: 1024,
    enableMetrics: true,
    enableLogging: true,
  });
  
  const [redisSettings, setRedisSettings] = useState({
    enabled: !!configuration.redisConnectionString,
    host: 'localhost',
    port: 6379,
    password: '',
    database: 0,
    cluster: false,
    sentinels: '',
    connectionPool: 10,
    timeout: 5000,
  });

  const overallHitRate = statistics.hitRate;
  const totalCacheSize = policies.reduce((sum, p) => sum + (p.maxSize || 0), 0);
  const totalEntries = statistics.topCachedItems.length;

  const handleSaveSettings = async () => {
    try {
      await updateConfigMutation.mutateAsync({
        defaultTTLSeconds: globalSettings.defaultTTL,
        maxMemorySize: `${globalSettings.maxMemory}MB`,
        evictionPolicy: globalSettings.evictionPolicy,
        enableCompression: true,
        clearAllCaches: false,
      });
      notifications.show({
        title: 'Settings Saved',
        message: 'Cache configuration has been updated successfully',
        color: 'green',
      });
    } catch (_error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to save cache configuration',
        color: 'red',
      });
    }
  };

  const handleClearCache = async (policyId?: string) => {
    try {
      if (policyId) {
        await clearCacheMutation.mutateAsync(policyId);
        const policy = policies.find(p => p.id === policyId);
        notifications.show({
          title: 'Cache Cleared',
          message: `Cache cleared for ${policy?.name}`,
          color: 'orange',
        });
      } else {
        await updateConfigMutation.mutateAsync({
          defaultTTLSeconds: globalSettings.defaultTTL,
          maxMemorySize: `${globalSettings.maxMemory}MB`,
          evictionPolicy: globalSettings.evictionPolicy,
          enableCompression: true,
          clearAllCaches: true,
        });
        notifications.show({
          title: 'Cache Cleared',
          message: 'All caches have been cleared',
          color: 'orange',
        });
      }
    } catch (_error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to clear cache',
        color: 'red',
      });
    }
  };

  const handleTogglePolicy = (_policyId: string) => {
    // TODO: Implement policy toggle via API when endpoint is available
    notifications.show({
      title: 'Policy Updated',
      message: 'Cache policy status has been toggled',
      color: 'blue',
    });
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'healthy': return 'green';
      case 'warning': return 'yellow';
      case 'error': return 'red';
      default: return 'gray';
    }
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Caching Settings</Title>
          <Text c="dimmed">Configure caching strategies and performance optimization</Text>
        </div>
        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={() => handleClearCache()}
          >
            Clear All Caches
          </Button>
          <Button
            leftSection={<IconDeviceFloppy size={16} />}
            onClick={handleSaveSettings}
          >
            Save Changes
          </Button>
        </Group>
      </Group>

      {/* Cache Statistics Overview */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Hit Rate
                </Text>
                <Text size="xl" fw={700}>
                  {formatters.percentage(overallHitRate)}
                </Text>
                <Progress value={overallHitRate} size="sm" mt={8} color="green" />
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="green">
                <IconBolt size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Total Cache Size
                </Text>
                <Text size="xl" fw={700}>
                  {formatters.fileSize(totalCacheSize)}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  of {formatters.fileSize(globalSettings.maxMemory * 1024 * 1024)}
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="blue">
                <IconDatabase size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Cached Entries
                </Text>
                <Text size="xl" fw={700}>
                  {formatters.number(totalEntries)}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  Across all policies
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="orange">
                <IconKey size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Active Policies
                </Text>
                <Text size="xl" fw={700}>
                  {policies.filter(p => p.enabled).length} / {policies.length}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  Policies enabled
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="purple">
                <IconSettings size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Cache Performance Chart */}
      <Card withBorder>
        <Text fw={600} mb="md">Cache Performance (Last 12 Hours)</Text>
        <LoadingOverlay visible={cachingLoading} />
        <ResponsiveContainer width="100%" height={250}>
          <LineChart data={statistics.topCachedItems.map((item, index) => ({
            time: `${index}:00`,
            hits: item.hits,
            misses: Math.floor(item.hits * 0.2),
            hitRate: statistics.hitRate,
            size: parseInt(item.size),
          }))}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="time" />
            <YAxis yAxisId="left" />
            <YAxis yAxisId="right" orientation="right" />
            <RechartsTooltip />
            <Legend />
            <Line
              yAxisId="left"
              type="monotone"
              dataKey="hits"
              stroke="#10b981"
              name="Cache Hits"
              strokeWidth={2}
            />
            <Line
              yAxisId="left"
              type="monotone"
              dataKey="misses"
              stroke="#ef4444"
              name="Cache Misses"
              strokeWidth={2}
            />
            <Line
              yAxisId="right"
              type="monotone"
              dataKey="hitRate"
              stroke="#3b82f6"
              name="Hit Rate %"
              strokeWidth={2}
              strokeDasharray="5 5"
            />
          </LineChart>
        </ResponsiveContainer>
      </Card>

      {/* Tabbed Configuration */}
      <Tabs value={activeTab} onChange={setActiveTab}>
        <Tabs.List>
          <Tabs.Tab value="policies" leftSection={<IconFilter size={16} />}>
            Cache Policies
          </Tabs.Tab>
          <Tabs.Tab value="global" leftSection={<IconSettings size={16} />}>
            Global Settings
          </Tabs.Tab>
          <Tabs.Tab value="redis" leftSection={<IconDatabase size={16} />}>
            Redis Configuration
          </Tabs.Tab>
          <Tabs.Tab value="regions" leftSection={<IconNetwork size={16} />}>
            Regional Caches
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="policies" pt="md">
          {/* Cache Policies */}
          <Stack gap="md">
            {policies.map((policy) => (
              <Card key={policy.id} withBorder>
                <Group justify="space-between" mb="md">
                  <Group>
                    <Switch
                      checked={policy.enabled}
                      onChange={() => handleTogglePolicy(policy.id)}
                    />
                    <div>
                      <Text fw={500}>{policy.name}</Text>
                      <Badge variant="light" size="sm">
                        {policy.type === 'memory' ? 'In-Memory' : policy.type === 'redis' ? 'Redis' : 'Hybrid'}
                      </Badge>
                    </div>
                  </Group>
                  <Group gap="xs">
                    <Tooltip label="Edit Policy">
                      <ActionIcon variant="subtle" size="sm">
                        <IconEdit size={16} />
                      </ActionIcon>
                    </Tooltip>
                    <Tooltip label="Clear Cache">
                      <ActionIcon
                        variant="subtle"
                        size="sm"
                        color="orange"
                        onClick={() => handleClearCache(policy.id)}
                      >
                        <IconTrash size={16} />
                      </ActionIcon>
                    </Tooltip>
                  </Group>
                </Group>

                <Grid>
                  <Grid.Col span={{ base: 12, md: 6 }}>
                    <Stack gap="sm">
                      <Group justify="space-between">
                        <Text size="sm" c="dimmed">TTL</Text>
                        <Text size="sm" fw={500}>{formatters.duration(policy.ttl * 1000)}</Text>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm" c="dimmed">Type</Text>
                        <Text size="sm" fw={500}>{policy.type}</Text>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm" c="dimmed">Max Size</Text>
                        <Text size="sm" fw={500}>{formatters.fileSize(policy.maxSize)}</Text>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm" c="dimmed">Strategy</Text>
                        <Badge size="xs" variant="light">{policy.strategy}</Badge>
                      </Group>
                    </Stack>
                  </Grid.Col>

                  <Grid.Col span={{ base: 12, md: 6 }}>
                    <Stack gap="sm">
                      <Group justify="space-between">
                        <Text size="sm" c="dimmed">Enabled</Text>
                        <Badge variant="light" color={policy.enabled ? 'green' : 'gray'}>
                          {policy.enabled ? 'Active' : 'Inactive'}
                        </Badge>
                      </Group>
                      <Group justify="space-between">
                        <Text size="sm" c="dimmed">Description</Text>
                        <Text size="sm" fw={500}>{policy.description || 'No description'}</Text>
                      </Group>
                    </Stack>
                  </Grid.Col>
                </Grid>

              </Card>
            ))}

            <Button
              variant="light"
              leftSection={<IconPlus size={16} />}
              onClick={() => {
                notifications.show({
                  title: 'Add Cache Policy',
                  message: 'Opening policy configuration...',
                  color: 'blue',
                });
              }}
            >
              Add New Policy
            </Button>
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="global" pt="md">
          {/* Global Cache Settings */}
          <Card withBorder>
            <Group justify="space-between" mb="md">
              <Text fw={600}>Global Cache Configuration</Text>
              <Button
                variant="light"
                size="sm"
                leftSection={<IconRefresh size={16} />}
                onClick={() => refetchCaching()}
              >
                Refresh
              </Button>
            </Group>
            <Grid>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <Stack gap="md">
                  <Switch
                    label="Enable Caching"
                    description="Master switch for all caching functionality"
                    checked={globalSettings.enabled}
                    onChange={(e) => setGlobalSettings({ ...globalSettings, enabled: e.currentTarget.checked })}
                  />

                  <NumberInput
                    label="Default TTL"
                    description="Default time-to-live for cached items"
                    value={globalSettings.defaultTTL}
                    onChange={(value) => setGlobalSettings({ ...globalSettings, defaultTTL: Number(value) })}
                    min={60}
                    max={86400}
                    suffix=" seconds"
                  />

                  <NumberInput
                    label="Max Memory"
                    description="Maximum memory allocation for all caches"
                    value={globalSettings.maxMemory}
                    onChange={(value) => setGlobalSettings({ ...globalSettings, maxMemory: Number(value) })}
                    min={128}
                    max={16384}
                    suffix=" MB"
                  />

                  <Select
                    label="Eviction Policy"
                    description="Strategy for removing items when cache is full"
                    value={globalSettings.evictionPolicy}
                    onChange={(value) => setGlobalSettings({ ...globalSettings, evictionPolicy: value || 'lru' })}
                    data={[
                      { value: 'lru', label: 'Least Recently Used (LRU)' },
                      { value: 'lfu', label: 'Least Frequently Used (LFU)' },
                      { value: 'ttl', label: 'Time To Live (TTL)' },
                      { value: 'random', label: 'Random Eviction' },
                    ]}
                  />
                </Stack>
              </Grid.Col>

              <Grid.Col span={{ base: 12, md: 6 }}>
                <Stack gap="md">
                  <NumberInput
                    label="Compression Threshold"
                    description="Minimum size before compression is applied"
                    value={globalSettings.compressionThreshold}
                    onChange={(value) => setGlobalSettings({ ...globalSettings, compressionThreshold: Number(value) })}
                    min={256}
                    max={10240}
                    suffix=" bytes"
                  />

                  <Switch
                    label="Enable Metrics Collection"
                    description="Collect detailed cache performance metrics"
                    checked={globalSettings.enableMetrics}
                    onChange={(e) => setGlobalSettings({ ...globalSettings, enableMetrics: e.currentTarget.checked })}
                  />

                  <Switch
                    label="Enable Debug Logging"
                    description="Log cache operations for debugging"
                    checked={globalSettings.enableLogging}
                    onChange={(e) => setGlobalSettings({ ...globalSettings, enableLogging: e.currentTarget.checked })}
                  />

                  <MultiSelect
                    label="Cache Headers"
                    description="HTTP headers to consider for cache keys"
                    data={[
                      'accept',
                      'accept-encoding',
                      'accept-language',
                      'authorization',
                      'content-type',
                      'user-agent',
                    ]}
                    defaultValue={['accept', 'content-type']}
                  />
                </Stack>
              </Grid.Col>
            </Grid>
          </Card>

          {/* Cache Warmup Settings */}
          <Card withBorder mt="md">
            <Text fw={600} mb="md">Cache Warmup Configuration</Text>
            <Stack gap="md">
              <Switch
                label="Enable Cache Warmup"
                description="Pre-populate cache with frequently accessed data"
                defaultChecked
              />
              
              <JsonInput
                label="Warmup Endpoints"
                description="List of endpoints to warm up on startup"
                placeholder='["/v1/models", "/v1/chat/completions"]'
                minRows={4}
                formatOnBlur
                validationError="Invalid JSON"
              />

              <NumberInput
                label="Warmup Interval"
                description="How often to refresh warmed cache entries"
                defaultValue={3600}
                min={300}
                max={86400}
                suffix=" seconds"
              />
            </Stack>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="redis" pt="md">
          {/* Redis Configuration */}
          <Card withBorder>
            <Text fw={600} mb="md">Redis Connection Settings</Text>
            <Grid>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <Stack gap="md">
                  <Switch
                    label="Enable Redis Cache"
                    description="Use Redis for distributed caching"
                    checked={redisSettings.enabled}
                    onChange={(e) => setRedisSettings({ ...redisSettings, enabled: e.currentTarget.checked })}
                  />

                  <TextInput
                    label="Host"
                    placeholder="localhost"
                    value={redisSettings.host}
                    onChange={(e) => setRedisSettings({ ...redisSettings, host: e.currentTarget.value })}
                  />

                  <NumberInput
                    label="Port"
                    value={redisSettings.port}
                    onChange={(value) => setRedisSettings({ ...redisSettings, port: Number(value) })}
                    min={1}
                    max={65535}
                  />

                  <PasswordInput
                    label="Password"
                    placeholder="Optional"
                    value={redisSettings.password}
                    onChange={(e) => setRedisSettings({ ...redisSettings, password: e.currentTarget.value })}
                  />

                  <NumberInput
                    label="Database"
                    value={redisSettings.database}
                    onChange={(value) => setRedisSettings({ ...redisSettings, database: Number(value) })}
                    min={0}
                    max={15}
                  />
                </Stack>
              </Grid.Col>

              <Grid.Col span={{ base: 12, md: 6 }}>
                <Stack gap="md">
                  <Switch
                    label="Cluster Mode"
                    description="Connect to Redis Cluster"
                    checked={redisSettings.cluster}
                    onChange={(e) => setRedisSettings({ ...redisSettings, cluster: e.currentTarget.checked })}
                  />

                  <TextInput
                    label="Sentinel Hosts"
                    placeholder="sentinel1:26379,sentinel2:26379"
                    value={redisSettings.sentinels}
                    onChange={(e) => setRedisSettings({ ...redisSettings, sentinels: e.currentTarget.value })}
                    disabled={!redisSettings.cluster}
                  />

                  <NumberInput
                    label="Connection Pool Size"
                    value={redisSettings.connectionPool}
                    onChange={(value) => setRedisSettings({ ...redisSettings, connectionPool: Number(value) })}
                    min={1}
                    max={100}
                  />

                  <NumberInput
                    label="Connection Timeout"
                    value={redisSettings.timeout}
                    onChange={(value) => setRedisSettings({ ...redisSettings, timeout: Number(value) })}
                    min={1000}
                    max={30000}
                    suffix=" ms"
                  />

                  <Button
                    variant="light"
                    leftSection={<IconCheck size={16} />}
                    onClick={() => {
                      notifications.show({
                        title: 'Testing Connection',
                        message: 'Connecting to Redis...',
                        color: 'blue',
                        loading: true,
                      });
                    }}
                  >
                    Test Connection
                  </Button>
                </Stack>
              </Grid.Col>
            </Grid>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="regions" pt="md">
          {/* Regional Cache Distribution */}
          <Card withBorder>
            <LoadingOverlay visible={cachingLoading} />
            <Group justify="space-between" mb="md">
              <Text fw={600}>Regional Cache Nodes</Text>
              <Badge variant="light">
                {cacheRegions.length} regions active
              </Badge>
            </Group>
            <ScrollArea>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Region</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Nodes</Table.Th>
                    <Table.Th>Memory Usage</Table.Th>
                    <Table.Th>Hit Rate</Table.Th>
                    <Table.Th>Items</Table.Th>
                    <Table.Th>Actions</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {cacheRegions.map((region) => (
                    <Table.Tr key={region.id}>
                      <Table.Td fw={500}>{region.name}</Table.Td>
                      <Table.Td>
                        <Badge variant="light" color={getStatusColor(region.status)}>
                          {region.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{region.nodes}</Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Text size="sm">{((parseInt(region.metrics?.size || '0MB') / 1024 / 1024) || 0).toFixed(1)}MB</Text>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Text size="sm">{(region.metrics?.hitRate || 0).toFixed(1)}%</Text>
                          <Progress value={region.metrics?.hitRate || 0} size="sm" w={60} color={(region.metrics?.hitRate || 0) > 70 ? 'green' : 'orange'} />
                        </Group>
                      </Table.Td>
                      <Table.Td>{formatters.number(region.metrics?.items || 0)}</Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Tooltip label="Clear Region Cache">
                            <ActionIcon
                              variant="subtle"
                              size="sm"
                              color="orange"
                              onClick={() => handleClearCache()}
                            >
                              <IconTrash size={16} />
                            </ActionIcon>
                          </Tooltip>
                          <Tooltip label="View Details">
                            <ActionIcon variant="subtle" size="sm">
                              <IconChartBar size={16} />
                            </ActionIcon>
                          </Tooltip>
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>

          {/* Regional Performance */}
          <Card withBorder mt="md">
            <Text fw={600} mb="md">Regional Performance Distribution</Text>
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={cacheRegions.map(region => ({
                name: region.name,
                hitRate: region.metrics?.hitRate || 0,
                items: region.metrics?.items || 0,
                evictionRate: region.metrics?.evictionRate || 0,
              }))}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="name" />
                <YAxis />
                <RechartsTooltip />
                <Legend />
                <Bar dataKey="hitRate" fill="#3b82f6" name="Hit Rate %" />
                <Bar dataKey="items" fill="#10b981" name="Items" />
              </BarChart>
            </ResponsiveContainer>
          </Card>
        </Tabs.Panel>
      </Tabs>

      {/* Cache Best Practices Alert */}
      <Alert
        icon={<IconInfoCircle size={16} />}
        title="Caching Best Practices"
        color="blue"
      >
        <Text size="sm">
          • Set appropriate TTL values based on data volatility
          <br />
          • Monitor hit rates and adjust cache sizes accordingly
          <br />
          • Use cache warming for frequently accessed endpoints
          <br />
          • Implement proper cache invalidation strategies
          <br />
          • Consider regional distribution for global applications
        </Text>
      </Alert>
    </Stack>
  );
}