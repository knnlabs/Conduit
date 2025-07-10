'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Paper,
  Grid,
  Select,
  Card,
  RingProgress,
  Button,
  Badge,
  ThemeIcon,
  Table,
  ScrollArea,
  Code,
  Skeleton,
} from '@mantine/core';
import {
  AreaChart,
  LineChart,
  BarChart,
  DonutChart,
} from '@mantine/charts';
import {
  IconTrendingUp,
  IconApi,
  IconCoins,
  IconUsers,
  IconCalendar,
  IconDownload,
  IconRefresh,
  IconArrowUpRight,
  IconArrowDownRight,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback } from 'react';
import { CardSkeleton } from '@/components/common/LoadingState';
import { formatters } from '@/lib/utils/formatters';

interface UsageMetrics {
  totalRequests: number;
  totalCost: number;
  totalTokens: number;
  activeVirtualKeys: number;
  requestsChange: number;
  costChange: number;
  tokensChange: number;
  virtualKeysChange: number;
}

interface TimeSeriesData {
  timestamp: string;
  requests: number;
  cost: number;
  tokens: number;
}

interface ProviderUsage {
  provider: string;
  requests: number;
  cost: number;
  tokens: number;
  percentage: number;
}

interface ModelUsage {
  model: string;
  provider: string;
  requests: number;
  cost: number;
  tokens: number;
}

interface VirtualKeyUsage {
  keyName: string;
  requests: number;
  cost: number;
  tokens: number;
  lastUsed: string;
}

interface EndpointUsage {
  endpoint: string;
  requests: number;
  avgDuration: number;
  errorRate: number;
}

