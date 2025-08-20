'use client';

import {
  Grid,
  Card,
  Group,
  Text,
  ThemeIcon,
  Badge,
  Progress,
} from '@mantine/core';
import {
  IconCurrencyDollar,
  IconTrendingUp,
  IconTrendingDown,
  IconChartBar,
} from '@tabler/icons-react';

interface CostMetricsCardsProps {
  totalSpend: number;
  projectedTrend: number;
  averageDailyCost: number;
  projectedMonthlySpend: number;
  monthlyBudget: number | null;
  budgetUtilization: number | null;
  isOverBudget: boolean;
  timeRange: string;
}

export function CostMetricsCards({
  totalSpend,
  projectedTrend,
  averageDailyCost,
  projectedMonthlySpend,
  monthlyBudget,
  budgetUtilization,
  isOverBudget,
  timeRange,
}: CostMetricsCardsProps) {
  const getTimeRangeLabel = () => {
    if (timeRange === '7d') return 'Last 7 days';
    if (timeRange === '30d') return 'Last 30 days';
    return 'Last 90 days';
  };

  return (
    <Grid>
      <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
              Total Spend
            </Text>
            <ThemeIcon color="blue" variant="light" radius="md" size="md">
              <IconCurrencyDollar size={18} />
            </ThemeIcon>
          </Group>
          <Group align="baseline" gap="xs">
            <Text size="xl" fw={700}>
              ${totalSpend.toFixed(2)}
            </Text>
            {projectedTrend !== 0 && (
              <Badge
                color={projectedTrend > 0 ? 'red' : 'green'}
                variant="light"
                leftSection={
                  projectedTrend > 0 ? <IconTrendingUp size={12} /> : <IconTrendingDown size={12} />
                }
              >
                {Math.abs(projectedTrend).toFixed(1)}%
              </Badge>
            )}
          </Group>
          <Text size="xs" c="dimmed" mt="xs">
            {getTimeRangeLabel()}
          </Text>
        </Card>
      </Grid.Col>

      <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
              Daily Average
            </Text>
            <ThemeIcon color="teal" variant="light" radius="md" size="md">
              <IconChartBar size={18} />
            </ThemeIcon>
          </Group>
          <Text size="xl" fw={700}>
            ${averageDailyCost.toFixed(2)}
          </Text>
          <Text size="xs" c="dimmed" mt="xs">
            Per day average
          </Text>
        </Card>
      </Grid.Col>

      <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
              Projected Monthly
            </Text>
            <ThemeIcon color="orange" variant="light" radius="md" size="md">
              <IconTrendingUp size={18} />
            </ThemeIcon>
          </Group>
          <Text size="xl" fw={700}>
            ${projectedMonthlySpend.toFixed(2)}
          </Text>
          <Text size="xs" c="dimmed" mt="xs">
            Based on current usage
          </Text>
        </Card>
      </Grid.Col>

      <Grid.Col span={{ base: 12, md: 6, lg: 3 }}>
        <Card padding="lg" radius="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="sm" c="dimmed" tt="uppercase" fw={600}>
              Budget Status
            </Text>
            <ThemeIcon 
              color={isOverBudget ? 'red' : 'green'} 
              variant="light" 
              radius="md" 
              size="md"
            >
              <IconCurrencyDollar size={18} />
            </ThemeIcon>
          </Group>
          {monthlyBudget ? (
            <>
              <Text size="xl" fw={700}>
                {budgetUtilization?.toFixed(1)}%
              </Text>
              <Progress 
                value={budgetUtilization ?? 0} 
                color={isOverBudget ? 'red' : 'green'} 
                size="sm" 
                mt="xs" 
              />
            </>
          ) : (
            <>
              <Text size="xl" fw={700} c="dimmed">
                N/A
              </Text>
              <Text size="xs" c="dimmed" mt="xs">
                No budget set
              </Text>
            </>
          )}
        </Card>
      </Grid.Col>
    </Grid>
  );
}