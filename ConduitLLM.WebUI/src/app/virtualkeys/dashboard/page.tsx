'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Grid,
  Group,
  Button,
  Select,
  ThemeIcon,
  Progress,
  Badge,
  Table,
  ScrollArea,
  Paper,
  SimpleGrid,
  LoadingOverlay,
} from '@mantine/core';
import {
  IconKey,
  IconTrendingUp,
  IconActivity,
  IconCoin,
  IconAlertCircle,
  IconRefresh,
  IconDownload,
} from '@tabler/icons-react';
import { useState } from 'react';
import { AreaChart, Area, PieChart, Pie, Cell, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Legend } from 'recharts';
// Removed unused DatePickerInput
import { notifications } from '@mantine/notifications';
import { formatters } from '@/lib/utils/formatters';
import { useQuery } from '@tanstack/react-query';
import type { VirtualKeyDto } from '@knn_labs/conduit-admin-client';


const COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6'];

// Use SDK types directly for Virtual Keys
interface DashboardVirtualKey extends VirtualKeyDto {
  status: string;
  budgetUsed: number;
}

interface ModelUsage {
  name: string;
  value: number;
  percentage: number;
  model: string;
}

interface SummaryData {
  totalRequests: number;
  totalCost: number;
  averageBudgetUsed: number;
  requestsGrowth: number;
  costGrowth: number;
  activeKeysGrowth: number;
  activeKeys: number;
}

interface TimeSeriesPoint {
  date: string;
  requests: number | null;
  cost: number;
}

interface DashboardData {
  virtualKeys: DashboardVirtualKey[];
  summary: SummaryData;
  timeSeriesData: TimeSeriesPoint[];
  modelUsage: ModelUsage[];
}

