'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Grid,
  SimpleGrid,
  Select,
  Badge,
  Progress,
  ThemeIcon,
  Paper,
  Table,
  ScrollArea,
  LoadingOverlay,
  Alert,
} from '@mantine/core';
import {
  IconCurrencyDollar,
  IconTrendingUp,
  IconTrendingDown,
  IconRefresh,
  IconDownload,
  IconAlertCircle,
  IconChartBar,
  IconCalendar,
  IconFilter,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';
import { CostChart } from '@/components/charts/CostChart';
import { ErrorDisplay } from '@/components/common/ErrorDisplay';
import { safeLog } from '@/lib/utils/logging';

interface ProviderCost {
  provider: string;
  cost: number;
  usage: number;
  trend: number;
}

interface ModelUsage {
  model: string;
  provider: string;
  requests: number;
  tokensIn: number;
  tokensOut: number;
  cost: number;
}

interface DailyCost {
  date: string;
  cost: number;
  providers: Record<string, number>;
}

export default function CostDashboard() {
  const [timeRange, setTimeRange] = useState('30d');
  const [selectedProvider, setSelectedProvider] = useState<string | null>('all');
  const [isLoading, setIsLoading] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  // Mock data - in a real app, this would come from an API
  const totalSpend = 2847.23;
  const monthlyBudget = 5000;
  const projectedSpend = 3412.50;
  const averageDailyCost = 94.91;

  const providerCosts: ProviderCost[] = [
    { provider: 'OpenAI', cost: 1423.12, usage: 65, trend: 12.5 },
    { provider: 'Anthropic', cost: 856.45, usage: 25, trend: -5.3 },
    { provider: 'Google', cost: 432.66, usage: 8, trend: 23.1 },
    { provider: 'Replicate', cost: 135.00, usage: 2, trend: 45.2 },
  ];

  const modelUsage: ModelUsage[] = [
    { model: 'gpt-4-turbo', provider: 'OpenAI', requests: 45320, tokensIn: 12500000, tokensOut: 8900000, cost: 892.34 },
    { model: 'gpt-3.5-turbo', provider: 'OpenAI', requests: 125430, tokensIn: 45000000, tokensOut: 32000000, cost: 530.78 },
    { model: 'claude-3-opus', provider: 'Anthropic', requests: 23450, tokensIn: 8900000, tokensOut: 6200000, cost: 656.23 },
    { model: 'claude-3-sonnet', provider: 'Anthropic', requests: 34200, tokensIn: 12000000, tokensOut: 9800000, cost: 200.22 },
    { model: 'gemini-pro', provider: 'Google', requests: 18900, tokensIn: 6700000, tokensOut: 4500000, cost: 432.66 },
    { model: 'stable-diffusion-xl', provider: 'Replicate', requests: 450, tokensIn: 0, tokensOut: 0, cost: 135.00 },
  ];

  const dailyCosts: DailyCost[] = Array.from({ length: 30 }, (_, i) => {
    const date = new Date();
    date.setDate(date.getDate() - (29 - i));
    const baseCost = 80 + Math.random() * 40;
    
    return {
      date: date.toISOString().split('T')[0],
      cost: baseCost,
      providers: {
        OpenAI: baseCost * 0.5,
        Anthropic: baseCost * 0.3,
        Google: baseCost * 0.15,
        Replicate: baseCost * 0.05,
      },
    };
  });

  const handleRefresh = () => {
    setIsLoading(true);
    // Simulate API call
    setTimeout(() => {
      setIsLoading(false);
      notifications.show({
        title: 'Data Refreshed',
        message: 'Cost data has been updated',
        color: 'green',
      });
    }, 1000);
  };

  const handleExport = async () => {
    setIsExporting(true);
    try {
      // Simulate export functionality
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      notifications.show({
        title: 'Export Successful',
        message: 'Cost report has been exported',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Export Failed',
        message: 'Failed to export cost report',
        color: 'red',
      });
    } finally {
      setIsExporting(false);
    }
  };

  const filteredData = selectedProvider === 'all' 
    ? dailyCosts 
    : dailyCosts.map(day => ({
        ...day,
        cost: selectedProvider ? (day.providers[selectedProvider] || 0) : 0,
      }));

  const budgetUsagePercent = (totalSpend / monthlyBudget) * 100;
  const budgetStatusColor = budgetUsagePercent > 90 ? 'red' : budgetUsagePercent > 70 ? 'yellow' : 'green';

  if (error) {
    return <ErrorDisplay error={error} title="Failed to load cost data" variant="card" onRetry={() => window.location.reload()} />;
  }

  return (
    <Stack>
      <Card shadow="sm" p="md" radius="md">
        <Group justify="space-between" align="center">
          <div>
            <Title order={2}>Cost Dashboard</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Monitor and analyze your API usage costs
            </Text>
          </div>
          <Group>
            <Select
              value={timeRange}
              onChange={(value) => setTimeRange(value || '30d')}
              data={[
                { value: '7d', label: 'Last 7 Days' },
                { value: '30d', label: 'Last 30 Days' },
                { value: '90d', label: 'Last 90 Days' },
                { value: 'ytd', label: 'Year to Date' },
              ]}
              w={150}
            />
            <Button
              variant="light"
              leftSection={<IconRefresh size={16} />}
              onClick={handleRefresh}
              loading={isLoading}
            >
              Refresh
            </Button>
            <Button
              variant="filled"
              leftSection={<IconDownload size={16} />}
              onClick={handleExport}
              loading={isExporting}
            >
              Export
            </Button>
          </Group>
        </Group>
      </Card>

      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Total Spend
                </Text>
                <Text size="xl" fw={700} mt={4}>
                  ${totalSpend.toFixed(2)}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  This month
                </Text>
              </div>
              <ThemeIcon
                color="blue"
                variant="light"
                size={48}
                radius="md"
              >
                <IconCurrencyDollar size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Budget Usage
                </Text>
                <Text size="xl" fw={700} mt={4}>
                  {budgetUsagePercent.toFixed(1)}%
                </Text>
                <Progress
                  value={budgetUsagePercent}
                  color={budgetStatusColor}
                  size="sm"
                  radius="md"
                  mt={8}
                />
              </div>
              <ThemeIcon
                color={budgetStatusColor}
                variant="light"
                size={48}
                radius="md"
              >
                <IconChartBar size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Projected Spend
                </Text>
                <Text size="xl" fw={700} mt={4}>
                  ${projectedSpend.toFixed(2)}
                </Text>
                <Group gap={4} mt={8}>
                  <IconTrendingUp size={16} color="var(--mantine-color-yellow-6)" />
                  <Text size="xs" c="yellow">
                    +20% from last month
                  </Text>
                </Group>
              </div>
              <ThemeIcon
                color="yellow"
                variant="light"
                size={48}
                radius="md"
              >
                <IconTrendingUp size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, lg: 3 }}>
          <Card padding="lg" radius="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="sm" c="dimmed" fw={600} tt="uppercase">
                  Avg Daily Cost
                </Text>
                <Text size="xl" fw={700} mt={4}>
                  ${averageDailyCost.toFixed(2)}
                </Text>
                <Text size="xs" c="dimmed" mt={4}>
                  Last 30 days
                </Text>
              </div>
              <ThemeIcon
                color="green"
                variant="light"
                size={48}
                radius="md"
              >
                <IconCalendar size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>
      </Grid>

      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" mb="md">
          <Title order={4}>Cost Trends</Title>
          <Select
            value={selectedProvider}
            onChange={setSelectedProvider}
            data={[
              { value: 'all', label: 'All Providers' },
              ...providerCosts.map(p => ({ value: p.provider, label: p.provider })),
            ]}
            w={180}
            leftSection={<IconFilter size={16} />}
          />
        </Group>
        <CostChart 
          data={filteredData.map(day => ({
            date: day.date,
            cost: day.cost,
            ...day.providers
          }))} 
          height={300} 
          title="Daily Cost Trends"
          type="line"
          valueKey="cost"
          nameKey="date"
        />
      </Card>

      <Grid>
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card shadow="sm" p="md" radius="md" withBorder h="100%">
            <Title order={4} mb="md">Provider Breakdown</Title>
            <Stack gap="sm">
              {providerCosts.map((provider) => (
                <Paper key={provider.provider} p="sm" withBorder>
                  <Group justify="space-between" mb="xs">
                    <Group>
                      <Text fw={500}>{provider.provider}</Text>
                      <Badge
                        variant="light"
                        color={provider.trend > 0 ? 'red' : 'green'}
                        leftSection={provider.trend > 0 ? <IconTrendingUp size={12} /> : <IconTrendingDown size={12} />}
                      >
                        {Math.abs(provider.trend)}%
                      </Badge>
                    </Group>
                    <Text fw={600}>${provider.cost.toFixed(2)}</Text>
                  </Group>
                  <Progress
                    value={provider.usage}
                    color="blue"
                    size="sm"
                    radius="md"
                  />
                  <Text size="xs" c="dimmed" mt={4}>
                    {provider.usage}% of total usage
                  </Text>
                </Paper>
              ))}
            </Stack>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card shadow="sm" p="md" radius="md" withBorder h="100%">
            <Title order={4} mb="md">Top Models by Cost</Title>
            <ScrollArea h={300}>
              <Table striped>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Model</Table.Th>
                    <Table.Th>Requests</Table.Th>
                    <Table.Th>Cost</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {modelUsage
                    .sort((a, b) => b.cost - a.cost)
                    .slice(0, 6)
                    .map((model) => (
                      <Table.Tr key={model.model}>
                        <Table.Td>
                          <div>
                            <Text size="sm" fw={500}>{model.model}</Text>
                            <Text size="xs" c="dimmed">{model.provider}</Text>
                          </div>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm">{model.requests.toLocaleString()}</Text>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm" fw={600}>${model.cost.toFixed(2)}</Text>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          </Card>
        </Grid.Col>
      </Grid>

      {projectedSpend > monthlyBudget && (
        <Alert
          icon={<IconAlertCircle size={16} />}
          title="Budget Alert"
          color="red"
          variant="light"
        >
          Your projected spend (${projectedSpend.toFixed(2)}) exceeds your monthly budget (${monthlyBudget.toFixed(2)}). 
          Consider reviewing your usage patterns to avoid overage charges.
        </Alert>
      )}

      <LoadingOverlay visible={isLoading} />
    </Stack>
  );
}