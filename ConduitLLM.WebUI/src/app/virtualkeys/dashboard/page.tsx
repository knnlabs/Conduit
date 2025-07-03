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
  RingProgress,
  Center,
  SimpleGrid,
} from '@mantine/core';
import {
  IconKey,
  IconTrendingUp,
  IconTrendingDown,
  IconActivity,
  IconCoin,
  IconClock,
  IconAlertCircle,
  IconRefresh,
  IconDownload,
  IconCalendar,
} from '@tabler/icons-react';
import { useState } from 'react';
import { AreaChart, Area, BarChart, Bar, PieChart, Pie, Cell, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Legend } from 'recharts';
import { DatePickerInput } from '@mantine/dates';
import { notifications } from '@mantine/notifications';
import { formatCurrency, formatNumber, formatPercent, formatRelativeTime } from '@/lib/utils/formatting';


const COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6'];

export default function VirtualKeysDashboardPage() {
  const [dateRange, setDateRange] = useState<[Date | null, Date | null]>([
    new Date(Date.now() - 30 * 24 * 60 * 60 * 1000),
    new Date(),
  ]);
  const [selectedPeriod, setSelectedPeriod] = useState('30d');
  const [isRefreshing, setIsRefreshing] = useState(false);

  // Generate mock data based on selected period
  const getDaysFromPeriod = (period: string) => {
    switch (period) {
      case '7d': return 7;
      case '30d': return 30;
      case '90d': return 90;
      default: return 30;
    }
  };

  const timeSeriesData = generateTimeSeriesData(getDaysFromPeriod(selectedPeriod));

  // Calculate summary statistics
  const totalRequests = mockVirtualKeys.reduce((sum, key) => sum + key.requests, 0);
  const totalCost = mockVirtualKeys.reduce((sum, key) => sum + key.cost, 0);
  const totalBudget = mockVirtualKeys.reduce((sum, key) => sum + key.budget, 0);
  const averageBudgetUsed = mockVirtualKeys.reduce((sum, key) => sum + key.budgetUsed, 0) / mockVirtualKeys.length;

  // Calculate growth rates (mock)
  const requestsGrowth = 12.5;
  const costGrowth = 8.3;
  const activeKeysGrowth = 5.0;

  const handleRefresh = () => {
    setIsRefreshing(true);
    setTimeout(() => {
      setIsRefreshing(false);
      notifications.show({
        title: 'Dashboard Refreshed',
        message: 'Virtual keys data has been updated',
        color: 'green',
      });
    }, 1000);
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
            onChange={(value) => setSelectedPeriod(value || '30d')}
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
            onClick={handleRefresh}
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
              {formatNumber(totalRequests)}
            </Text>
            <Group gap="xs" mt={4}>
              <Text size="xs" c="green" fw={500}>
                <IconTrendingUp size={14} style={{ verticalAlign: 'middle' }} /> {requestsGrowth}%
              </Text>
              <Text size="xs" c="dimmed">
                vs last period
              </Text>
            </Group>
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
              {formatCurrency(totalCost)}
            </Text>
            <Group gap="xs" mt={4}>
              <Text size="xs" c="green" fw={500}>
                <IconTrendingUp size={14} style={{ verticalAlign: 'middle' }} /> {costGrowth}%
              </Text>
              <Text size="xs" c="dimmed">
                vs last period
              </Text>
            </Group>
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
            <Group gap="xs" mt={4}>
              <Text size="xs" c="green" fw={500}>
                <IconTrendingUp size={14} style={{ verticalAlign: 'middle' }} /> {activeKeysGrowth}%
              </Text>
              <Text size="xs" c="dimmed">
                new this month
              </Text>
            </Group>
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
              {formatPercent(averageBudgetUsed)}
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
            <Area
              yAxisId="left"
              type="monotone"
              dataKey="requests"
              stroke="#3b82f6"
              fill="#3b82f6"
              fillOpacity={0.6}
              name="Requests"
            />
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
                  {mockVirtualKeys.map((key) => (
                    <Table.Tr key={key.id}>
                      <Table.Td>
                        <Group gap="xs">
                          <ThemeIcon size="xs" variant="light">
                            <IconKey size={12} />
                          </ThemeIcon>
                          <div>
                            <Text size="sm" fw={500}>{key.name}</Text>
                            <Text size="xs" c="dimmed">{key.id}</Text>
                          </div>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Badge variant="light" color={getStatusColor(key.status)}>
                          {key.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{formatNumber(key.requests)}</Table.Td>
                      <Table.Td>{formatCurrency(key.cost)}</Table.Td>
                      <Table.Td>{formatCurrency(key.budget)}</Table.Td>
                      <Table.Td>
                        <Stack gap={4}>
                          <Progress 
                            value={key.budgetUsed} 
                            color={getBudgetColor(key.budgetUsed)}
                            size="sm"
                          />
                          <Text size="xs" c="dimmed">{formatPercent(key.budgetUsed)}</Text>
                        </Stack>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Grid.Col>

        {/* Model Usage Distribution */}
        <Grid.Col span={{ base: 12, lg: 4 }}>
          <Card withBorder h="100%">
            <Text fw={600} mb="md">Model Usage Distribution</Text>
            <ResponsiveContainer width="100%" height={250}>
              <PieChart>
                <Pie
                  data={mockModelUsage}
                  cx="50%"
                  cy="50%"
                  labelLine={false}
                  label={(entry) => `${entry.model}: ${entry.percentage}%`}
                  outerRadius={80}
                  fill="#8884d8"
                  dataKey="percentage"
                >
                  {mockModelUsage.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                  ))}
                </Pie>
                <RechartsTooltip />
              </PieChart>
            </ResponsiveContainer>
            
            <Stack gap="xs" mt="md">
              {mockModelUsage.map((model, index) => (
                <Group key={model.model} justify="space-between">
                  <Group gap="xs">
                    <div
                      style={{
                        width: 12,
                        height: 12,
                        backgroundColor: COLORS[index % COLORS.length],
                        borderRadius: 2,
                      }}
                    />
                    <Text size="sm">{model.model}</Text>
                  </Group>
                  <Text size="sm" c="dimmed">
                    {formatNumber(model.requests)} requests
                  </Text>
                </Group>
              ))}
            </Stack>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Budget Alerts */}
      <Card withBorder>
        <Group justify="space-between" mb="md">
          <Text fw={600}>Budget Alerts</Text>
          <Badge variant="light" color="orange">
            {mockVirtualKeys.filter(k => k.budgetUsed > 75).length} warnings
          </Badge>
        </Group>
        <SimpleGrid cols={{ base: 1, md: 2, lg: 3 }}>
          {mockVirtualKeys
            .filter(k => k.budgetUsed > 50)
            .sort((a, b) => b.budgetUsed - a.budgetUsed)
            .map((key) => (
              <Paper key={key.id} p="md" withBorder>
                <Group justify="space-between" mb="xs">
                  <Text fw={500}>{key.name}</Text>
                  <Badge 
                    variant="light" 
                    color={getBudgetColor(key.budgetUsed)}
                  >
                    {formatPercent(key.budgetUsed)} used
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
                    {formatCurrency(key.cost)} / {formatCurrency(key.budget)}
                  </Text>
                  <Text size="xs" c="dimmed">
                    {formatCurrency(key.budget - key.cost)} remaining
                  </Text>
                </Group>
              </Paper>
            ))}
        </SimpleGrid>
      </Card>
    </Stack>
  );
}