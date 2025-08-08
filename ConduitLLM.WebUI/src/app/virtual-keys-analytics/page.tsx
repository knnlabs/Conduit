'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Grid,
  Card,
  Badge,
  ThemeIcon,
  Table,
  ScrollArea,
  Button,
  Select,
  MultiSelect,
  RingProgress,
  Skeleton,
  Tooltip,
  Tabs,
  Code,
} from '@mantine/core';
import {
  AreaChart,
  DonutChart,
} from '@mantine/charts';
import {
  IconKey,
  IconTrendingUp,
  IconApi,
  IconRefresh,
  IconDownload,
  IconFilter,
  IconArrowUpRight,
  IconArrowDownRight,
  IconEye,
  IconActivity,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback } from 'react';
import { CardSkeleton } from '@/components/common/LoadingState';
import { formatters } from '@/lib/utils/formatters';

function getErrorRateColor(errorRate: number): string {
  if (errorRate > 5) {
    return 'red';
  }
  if (errorRate > 2) {
    return 'orange';
  }
  return 'green';
}

interface VirtualKeyAnalytics {
  id: string;
  name: string;
  status: 'active' | 'inactive' | 'suspended';
  created: string;
  lastUsed: string;
  usage: {
    requests: number;
    requestsChange: number;
    tokens: number;
    tokensChange: number;
    cost: number;
    costChange: number;
    errorRate: number;
  };
  quotas: {
    requests: {
      used: number;
      limit: number;
      period: 'hour' | 'day' | 'month';
    };
    tokens: {
      used: number;
      limit: number;
      period: 'hour' | 'day' | 'month';
    };
    cost: {
      used: number;
      limit: number;
      period: 'hour' | 'day' | 'month';
    };
  };
  providers: {
    name: string;
    requests: number;
    cost: number;
    percentage: number;
  }[];
  models: {
    name: string;
    provider: string;
    requests: number;
    tokens: number;
    cost: number;
  }[];
  endpoints: {
    path: string;
    requests: number;
    avgDuration: number;
    errorRate: number;
  }[];
}

interface TimeSeriesData {
  timestamp: string;
  requests: number;
  tokens: number;
  cost: number;
  errorRate: number;
}

interface AggregateMetrics {
  totalRequests: number;
  totalTokens: number;
  totalCost: number;
  activeKeys: number;
  avgErrorRate: number;
  topKey: string;
}

interface VirtualKeysAnalyticsResponse {
  virtualKeys: VirtualKeyAnalytics[];
  timeSeries: Record<string, TimeSeriesData[]>;
  aggregateMetrics: AggregateMetrics;
}

