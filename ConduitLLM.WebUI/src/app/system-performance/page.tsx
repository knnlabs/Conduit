'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  SimpleGrid,
  ThemeIcon,
  Select,
  Tabs,
  LoadingOverlay,
  Alert,
  Badge,
  Table,
  Progress,
  ActionIcon,
  Tooltip,
  Center,
  RingProgress,
} from '@mantine/core';
import {
  IconRefresh,
  IconAlertCircle,
  IconCircleCheck,
  IconChartLine,
  IconCurrencyDollar,
  IconAlertTriangle,
  IconActivity,
  IconExternalLink,
  IconClock,
} from '@tabler/icons-react';
import { useState } from 'react';
import { 
  useProviderHealth,
} from '@/hooks/api/useProviderHealthApi';
import { 
  useCostSummary,
  useCostTrends
} from '@/hooks/api/useAnalyticsApi';
import type { TimeRangeFilter } from '@/types/analytics-types';
import { notifications } from '@mantine/notifications';
import { CostChart, type ChartDataItem } from '@/components/charts/CostChart';

export default function SystemPerformancePage() {
  const [timeRangeValue, setTimeRangeValue] = useState('24h');
  const [selectedTab, setSelectedTab] = useState('overview');
  
  const timeRange: TimeRangeFilter = { range: timeRangeValue as '1h' | '24h' | '7d' | '30d' | '90d' | 'custom' };
  
  // Provider health data
  const { data: providerHealth, isLoading: providerHealthLoading, refetch: refetchProviderHealth } = useProviderHealth();
  
  // Cost analytics data
  const { data: costSummary, isLoading: costSummaryLoading } = useCostSummary(timeRange);
  const { data: costTrends, isLoading: costTrendsLoading } = useCostTrends(timeRange);

  const isLoading = providerHealthLoading || costSummaryLoading;

  const handleRefresh = () => {
    refetchProviderHealth();
    notifications.show({
      title: 'Refreshing Data',
      message: 'Updating performance metrics...',
      color: 'blue',
    });
  };

  const getStatusColor = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'healthy':
      case 'connected':
      case 'operational':
        return 'green';
      case 'degraded':
      case 'warning':
        return 'orange';
      case 'unhealthy':
      case 'error':
      case 'failed':
        return 'red';
      default:
        return 'gray';
    }
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
      maximumFractionDigits: 4,
    }).format(amount);
  };

  const formatNumber = (num: number) => {
    return new Intl.NumberFormat('en-US').format(num);
  };

  // Calculate overall health percentage
  const overallHealth = providerHealth && providerHealth.length > 0
    ? (providerHealth.filter((p) => p.status === 'healthy').length / providerHealth.length) * 100
    : 0;

  // Extract health incidents from provider issues
  const allIssues = providerHealth?.flatMap(provider => 
    provider.issues.map(issue => ({
      ...issue,
      provider: provider.providerName,
      providerId: provider.providerId,
      startTime: issue.timestamp,
      description: issue.message,
    }))
  ) || [];

  const healthIncidents = {
    activeIncidents: allIssues.filter(issue => !issue.resolved),
    resolvedIncidents: allIssues.filter(issue => issue.resolved),
  };

  // Performance overview cards
  const performanceCards = [
    {
      title: 'System Health',
      value: `${overallHealth.toFixed(0)}%`,
      icon: IconActivity,
      color: overallHealth >= 90 ? 'green' : overallHealth >= 70 ? 'orange' : 'red',
      description: `${providerHealth?.filter((p) => p.status === 'healthy').length || 0} of ${providerHealth?.length || 0} providers healthy`,
    },
    {
      title: 'Total Requests',
      value: formatNumber(costSummary?.totalRequests || 0),
      icon: IconChartLine,
      color: 'blue',
      description: `Last ${timeRangeValue}`,
    },
    {
      title: 'Total Cost',
      value: formatCurrency(costSummary?.totalSpend || 0),
      icon: IconCurrencyDollar,
      color: 'green',
      description: `Last ${timeRangeValue}`,
    },
    {
      title: 'Active Incidents',
      value: 0, // TODO: Implement incidents tracking
      icon: IconAlertTriangle,
      color: 'green',
      description: `0 resolved`,
    },
  ];

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>System Performance</Title>
          <Text c="dimmed">Monitor LLM gateway performance, provider health, and cost analytics</Text>
        </div>

        <Group>
          <Select
            value={timeRangeValue}
            onChange={(value) => setTimeRangeValue(value || '24h')}
            data={[
              { value: '1h', label: 'Last Hour' },
              { value: '24h', label: 'Last 24 Hours' },
              { value: '7d', label: 'Last 7 Days' },
            ]}
            w={180}
          />
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
            loading={isLoading}
          >
            Refresh
          </Button>
        </Group>
      </Group>

      {/* Active Incidents Alert */}
      {healthIncidents?.activeIncidents?.length > 0 && (
        <Alert icon={<IconAlertCircle size={16} />} color="red" title="Active Incidents">
          <Stack gap="xs">
            {healthIncidents.activeIncidents.slice(0, 3).map((incident) => (
              <Group key={incident.id} justify="space-between">
                <Text size="sm">
                  <Text span fw={500}>{incident.provider}:</Text> {incident.description}
                </Text>
                <Badge color="red" variant="light" size="sm">
                  {new Date(incident.startTime).toLocaleTimeString()}
                </Badge>
              </Group>
            ))}
            {healthIncidents.activeIncidents.length > 3 && (
              <Text size="sm" c="dimmed">
                +{healthIncidents.activeIncidents.length - 3} more incidents
              </Text>
            )}
          </Stack>
        </Alert>
      )}

      {/* Performance Overview Cards */}
      <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="lg">
        {performanceCards.map((stat) => (
          <Card key={stat.title} p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                {stat.title}
              </Text>
              <ThemeIcon size="sm" variant="light" color={stat.color}>
                <stat.icon size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {stat.value}
            </Text>
            <Text size="xs" c="dimmed" mt={4}>
              {stat.description}
            </Text>
          </Card>
        ))}
      </SimpleGrid>

      {/* Performance Dashboard */}
      <Card>
        <Tabs value={selectedTab} onChange={(value) => setSelectedTab(value || 'overview')}>
          <Tabs.List>
            <Tabs.Tab value="overview">Overview</Tabs.Tab>
            <Tabs.Tab value="providers">Provider Health</Tabs.Tab>
            <Tabs.Tab value="performance">Request Performance</Tabs.Tab>
            <Tabs.Tab value="costs">Cost Analytics</Tabs.Tab>
            <Tabs.Tab value="incidents">Incidents</Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="overview" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                {/* Provider Health Summary */}
                <Card withBorder>
                  <Card.Section p="md" withBorder>
                    <Text fw={600}>Provider Health Summary</Text>
                  </Card.Section>
                  <Card.Section p="md">
                    <Center mb="md">
                      <RingProgress
                        size={140}
                        thickness={12}
                        sections={[
                          { value: overallHealth, color: overallHealth >= 90 ? 'green' : overallHealth >= 70 ? 'orange' : 'red' }
                        ]}
                        label={
                          <Text size="lg" ta="center" fw={700}>
                            {overallHealth.toFixed(0)}%<br />
                            <Text size="xs" c="dimmed" fw={400}>
                              Overall Health
                            </Text>
                          </Text>
                        }
                      />
                    </Center>
                    
                    <Stack gap="xs">
                      {providerHealth?.slice(0, 5).map((provider) => (
                        <Group key={provider.providerName} justify="space-between">
                          <Group gap="xs">
                            <ThemeIcon size="sm" color={getStatusColor(provider.status)} variant="light">
                              {provider.status === 'healthy' ? 
                                <IconCircleCheck size={12} /> : 
                                <IconAlertCircle size={12} />
                              }
                            </ThemeIcon>
                            <Text size="sm">{provider.providerName}</Text>
                          </Group>
                          <Group gap="xs">
                            <Text size="xs" c="dimmed">
                              {provider.responseTime}ms
                            </Text>
                            <Badge size="xs" color={getStatusColor(provider.status)} variant="light">
                              {provider.uptime.toFixed(0)}%
                            </Badge>
                          </Group>
                        </Group>
                      ))}
                    </Stack>
                  </Card.Section>
                </Card>

                {/* Cost Summary */}
                <Card withBorder>
                  <Card.Section p="md" withBorder>
                    <Text fw={600}>Cost Summary</Text>
                  </Card.Section>
                  <Card.Section p="md">
                    <Stack gap="md">
                      <div>
                        <Text size="xs" c="dimmed" mb={4}>Total Spend</Text>
                        <Text size="xl" fw={700}>{formatCurrency(costSummary?.totalSpend || 0)}</Text>
                        <Text size="xs" c="dimmed">Last {timeRangeValue}</Text>
                      </div>
                      
                      <SimpleGrid cols={2} spacing="md">
                        <div>
                          <Text size="xs" c="dimmed" mb={4}>Requests</Text>
                          <Text fw={500}>{formatNumber(costSummary?.totalRequests || 0)}</Text>
                        </div>
                        <div>
                          <Text size="xs" c="dimmed" mb={4}>Avg Cost/Request</Text>
                          <Text fw={500}>
                            {costSummary?.totalRequests ? 
                              formatCurrency((costSummary.totalSpend || 0) / costSummary.totalRequests) : 
                              '$0.00'
                            }
                          </Text>
                        </div>
                      </SimpleGrid>

                      {/* TODO: Add top models by cost when available from API */}
                    </Stack>
                  </Card.Section>
                </Card>
              </SimpleGrid>

              {/* Charts */}
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg" mt="lg">
                {/* TODO: Add health history chart */}
                
                {costTrends && (
                  <CostChart
                    data={costTrends.map(trend => ({
                      date: trend.date,
                      value: trend.spend,
                      name: trend.date
                    })) as ChartDataItem[]}
                    title="Daily Cost Trend"
                    type="bar"
                    valueKey="value"
                    nameKey="name"
                    timeKey="date"
                    onRefresh={handleRefresh}
                  />
                )}
              </SimpleGrid>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="providers" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={providerHealthLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Card withBorder>
                <Card.Section p="md" withBorder>
                  <Group justify="space-between">
                    <Text fw={600}>Provider Health Status</Text>
                    <Badge variant="light">
                      {providerHealth?.length || 0} providers
                    </Badge>
                  </Group>
                </Card.Section>
                <Card.Section>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Provider</Table.Th>
                        <Table.Th>Status</Table.Th>
                        <Table.Th>Uptime</Table.Th>
                        <Table.Th>Avg Response</Table.Th>
                        <Table.Th>Error Rate</Table.Th>
                        <Table.Th>Last Check</Table.Th>
                        <Table.Th>Actions</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {providerHealth?.map((provider) => (
                        <Table.Tr key={provider.providerName}>
                          <Table.Td>
                            <Text fw={500}>{provider.providerName}</Text>
                          </Table.Td>
                          <Table.Td>
                            <Badge color={getStatusColor(provider.status)} variant="light" size="sm">
                              {provider.status}
                            </Badge>
                          </Table.Td>
                          <Table.Td>
                            <Group gap="xs">
                              <Progress value={provider.uptime} size="sm" w={60} />
                              <Text size="sm">{provider.uptime.toFixed(0)}%</Text>
                            </Group>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm">{provider.responseTime}ms</Text>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm" c={provider.errorRate > 5 ? 'red' : undefined}>
                              {provider.errorRate.toFixed(2)}%
                            </Text>
                          </Table.Td>
                          <Table.Td>
                            <Text size="sm" c="dimmed">
                              {new Date(provider.lastChecked).toLocaleTimeString()}
                            </Text>
                          </Table.Td>
                          <Table.Td>
                            <Tooltip label="View details">
                              <ActionIcon size="sm" variant="light">
                                <IconExternalLink size={14} />
                              </ActionIcon>
                            </Tooltip>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Card.Section>
              </Card>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="performance" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                {/* TODO: Add performance charts when health history data is available */}
              </SimpleGrid>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="costs" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={costTrendsLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Stack gap="lg">
                {/* Cost by Model */}
                {/* TODO: Add cost by model table when available from API */}

                {/* Cost Charts */}
                <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
                  {costTrends && (
                    <>
                      <CostChart
                        data={costTrends.map(trend => ({
                          date: trend.date,
                          value: trend.spend,
                          name: trend.date
                        })) as ChartDataItem[]}
                        title="Cost Trend"
                        type="line"
                        valueKey="value"
                        nameKey="name"
                        timeKey="date"
                        onRefresh={handleRefresh}
                      />
                      
                      <CostChart
                        data={costTrends.map(trend => ({
                          date: trend.date,
                          value: trend.requests,
                          name: trend.date
                        })) as ChartDataItem[]}
                        title="Request Volume"
                        type="bar"
                        valueKey="value"
                        nameKey="name"
                        timeKey="date"
                        onRefresh={handleRefresh}
                      />
                    </>
                  )}
                </SimpleGrid>
              </Stack>
            </div>
          </Tabs.Panel>

          <Tabs.Panel value="incidents" pt="md">
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Stack gap="lg">
                {/* Active Incidents */}
                {healthIncidents?.activeIncidents?.length > 0 && (
                  <Card withBorder>
                    <Card.Section p="md" withBorder>
                      <Group justify="space-between">
                        <Text fw={600} c="red">Active Incidents</Text>
                        <Badge color="red" variant="light">
                          {healthIncidents.activeIncidents.length}
                        </Badge>
                      </Group>
                    </Card.Section>
                    <Card.Section p="md">
                      <Stack gap="md">
                        {healthIncidents.activeIncidents.map((incident) => (
                          <Card key={incident.id} withBorder p="sm">
                            <Group justify="space-between" mb="xs">
                              <Group gap="xs">
                                <ThemeIcon color="red" variant="light" size="sm">
                                  <IconAlertCircle size={16} />
                                </ThemeIcon>
                                <Text fw={500}>{incident.provider}</Text>
                              </Group>
                              <Badge color="red" variant="light" size="sm">
                                {incident.severity}
                              </Badge>
                            </Group>
                            <Text size="sm" mb="xs">{incident.description}</Text>
                            <Group gap="xs">
                              <IconClock size={14} />
                              <Text size="xs" c="dimmed">
                                Started {new Date(incident.startTime).toLocaleString()}
                              </Text>
                            </Group>
                          </Card>
                        ))}
                      </Stack>
                    </Card.Section>
                  </Card>
                )}

                {/* Recent Resolved Incidents */}
                <Card withBorder>
                  <Card.Section p="md" withBorder>
                    <Group justify="space-between">
                      <Text fw={600}>Recent Incidents</Text>
                      <Badge variant="light">
                        {healthIncidents?.resolvedIncidents?.length || 0} resolved
                      </Badge>
                    </Group>
                  </Card.Section>
                  <Card.Section>
                    <Table>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Provider</Table.Th>
                          <Table.Th>Severity</Table.Th>
                          <Table.Th>Description</Table.Th>
                          <Table.Th>Time</Table.Th>
                          <Table.Th>Status</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {healthIncidents?.resolvedIncidents?.slice(0, 10).map((incident) => (
                          <Table.Tr key={incident.id}>
                            <Table.Td>
                              <Text size="sm" fw={500}>{incident.provider}</Text>
                            </Table.Td>
                            <Table.Td>
                              <Badge color={incident.severity === 'high' ? 'red' : 'orange'} variant="light" size="sm">
                                {incident.severity}
                              </Badge>
                            </Table.Td>
                            <Table.Td>
                              <Text size="sm">{incident.description}</Text>
                            </Table.Td>
                            <Table.Td>
                              <Text size="sm" c="dimmed">
                                {new Date(incident.startTime).toLocaleString()}
                              </Text>
                            </Table.Td>
                            <Table.Td>
                              <Badge color="green" variant="light" size="sm">
                                Resolved
                              </Badge>
                            </Table.Td>
                          </Table.Tr>
                        ))}
                      </Table.Tbody>
                    </Table>
                  </Card.Section>
                </Card>
              </Stack>
            </div>
          </Tabs.Panel>
        </Tabs>
      </Card>
    </Stack>
  );
}