export default function VirtualKeysDashboardPage() {
  // Removed unused date range state variables
  const [selectedPeriod, setSelectedPeriod] = useState('30d');
  const [isRefreshing, setIsRefreshing] = useState(false);

  // Fetch real virtual keys data from the API
  const { data: dashboardData, isLoading, error, refetch } = useQuery<DashboardData>({
    queryKey: ['virtual-keys-dashboard', selectedPeriod],
    queryFn: async () => {
      const response = await fetch(`/api/virtualkeys/dashboard?period=${selectedPeriod}`);
      if (!response.ok) {
        throw new Error('Failed to fetch virtual keys data');
      }
      return response.json() as Promise<DashboardData>;
    },
    refetchInterval: 300000, // Refresh every 5 minutes
  });

  // Use real data from API or fallback values
  const virtualKeys: DashboardVirtualKey[] = dashboardData?.virtualKeys ?? [];
  const totalRequests = dashboardData?.summary?.totalRequests ?? 0;
  const totalCost = dashboardData?.summary?.totalCost ?? 0;
  const averageBudgetUsed = dashboardData?.summary?.averageBudgetUsed ?? 0;
  const requestsGrowth = dashboardData?.summary?.requestsGrowth ?? 0;
  const costGrowth = dashboardData?.summary?.costGrowth ?? 0;
  const activeKeysGrowth = dashboardData?.summary?.activeKeysGrowth ?? 0;
  const activeKeys = dashboardData?.summary?.activeKeys ?? 0;
  const timeSeriesData: TimeSeriesPoint[] = dashboardData?.timeSeriesData ?? [];
  const modelUsage: ModelUsage[] = dashboardData?.modelUsage ?? [];

  const handleRefresh = async () => {
    setIsRefreshing(true);
    try {
      await refetch();
      notifications.show({
        title: 'Dashboard Refreshed',
        message: 'Virtual keys data has been updated',
        color: 'green',
      });
    } catch {
      notifications.show({
        title: 'Refresh Failed',
        message: 'Failed to refresh virtual keys data',
        color: 'red',
      });
    } finally {
      setIsRefreshing(false);
    }
  };

  const handleExport = () => {
    notifications.show({
      title: 'Export Started',
      message: 'Preparing your virtual keys report...',
      color: 'blue',
    });
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'active': return 'green';
      case 'warning': return 'yellow';
      case 'inactive': return 'gray';
      case 'error': return 'red';
      default: return 'gray';
    }
  };

  const getBudgetColor = (percentage: number) => {
    if (percentage >= 90) return 'red';
    if (percentage >= 75) return 'orange';
    if (percentage >= 50) return 'yellow';
    return 'green';
  };

  if (error) {
    return (
      <Card withBorder p="xl">
        <Stack align="center" gap="md">
          <ThemeIcon size="xl" color="red" variant="light">
            <IconAlertCircle size={32} />
          </ThemeIcon>
          <Title order={4}>Failed to load virtual keys data</Title>
          <Text c="dimmed">Please try refreshing the page</Text>
          <Button onClick={() => window.location.reload()}>Refresh Page</Button>
        </Stack>
      </Card>
    );
  }

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Virtual Keys Dashboard</Title>
          <Text c="dimmed">Overview of all virtual keys performance and usage</Text>
        </div>
        <Group>
          <Select
            value={selectedPeriod}
            onChange={(value) => setSelectedPeriod(value ?? '30d')}
            data={[
              { value: '7d', label: 'Last 7 days' },
              { value: '30d', label: 'Last 30 days' },
              { value: '90d', label: 'Last 90 days' },
            ]}
            w={150}
          />
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={() => void handleRefresh()}
            loading={isRefreshing}
          >
            Refresh
          </Button>
          <Button
            leftSection={<IconDownload size={16} />}
            onClick={handleExport}
          >
            Export Report
          </Button>
        </Group>
      </Group>

      {/* Summary Cards */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between" mb="xs">
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                Total Requests
              </Text>
              <ThemeIcon size="sm" variant="light" radius="xl">
                <IconActivity size={16} />
              </ThemeIcon>
            </Group>
            <Text size="xl" fw={700}>
              {formatters.number(totalRequests)}
            </Text>
            {requestsGrowth !== null && (
              <Group gap="xs" mt={4}>
                <Text size="xs" c="green" fw={500}>
                  <IconTrendingUp size={14} style={{ verticalAlign: 'middle' }} /> {requestsGrowth}%
                </Text>
                <Text size="xs" c="dimmed">
                  vs last period
                </Text>
              </Group>
            )}
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between" mb="xs">
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                Total Cost
              </Text>
              <ThemeIcon size="sm" variant="light" radius="xl" color="green">
                <IconCoin size={16} />
              </ThemeIcon>
            </Group>
            <Text size="xl" fw={700}>
              {formatters.currency(totalCost)}
            </Text>
            {costGrowth !== null && (
              <Group gap="xs" mt={4}>
                <Text size="xs" c="green" fw={500}>
                  <IconTrendingUp size={14} style={{ verticalAlign: 'middle' }} /> {costGrowth}%
                </Text>
                <Text size="xs" c="dimmed">
                  vs last period
                </Text>
              </Group>
            )}
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between" mb="xs">
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                Active Keys
              </Text>
              <ThemeIcon size="sm" variant="light" radius="xl" color="blue">
                <IconKey size={16} />
              </ThemeIcon>
            </Group>
            <Text size="xl" fw={700}>
              {activeKeys}
            </Text>
            {activeKeysGrowth !== null && (
              <Group gap="xs" mt={4}>
                <Text size="xs" c="green" fw={500}>
                  <IconTrendingUp size={14} style={{ verticalAlign: 'middle' }} /> {activeKeysGrowth}%
                </Text>
                <Text size="xs" c="dimmed">
                  new this month
                </Text>
              </Group>
            )}
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between" mb="xs">
              <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                Budget Usage
              </Text>
              <ThemeIcon size="sm" variant="light" radius="xl" color="orange">
                <IconAlertCircle size={16} />
              </ThemeIcon>
            </Group>
            <Text size="xl" fw={700}>
              {formatters.percentage(averageBudgetUsed)}
            </Text>
            <Text size="xs" c="dimmed" mt={4}>
              Average across all keys
            </Text>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Usage Trends Chart */}
      <Card withBorder>
        <Text fw={600} mb="md">Usage Trends</Text>
        <ResponsiveContainer width="100%" height={300}>
          <AreaChart data={timeSeriesData}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="date" />
            <YAxis yAxisId="left" />
            <YAxis yAxisId="right" orientation="right" />
            <RechartsTooltip />
            <Legend />
            {/* Only show requests if data is available */}
            {timeSeriesData.some((d) => d.requests !== null) && (
              <Area
                yAxisId="left"
                type="monotone"
                dataKey="requests"
                stroke="#3b82f6"
                fill="#3b82f6"
                fillOpacity={0.6}
                name="Requests"
              />
            )}
            <Area
              yAxisId="right"
              type="monotone"
              dataKey="cost"
              stroke="#10b981"
              fill="#10b981"
              fillOpacity={0.6}
              name="Cost ($)"
            />
          </AreaChart>
        </ResponsiveContainer>
      </Card>

      <Grid>
        {/* Virtual Keys Performance Table */}
        <Grid.Col span={{ base: 12, lg: 8 }}>
          <Card withBorder h="100%">
            <Group justify="space-between" mb="md">
              <Text fw={600}>Virtual Keys Performance</Text>
              <Badge variant="light">{selectedPeriod}</Badge>
            </Group>
            <ScrollArea>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Key Name</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Requests</Table.Th>
                    <Table.Th>Cost</Table.Th>
                    <Table.Th>Budget</Table.Th>
                    <Table.Th>Usage</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {virtualKeys.length > 0 ? (
                    virtualKeys.map((key) => (
                      <Table.Tr key={key.id}>
                        <Table.Td>
                          <Group gap="xs">
                            <ThemeIcon size="xs" variant="light">
                              <IconKey size={12} />
                            </ThemeIcon>
                            <div>
                              <Text size="sm" fw={500}>{key.keyName}</Text>
                              <Text size="xs" c="dimmed">{key.id}</Text>
                            </div>
                          </Group>
                        </Table.Td>
                        <Table.Td>
                          <Badge variant="light" color={getStatusColor(key.status)}>
                            {key.status}
                          </Badge>
                        </Table.Td>
                        <Table.Td>{formatters.number(key.requestCount ?? 0)}</Table.Td>
                        <Table.Td>{formatters.currency(key.currentSpend)}</Table.Td>
                        <Table.Td>{formatters.currency(key.maxBudget || 0)}</Table.Td>
                        <Table.Td>
                          <Stack gap={4}>
                            <Progress 
                              value={key.budgetUsed} 
                              color={getBudgetColor(key.budgetUsed)}
                              size="sm"
                            />
                            <Text size="xs" c="dimmed">{formatters.percentage(key.budgetUsed)}</Text>
                          </Stack>
                        </Table.Td>
                      </Table.Tr>
                    ))
                  ) : (
                    <Table.Tr>
                      <Table.Td colSpan={6}>
                        <Text c="dimmed" ta="center" py="xl">
                          No virtual keys found
                        </Text>
                      </Table.Td>
                    </Table.Tr>
                  )}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Grid.Col>

        {/* Model Usage Distribution */}
        <Grid.Col span={{ base: 12, lg: 4 }}>
          <Card withBorder h="100%">
            <Text fw={600} mb="md">Model Usage Distribution</Text>
            {modelUsage.length > 0 ? (
              <>
                <ResponsiveContainer width="100%" height={250}>
                  <PieChart>
                    <Pie
                      data={modelUsage}
                      cx="50%"
                      cy="50%"
                      labelLine={false}
                      label={(entry: ModelUsage) => `${entry.model}: ${entry.percentage}%`}
                      outerRadius={80}
                      fill="#8884d8"
                      dataKey="percentage"
                    >
                      {modelUsage.map((entry, index) => (
                        <Cell key={`cell-${entry.name}`} fill={COLORS[index % COLORS.length]} />
                      ))}
                    </Pie>
                    <RechartsTooltip />
                  </PieChart>
                </ResponsiveContainer>
                
                <Stack gap="xs" mt="md">
                  {modelUsage.map((model, index) => (
                    <Group key={model.name} justify="space-between">
                      <Group gap="xs">
                        <div
                          style={{
                            width: 12,
                            height: 12,
                            backgroundColor: COLORS[index % COLORS.length],
                            borderRadius: 2,
                          }}
                        />
                        <Text size="sm">{model.name}</Text>
                      </Group>
                      <Text size="sm" c="dimmed">
                        {formatters.number(model.value)} requests
                      </Text>
                    </Group>
                  ))}
                </Stack>
              </>
            ) : (
              <Text c="dimmed" ta="center" mt="xl">
                No model usage data available
              </Text>
            )}
          </Card>
        </Grid.Col>
      </Grid>

      {/* Budget Alerts */}
      <Card withBorder>
        <Group justify="space-between" mb="md">
          <Text fw={600}>Budget Alerts</Text>
          <Badge variant="light" color="orange">
            {virtualKeys.filter(k => k.budgetUsed > 75).length} warnings
          </Badge>
        </Group>
        <SimpleGrid cols={{ base: 1, md: 2, lg: 3 }}>
          {virtualKeys.length > 0 ? (
            virtualKeys
              .filter(k => k.budgetUsed > 50)
              .sort((a, b) => b.budgetUsed - a.budgetUsed)
              .map((key) => (
                <Paper key={key.id} p="md" withBorder>
                  <Group justify="space-between" mb="xs">
                    <Text fw={500}>{key.keyName}</Text>
                    <Badge 
                      variant="light" 
                      color={getBudgetColor(key.budgetUsed)}
                    >
                      {formatters.percentage(key.budgetUsed)} used
                    </Badge>
                  </Group>
                  <Progress 
                    value={key.budgetUsed} 
                    color={getBudgetColor(key.budgetUsed)}
                    size="lg"
                    mb="xs"
                  />
                  <Group justify="space-between">
                    <Text size="sm" c="dimmed">
                      {formatters.currency(key.currentSpend)} / {formatters.currency(key.maxBudget || 0)}
                    </Text>
                    <Text size="xs" c="dimmed">
                      {formatters.currency((key.maxBudget || 0) - key.currentSpend)} remaining
                    </Text>
                  </Group>
                </Paper>
              ))
          ) : (
            <Text c="dimmed" ta="center" py="xl" style={{ gridColumn: '1 / -1' }}>
              No budget alerts at this time
            </Text>
          )}
        </SimpleGrid>
      </Card>
      
      <LoadingOverlay visible={isLoading || isRefreshing} />
    </Stack>
  );
}