export default function VirtualKeysAnalyticsPage() {
  const [timeRange, setTimeRange] = useState('7d');
  const [selectedKeys, setSelectedKeys] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [virtualKeys, setVirtualKeys] = useState<VirtualKeyAnalytics[]>([]);
  const [timeSeries, setTimeSeries] = useState<Record<string, TimeSeriesData[]>>({});
  const [aggregateMetrics, setAggregateMetrics] = useState<AggregateMetrics>({
    totalRequests: 0,
    totalTokens: 0,
    totalCost: 0,
    activeKeys: 0,
    avgErrorRate: 0,
    topKey: '',
  });

  const fetchAnalytics = useCallback(async () => {
    try {
      setIsLoading(true);
      const params = new URLSearchParams({
        range: timeRange,
        ...(selectedKeys.length > 0 && { keys: selectedKeys.join(',') }),
      });
      
      const response = await fetch(`/api/virtual-keys-analytics?${params}`);
      if (!response.ok) {
        throw new Error('Failed to fetch analytics');
      }
      const data = await response.json() as VirtualKeysAnalyticsResponse;
      
      setVirtualKeys(data.virtualKeys);
      setTimeSeries(data.timeSeries);
      setAggregateMetrics(data.aggregateMetrics);
    } catch (error) {
      console.error('Error fetching analytics:', error);
    } finally {
      setIsLoading(false);
    }
  }, [timeRange, selectedKeys]);

  useEffect(() => {
    void fetchAnalytics();
  }, [fetchAnalytics]);

  const getStatusColor = (status: string): string => {
    switch (status) {
      case 'active': return 'green';
      case 'inactive': return 'gray';
      case 'suspended': return 'red';
      default: return 'gray';
    }
  };

  const getChangeColor = (change: number): string => {
    if (change > 0) return 'green';
    if (change < 0) return 'red';
    return 'gray';
  };

  const getChangeIcon = (change: number) => {
    const Icon = change >= 0 ? IconArrowUpRight : IconArrowDownRight;
    return <Icon size={12} />;
  };

  const getQuotaColor = (used: number, limit: number): string => {
    const percentage = (used / limit) * 100;
    if (percentage < 70) return 'green';
    if (percentage < 90) return 'orange';
    return 'red';
  };

  const handleExport = async () => {
    try {
      const params = new URLSearchParams({
        range: timeRange,
        ...(selectedKeys.length > 0 && { keys: selectedKeys.join(',') }),
      });
      
      const response = await fetch(`/api/virtual-keys-analytics/export?${params}`);
      if (!response.ok) throw new Error('Export failed');

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `virtual-keys-analytics-${timeRange}-${new Date().toISOString()}.csv`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (error) {
      console.error('Export failed:', error);
    }
  };

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Virtual Keys Analytics</Title>
          <Text c="dimmed">Detailed usage analytics for each virtual key</Text>
        </div>
        <Group>
          <MultiSelect
            placeholder="Filter by keys"
            data={virtualKeys.map(k => ({ value: k.id, label: k.name }))}
            value={selectedKeys}
            onChange={setSelectedKeys}
            leftSection={<IconFilter size={16} />}
            clearable
            w={250}
          />
          <Select
            value={timeRange}
            onChange={(value) => setTimeRange(value ?? '7d')}
            data={[
              { value: '24h', label: 'Last 24 Hours' },
              { value: '7d', label: 'Last 7 Days' },
              { value: '30d', label: 'Last 30 Days' },
              { value: '90d', label: 'Last 90 Days' },
            ]}
          />
          <Button
            variant="light"
            leftSection={<IconDownload size={16} />}
            onClick={() => void handleExport()}
          >
            Export
          </Button>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={() => void fetchAnalytics()}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Group>

      {/* Aggregate Metrics */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 2 }}>
          {isLoading ? (
            <CardSkeleton height={100} />
          ) : (
            <Card withBorder>
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                Total Requests
              </Text>
              <Text size="xl" fw={700}>
                {formatters.shortNumber(aggregateMetrics.totalRequests)}
              </Text>
            </Card>
          )}
        </Grid.Col>
        <Grid.Col span={{ base: 12, sm: 6, md: 2 }}>
          {isLoading ? (
            <CardSkeleton height={100} />
          ) : (
            <Card withBorder>
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                Total Tokens
              </Text>
              <Text size="xl" fw={700}>
                {formatters.shortNumber(aggregateMetrics.totalTokens)}
              </Text>
            </Card>
          )}
        </Grid.Col>
        <Grid.Col span={{ base: 12, sm: 6, md: 2 }}>
          {isLoading ? (
            <CardSkeleton height={100} />
          ) : (
            <Card withBorder>
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                Total Cost
              </Text>
              <Text size="xl" fw={700}>
                {formatters.currency(aggregateMetrics.totalCost)}
              </Text>
            </Card>
          )}
        </Grid.Col>
        <Grid.Col span={{ base: 12, sm: 6, md: 2 }}>
          {isLoading ? (
            <CardSkeleton height={100} />
          ) : (
            <Card withBorder>
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                Active Keys
              </Text>
              <Text size="xl" fw={700}>
                {aggregateMetrics.activeKeys}
              </Text>
            </Card>
          )}
        </Grid.Col>
        <Grid.Col span={{ base: 12, sm: 6, md: 2 }}>
          {isLoading ? (
            <CardSkeleton height={100} />
          ) : (
            <Card withBorder>
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                Avg Error Rate
              </Text>
              <Text size="xl" fw={700}>
                {aggregateMetrics.avgErrorRate.toFixed(1)}%
              </Text>
            </Card>
          )}
        </Grid.Col>
        <Grid.Col span={{ base: 12, sm: 6, md: 2 }}>
          {isLoading ? (
            <CardSkeleton height={100} />
          ) : (
            <Card withBorder>
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                Top Key
              </Text>
              <Text size="sm" fw={700} truncate>
                {aggregateMetrics.topKey ?? 'N/A'}
              </Text>
            </Card>
          )}
        </Grid.Col>
      </Grid>

      {/* Virtual Keys Table */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Group justify="space-between">
            <Text fw={500}>Virtual Key Performance</Text>
            <Badge variant="light">
              {virtualKeys.length} keys
            </Badge>
          </Group>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          <ScrollArea>
            <Table striped highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Name</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Requests</Table.Th>
                  <Table.Th>Tokens</Table.Th>
                  <Table.Th>Cost</Table.Th>
                  <Table.Th>Error Rate</Table.Th>
                  <Table.Th>Quota Usage</Table.Th>
                  <Table.Th>Last Used</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {(() => {
                  if (isLoading) {
                    return (
                      <Table.Tr>
                        <Table.Td colSpan={8}>
                          <Skeleton height={200} />
                        </Table.Td>
                      </Table.Tr>
                    );
                  }
                  if (virtualKeys.length === 0) {
                    return (
                      <Table.Tr>
                        <Table.Td colSpan={8}>
                          <Text ta="center" c="dimmed" py="xl">
                            No virtual keys found
                          </Text>
                        </Table.Td>
                      </Table.Tr>
                    );
                  }
                  return virtualKeys.map((key) => (
                    <Table.Tr key={key.id}>
                      <Table.Td>
                        <Group gap="xs">
                          <ThemeIcon size="sm" variant="light">
                            <IconKey size={14} />
                          </ThemeIcon>
                          <div>
                            <Text size="sm" fw={500}>{key.name}</Text>
                            <Text size="xs" c="dimmed">{key.id}</Text>
                          </div>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Badge color={getStatusColor(key.status)} variant="light">
                          {key.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Text size="sm">{formatters.number(key.usage.requests)}</Text>
                          <ThemeIcon 
                            size="xs" 
                            variant="subtle" 
                            color={getChangeColor(key.usage.requestsChange)}
                          >
                            {getChangeIcon(key.usage.requestsChange)}
                          </ThemeIcon>
                          <Text size="xs" c={getChangeColor(key.usage.requestsChange)}>
                            {Math.abs(key.usage.requestsChange)}%
                          </Text>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Text size="sm">{formatters.shortNumber(key.usage.tokens)}</Text>
                          <ThemeIcon 
                            size="xs" 
                            variant="subtle" 
                            color={getChangeColor(key.usage.tokensChange)}
                          >
                            {getChangeIcon(key.usage.tokensChange)}
                          </ThemeIcon>
                          <Text size="xs" c={getChangeColor(key.usage.tokensChange)}>
                            {Math.abs(key.usage.tokensChange)}%
                          </Text>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Text size="sm">{formatters.currency(key.usage.cost)}</Text>
                          <ThemeIcon 
                            size="xs" 
                            variant="subtle" 
                            color={getChangeColor(key.usage.costChange)}
                          >
                            {getChangeIcon(key.usage.costChange)}
                          </ThemeIcon>
                          <Text size="xs" c={getChangeColor(key.usage.costChange)}>
                            {Math.abs(key.usage.costChange)}%
                          </Text>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Badge 
                          color={getErrorRateColor(key.usage.errorRate)}
                          variant="light"
                        >
                          {key.usage.errorRate.toFixed(1)}%
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Tooltip label={`Requests: ${key.quotas.requests.used}/${key.quotas.requests.limit}`}>
                            <RingProgress
                              size={30}
                              thickness={3}
                              sections={[{
                                value: (key.quotas.requests.used / key.quotas.requests.limit) * 100,
                                color: getQuotaColor(key.quotas.requests.used, key.quotas.requests.limit),
                              }]}
                            />
                          </Tooltip>
                          <Tooltip label={`Tokens: ${formatters.shortNumber(key.quotas.tokens.used)}/${formatters.shortNumber(key.quotas.tokens.limit)}`}>
                            <RingProgress
                              size={30}
                              thickness={3}
                              sections={[{
                                value: (key.quotas.tokens.used / key.quotas.tokens.limit) * 100,
                                color: getQuotaColor(key.quotas.tokens.used, key.quotas.tokens.limit),
                              }]}
                            />
                          </Tooltip>
                          <Tooltip label={`Cost: $${key.quotas.cost.used}/$${key.quotas.cost.limit}`}>
                            <RingProgress
                              size={30}
                              thickness={3}
                              sections={[{
                                value: (key.quotas.cost.used / key.quotas.cost.limit) * 100,
                                color: getQuotaColor(key.quotas.cost.used, key.quotas.cost.limit),
                              }]}
                            />
                          </Tooltip>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{formatters.date(key.lastUsed, { relativeDays: 3 })}</Text>
                      </Table.Td>
                    </Table.Tr>
                  ));
                })()}
              </Table.Tbody>
            </Table>
          </ScrollArea>
        </Card.Section>
      </Card>

      {/* Detailed Analytics for Top Keys */}
      {!isLoading && virtualKeys.slice(0, 3).map((key) => (
        <Card key={key.id} withBorder>
          <Card.Section withBorder inheritPadding py="xs">
            <Group justify="space-between">
              <Group gap="xs">
                <ThemeIcon size="sm" variant="light">
                  <IconKey size={14} />
                </ThemeIcon>
                <Text fw={500}>{key.name}</Text>
                <Badge size="sm" color={getStatusColor(key.status)} variant="light">
                  {key.status}
                </Badge>
              </Group>
              <Text size="sm" c="dimmed">
                Created {formatters.date(key.created)}
              </Text>
            </Group>
          </Card.Section>
          
          <Card.Section inheritPadding py="md">
            <Tabs defaultValue="overview">
              <Tabs.List>
                <Tabs.Tab value="overview" leftSection={<IconActivity size={14} />}>
                  Overview
                </Tabs.Tab>
                <Tabs.Tab value="providers" leftSection={<IconApi size={14} />}>
                  Providers
                </Tabs.Tab>
                <Tabs.Tab value="models" leftSection={<IconTrendingUp size={14} />}>
                  Models
                </Tabs.Tab>
                <Tabs.Tab value="endpoints" leftSection={<IconEye size={14} />}>
                  Endpoints
                </Tabs.Tab>
              </Tabs.List>

              <Tabs.Panel value="overview" pt="md">
                {timeSeries[key.id] && (
                  <AreaChart
                    h={200}
                    data={timeSeries[key.id]}
                    dataKey="timestamp"
                    series={[
                      { name: 'requests', color: 'blue.6' },
                      { name: 'cost', color: 'green.6' },
                    ]}
                    curveType="linear"
                  />
                )}
              </Tabs.Panel>

              <Tabs.Panel value="providers" pt="md">
                <Grid>
                  <Grid.Col span={6}>
                    <DonutChart
                      h={200}
                      data={key.providers.map(p => ({
                        name: p.name,
                        value: p.requests,
                        color: {
                          'OpenAI': 'blue.6',
                          'Anthropic': 'orange.6',
                          'Azure': 'cyan.6',
                          'Google': 'green.6',
                        }[p.name] ?? 'gray.6'
                      }))}
                      withLabelsLine
                      withLabels
                    />
                  </Grid.Col>
                  <Grid.Col span={6}>
                    <Table>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Provider</Table.Th>
                          <Table.Th>Requests</Table.Th>
                          <Table.Th>Cost</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {key.providers.map((provider) => (
                          <Table.Tr key={provider.name}>
                            <Table.Td>{provider.name}</Table.Td>
                            <Table.Td>{formatters.number(provider.requests)}</Table.Td>
                            <Table.Td>{formatters.currency(provider.cost)}</Table.Td>
                          </Table.Tr>
                        ))}
                      </Table.Tbody>
                    </Table>
                  </Grid.Col>
                </Grid>
              </Tabs.Panel>

              <Tabs.Panel value="models" pt="md">
                <ScrollArea h={200}>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Model</Table.Th>
                        <Table.Th>Provider</Table.Th>
                        <Table.Th>Requests</Table.Th>
                        <Table.Th>Tokens</Table.Th>
                        <Table.Th>Cost</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {key.models.map((model) => (
                        <Table.Tr key={`model-${key.id}-${model.name}`}>
                          <Table.Td>{model.name}</Table.Td>
                          <Table.Td>
                            <Badge size="sm" variant="light">{model.provider}</Badge>
                          </Table.Td>
                          <Table.Td>{formatters.number(model.requests)}</Table.Td>
                          <Table.Td>{formatters.shortNumber(model.tokens)}</Table.Td>
                          <Table.Td>{formatters.currency(model.cost)}</Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </ScrollArea>
              </Tabs.Panel>

              <Tabs.Panel value="endpoints" pt="md">
                <ScrollArea h={200}>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Endpoint</Table.Th>
                        <Table.Th>Requests</Table.Th>
                        <Table.Th>Avg Duration</Table.Th>
                        <Table.Th>Error Rate</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {key.endpoints.map((endpoint) => (
                        <Table.Tr key={`endpoint-${key.id}-${endpoint.path}`}>
                          <Table.Td><Code>{endpoint.path}</Code></Table.Td>
                          <Table.Td>{formatters.number(endpoint.requests)}</Table.Td>
                          <Table.Td>{endpoint.avgDuration}ms</Table.Td>
                          <Table.Td>
                            <Badge 
                              color={getErrorRateColor(endpoint.errorRate)}
                              variant="light"
                            >
                              {endpoint.errorRate.toFixed(1)}%
                            </Badge>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </ScrollArea>
              </Tabs.Panel>
            </Tabs>
          </Card.Section>
        </Card>
      ))}
    </Stack>
  );
}