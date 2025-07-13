'use client';

import {
  Card,
  SimpleGrid,
  Group,
  Text,
  Badge,
  Progress,
  Stack,
  RingProgress,
  Center,
} from '@mantine/core';
import {
  IconServer,
  IconActivity,
  IconClock,
  IconChartBar,
  IconToggleLeft,
  IconToggleRight,
} from '@tabler/icons-react';

interface ProviderDisplay {
  providerId: string;
  providerName: string;
  priority: number;
  weight?: number;
  isEnabled: boolean;
  statistics: {
    usagePercentage: number;
    successRate: number;
    avgResponseTime: number;
  };
  type: 'primary' | 'backup' | 'special';
}

interface ProviderStatsProps {
  providers: ProviderDisplay[];
}

export function ProviderStats({ providers }: ProviderStatsProps) {
  const enabledProviders = providers.filter(p => p.isEnabled);
  const disabledProviders = providers.filter(p => !p.isEnabled);
  
  const totalUsage = providers.reduce((sum, p) => sum + p.statistics.usagePercentage, 0);
  const avgSuccessRate = providers.length > 0 
    ? providers.reduce((sum, p) => sum + p.statistics.successRate, 0) / providers.length 
    : 0;
  const avgResponseTime = providers.length > 0 
    ? providers.reduce((sum, p) => sum + p.statistics.avgResponseTime, 0) / providers.length 
    : 0;

  const providersByType = providers.reduce((acc, p) => {
    acc[p.type] = (acc[p.type] || 0) + 1;
    return acc;
  }, {} as Record<string, number>);

  const topProviders = providers
    .filter(p => p.isEnabled)
    .sort((a, b) => b.statistics.usagePercentage - a.statistics.usagePercentage)
    .slice(0, 3);

  const getSuccessRateColor = (rate: number) => {
    if (rate >= 95) return 'green';
    if (rate >= 85) return 'yellow';
    return 'red';
  };

  const getResponseTimeColor = (time: number) => {
    if (time <= 300) return 'green';
    if (time <= 500) return 'yellow';
    return 'red';
  };

  return (
    <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="md">
      {/* Provider Count */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" align="flex-start">
          <div>
            <Text size="xs" c="dimmed" fw={700} style={{ textTransform: 'uppercase' }}>
              Total Providers
            </Text>
            <Text fw={700} size="xl">
              {providers.length}
            </Text>
            <Group gap={4} mt="xs">
              <Group gap={4}>
                <IconToggleRight size={12} color="green" />
                <Text size="xs" c="green">{enabledProviders.length} enabled</Text>
              </Group>
              {disabledProviders.length > 0 && (
                <Group gap={4}>
                  <IconToggleLeft size={12} color="gray" />
                  <Text size="xs" c="gray">{disabledProviders.length} disabled</Text>
                </Group>
              )}
            </Group>
          </div>
          <IconServer size={22} color="blue" />
        </Group>
      </Card>

      {/* Success Rate */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" align="flex-start">
          <div style={{ flex: 1 }}>
            <Text size="xs" c="dimmed" fw={700} style={{ textTransform: 'uppercase' }}>
              Avg Success Rate
            </Text>
            <Text fw={700} size="xl">
              {avgSuccessRate.toFixed(1)}%
            </Text>
            <Progress
              value={avgSuccessRate}
              color={getSuccessRateColor(avgSuccessRate)}
              size="sm"
              mt="xs"
            />
          </div>
          <IconActivity size={22} color={getSuccessRateColor(avgSuccessRate)} />
        </Group>
      </Card>

      {/* Response Time */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" align="flex-start">
          <div>
            <Text size="xs" c="dimmed" fw={700} style={{ textTransform: 'uppercase' }}>
              Avg Response Time
            </Text>
            <Text fw={700} size="xl">
              {avgResponseTime.toFixed(0)}ms
            </Text>
            <Badge 
              variant="light" 
              color={getResponseTimeColor(avgResponseTime)}
              size="sm"
              mt="xs"
            >
              {avgResponseTime <= 300 ? 'Excellent' : 
               avgResponseTime <= 500 ? 'Good' : 'Needs Attention'}
            </Badge>
          </div>
          <IconClock size={22} color={getResponseTimeColor(avgResponseTime)} />
        </Group>
      </Card>

      {/* Provider Distribution */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" align="flex-start">
          <div style={{ flex: 1 }}>
            <Text size="xs" c="dimmed" fw={700} style={{ textTransform: 'uppercase' }}>
              Provider Types
            </Text>
            <Stack gap="xs" mt="xs">
              {Object.entries(providersByType).map(([type, count]) => (
                <Group key={type} justify="space-between">
                  <Badge 
                    variant="light" 
                    color={type === 'primary' ? 'blue' : type === 'backup' ? 'orange' : 'green'}
                    size="sm"
                  >
                    {type}
                  </Badge>
                  <Text size="sm" fw={500}>{count}</Text>
                </Group>
              ))}
            </Stack>
          </div>
          <IconChartBar size={22} color="violet" />
        </Group>
      </Card>

      {/* Top Providers by Usage */}
      {topProviders.length > 0 && (
        <Card shadow="sm" p="md" radius="md" withBorder style={{ gridColumn: '1 / -1' }}>
          <Text size="sm" fw={700} mb="md">Top Providers by Usage</Text>
          <SimpleGrid cols={{ base: 1, sm: 2, md: 3 }} spacing="md">
            {topProviders.map((provider, index) => (
              <Card key={provider.providerId} p="sm" withBorder>
                <Group justify="space-between" align="center" mb="xs">
                  <div>
                    <Text fw={500} size="sm">{provider.providerName}</Text>
                    <Badge 
                      variant="light" 
                      color={index === 0 ? 'gold' : index === 1 ? 'gray' : 'orange'}
                      size="xs"
                    >
                      #{index + 1}
                    </Badge>
                  </div>
                  <RingProgress
                    size={50}
                    thickness={4}
                    sections={[
                      { value: provider.statistics.usagePercentage, color: 'blue' },
                    ]}
                    label={
                      <Center>
                        <Text size="xs" fw={700}>
                          {provider.statistics.usagePercentage.toFixed(0)}%
                        </Text>
                      </Center>
                    }
                  />
                </Group>
                <Group justify="space-between">
                  <Text size="xs" c="dimmed">
                    Success: {provider.statistics.successRate.toFixed(1)}%
                  </Text>
                  <Text size="xs" c="dimmed">
                    {provider.statistics.avgResponseTime.toFixed(0)}ms
                  </Text>
                </Group>
              </Card>
            ))}
          </SimpleGrid>
        </Card>
      )}
    </SimpleGrid>
  );
}