export default function UsageAnalyticsPage() {
  const [timeRange, setTimeRange] = useState('7d');
  const [isLoading, setIsLoading] = useState(true);
  const [metrics, setMetrics] = useState<UsageMetrics | null>(null);
  const [timeSeriesData, setTimeSeriesData] = useState<TimeSeriesData[]>([]);
  const [providerUsage, setProviderUsage] = useState<ProviderUsage[]>([]);
  const [modelUsage, setModelUsage] = useState<ModelUsage[]>([]);
  const [virtualKeyUsage, setVirtualKeyUsage] = useState<VirtualKeyUsage[]>([]);
  const [endpointUsage, setEndpointUsage] = useState<EndpointUsage[]>([]);

  const fetchAnalytics = useCallback(async () => {
    try {
      setIsLoading(true);
      const response = await fetch(`/api/usage-analytics?range=${timeRange}`);
      if (!response.ok) {
        throw new Error('Failed to fetch analytics');
      }
      const data = await response.json();
      
      setMetrics(data.metrics);
      setTimeSeriesData(data.timeSeries || []);
      setProviderUsage(data.providerUsage || []);
      setModelUsage(data.modelUsage || []);
      setVirtualKeyUsage(data.virtualKeyUsage || []);
      setEndpointUsage(data.endpointUsage || []);
    } catch (error) {
      console.error('Error fetching analytics:', error);
    } finally {
      setIsLoading(false);
    }
  }, [timeRange]);

  useEffect(() => {
    fetchAnalytics();
  }, [fetchAnalytics]);

  const getChangeColor = (change: number): string => {
    if (change > 0) return 'green';
    if (change < 0) return 'red';
    return 'gray';
  };

  const getChangeIcon = (change: number) => {
    return change >= 0 ? IconArrowUpRight : IconArrowDownRight;
  };

  const handleExport = async () => {
    try {
      const response = await fetch(`/api/usage-analytics/export?range=${timeRange}`);
      if (!response.ok) throw new Error('Export failed');

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `usage-analytics-${timeRange}-${new Date().toISOString()}.csv`;
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
          <Title order={1}>Usage Analytics</Title>
          <Text c="dimmed">Comprehensive API usage statistics and trends</Text>
        </div>
        <Group>
          <Select
            value={timeRange}
            onChange={(value) => setTimeRange(value || '7d')}
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
            onClick={handleExport}
          >
            Export
          </Button>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={fetchAnalytics}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Group>

      {/* Key Metrics */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={120} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="md">
                <div>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                    Total Requests
                  </Text>
                  <Text size="xl" fw={700}>
                    {formatters.number(metrics?.totalRequests || 0)}
                  </Text>
                </div>
                <ThemeIcon color="blue" variant="light" size="xl">
                  <IconApi size={24} />
                </ThemeIcon>
              </Group>
              <Group gap="xs">
                <ThemeIcon 
                  size="xs" 
                  variant="subtle" 
                  color={getChangeColor(metrics?.requestsChange || 0)}
                >
                  {getChangeIcon(metrics?.requestsChange || 0)({ size: 14 })}
                </ThemeIcon>
                <Text size="xs" c={getChangeColor(metrics?.requestsChange || 0)}>
                  {Math.abs(metrics?.requestsChange || 0)}%
                </Text>
                <Text size="xs" c="dimmed">vs previous period</Text>
              </Group>
            </Card>
          )}
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={120} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="md">
                <div>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                    Total Cost
                  </Text>
                  <Text size="xl" fw={700}>
                    ${formatters.currency(metrics?.totalCost || 0)}
                  </Text>
                </div>
                <ThemeIcon color="green" variant="light" size="xl">
                  <IconCoins size={24} />
                </ThemeIcon>
              </Group>
              <Group gap="xs">
                <ThemeIcon 
                  size="xs" 
                  variant="subtle" 
                  color={getChangeColor(metrics?.costChange || 0)}
                >
                  {getChangeIcon(metrics?.costChange || 0)({ size: 14 })}
                </ThemeIcon>
                <Text size="xs" c={getChangeColor(metrics?.costChange || 0)}>
                  {Math.abs(metrics?.costChange || 0)}%
                </Text>
                <Text size="xs" c="dimmed">vs previous period</Text>
              </Group>
            </Card>
          )}
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={120} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="md">
                <div>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                    Total Tokens
                  </Text>
                  <Text size="xl" fw={700}>
                    {formatters.shortNumber(metrics?.totalTokens || 0)}
                  </Text>
                </div>
                <ThemeIcon color="cyan" variant="light" size="xl">
                  <IconTrendingUp size={24} />
                </ThemeIcon>
              </Group>
              <Group gap="xs">
                <ThemeIcon 
                  size="xs" 
                  variant="subtle" 
                  color={getChangeColor(metrics?.tokensChange || 0)}
                >
                  {getChangeIcon(metrics?.tokensChange || 0)({ size: 14 })}
                </ThemeIcon>
                <Text size="xs" c={getChangeColor(metrics?.tokensChange || 0)}>
                  {Math.abs(metrics?.tokensChange || 0)}%
                </Text>
                <Text size="xs" c="dimmed">vs previous period</Text>
              </Group>
            </Card>
          )}
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          {isLoading ? (
            <CardSkeleton height={120} />
          ) : (
            <Card withBorder>
              <Group justify="space-between" mb="md">
                <div>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                    Active Keys
                  </Text>
                  <Text size="xl" fw={700}>
                    {metrics?.activeVirtualKeys || 0}
                  </Text>
                </div>
                <ThemeIcon color="orange" variant="light" size="xl">
                  <IconUsers size={24} />
                </ThemeIcon>
              </Group>
              <Group gap="xs">
                <ThemeIcon 
                  size="xs" 
                  variant="subtle" 
                  color={getChangeColor(metrics?.virtualKeysChange || 0)}
                >
                  {getChangeIcon(metrics?.virtualKeysChange || 0)({ size: 14 })}
                </ThemeIcon>
                <Text size="xs" c={getChangeColor(metrics?.virtualKeysChange || 0)}>
                  {Math.abs(metrics?.virtualKeysChange || 0)}%
                </Text>
                <Text size="xs" c="dimmed">vs previous period</Text>
              </Group>
            </Card>
          )}
        </Grid.Col>
      </Grid>

      {/* Usage Over Time */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Text fw={500}>Usage Over Time</Text>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          {isLoading ? (
            <Skeleton height={300} />
          ) : (
            <AreaChart
              h={300}
              data={timeSeriesData}
              dataKey="timestamp"
              series={[
                { name: 'requests', color: 'blue.6' },
                { name: 'cost', color: 'green.6' },
              ]}
              curveType="linear"
              withLegend
              legendProps={{ verticalAlign: 'bottom', height: 50 }}
              valueFormatter={(value) => formatters.number(value)}
            />
          )}
        </Card.Section>
      </Card>

      <Grid>
        {/* Provider Usage */}
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card withBorder h="100%">
            <Card.Section withBorder inheritPadding py="xs">
              <Text fw={500}>Usage by Provider</Text>
            </Card.Section>
            <Card.Section inheritPadding py="md">
              {isLoading ? (
                <Skeleton height={250} />
              ) : (
                <Stack gap="md">
                  <DonutChart
                    h={200}
                    data={providerUsage.map(p => ({
                      name: p.provider,
                      value: p.requests,
                      color: {
                        'OpenAI': 'blue.6',
                        'Anthropic': 'orange.6',
                        'Azure': 'cyan.6',
                        'Google': 'green.6',
                        'Replicate': 'purple.6',
                      }[p.provider] || 'gray.6'
                    }))}
                    withLabelsLine
                    withLabels
                    paddingAngle={2}
                  />
                  <ScrollArea h={150}>
                    <Table>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Provider</Table.Th>
                          <Table.Th>Requests</Table.Th>
                          <Table.Th>Cost</Table.Th>
                          <Table.Th>%</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {providerUsage.map((provider) => (
                          <Table.Tr key={provider.provider}>
                            <Table.Td>{provider.provider}</Table.Td>
                            <Table.Td>{formatters.number(provider.requests)}</Table.Td>
                            <Table.Td>${formatters.currency(provider.cost)}</Table.Td>
                            <Table.Td>{provider.percentage}%</Table.Td>
                          </Table.Tr>
                        ))}
                      </Table.Tbody>
                    </Table>
                  </ScrollArea>
                </Stack>
              )}
            </Card.Section>
          </Card>
        </Grid.Col>

        {/* Top Models */}
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card withBorder h="100%">
            <Card.Section withBorder inheritPadding py="xs">
              <Text fw={500}>Top Models by Usage</Text>
            </Card.Section>
            <Card.Section inheritPadding py="md">
              {isLoading ? (
                <Skeleton height={250} />
              ) : (
                <ScrollArea h={450}>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Model</Table.Th>
                        <Table.Th>Provider</Table.Th>
                        <Table.Th>Requests</Table.Th>
                        <Table.Th>Cost</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {modelUsage.slice(0, 10).map((model, index) => (
                        <Table.Tr key={`${model.provider}-${model.model}`}>
                          <Table.Td>
                            <Text size="sm" fw={500}>{model.model}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Badge size="sm" variant="light">
                              {model.provider}
                            </Badge>
                          </Table.Td>
                          <Table.Td>{formatters.number(model.requests)}</Table.Td>
                          <Table.Td>${formatters.currency(model.cost)}</Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </ScrollArea>
              )}
            </Card.Section>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Virtual Key Usage */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Group justify="space-between">
            <Text fw={500}>Virtual Key Usage</Text>
            <Text size="sm" c="dimmed">Top 10 by request volume</Text>
          </Group>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          {isLoading ? (
            <Skeleton height={300} />
          ) : (
            <BarChart
              h={300}
              data={virtualKeyUsage.slice(0, 10)}
              dataKey="keyName"
              series={[
                { name: 'requests', color: 'blue.6' },
              ]}
              tickLine="y"
              gridAxis="y"
              valueFormatter={(value) => formatters.number(value)}
            />
          )}
        </Card.Section>
      </Card>

      {/* Endpoint Performance */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Text fw={500}>Endpoint Performance</Text>
        </Card.Section>
        <Card.Section inheritPadding py="md">
          <ScrollArea>
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
                {isLoading ? (
                  <Table.Tr>
                    <Table.Td colSpan={4}>
                      <Skeleton height={200} />
                    </Table.Td>
                  </Table.Tr>
                ) : (
                  endpointUsage.map((endpoint) => (
                    <Table.Tr key={endpoint.endpoint}>
                      <Table.Td>
                        <Code>{endpoint.endpoint}</Code>
                      </Table.Td>
                      <Table.Td>{formatters.number(endpoint.requests)}</Table.Td>
                      <Table.Td>{endpoint.avgDuration}ms</Table.Td>
                      <Table.Td>
                        <Badge 
                          color={endpoint.errorRate > 5 ? 'red' : endpoint.errorRate > 1 ? 'orange' : 'green'}
                          variant="light"
                        >
                          {endpoint.errorRate.toFixed(1)}%
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  ))
                )}
              </Table.Tbody>
            </Table>
          </ScrollArea>
        </Card.Section>
      </Card>
    </Stack>
  );
}