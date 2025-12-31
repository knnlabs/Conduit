'use client';

import {
  SimpleGrid,
  Card,
  Stack,
  Group,
  Text,
  ThemeIcon,
} from '@mantine/core';
import {
  IconShieldOff,
  IconBan,
  IconAlertTriangle,
  IconActivity,
} from '@tabler/icons-react';
import type { QuickStats } from './types';

interface QuickStatsCardsProps {
  stats: QuickStats;
  isLoading?: boolean;
}

interface StatCardConfig {
  title: string;
  value: number;
  description: string;
  icon: typeof IconShieldOff;
  color: string;
}

export function QuickStatsCards({ stats, isLoading = false }: QuickStatsCardsProps) {
  const statCards: StatCardConfig[] = [
    {
      title: 'Failed Auth (24h)',
      value: stats.failedAuthAttempts24h,
      description: 'Authentication failures',
      icon: IconShieldOff,
      color: 'red',
    },
    {
      title: 'Blocked IPs',
      value: stats.blockedIpsCount,
      description: 'Suspicious activity detected',
      icon: IconBan,
      color: 'orange',
    },
    {
      title: 'Rate Limit Hits',
      value: stats.rateLimitViolations,
      description: 'Violations in 24h',
      icon: IconAlertTriangle,
      color: 'yellow',
    },
    {
      title: 'Suspicious Activity',
      value: stats.suspiciousActivityCount,
      description: 'Events in 24h',
      icon: IconActivity,
      color: 'violet',
    },
  ];

  if (isLoading) {
    return (
      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
        {[...Array(4).keys()].map((index) => (
          <Card key={index} padding="lg" radius="md" withBorder>
            <Stack gap="md">
              <Group justify="space-between" align="flex-start">
                <Stack gap={4} style={{ flex: 1 }}>
                  <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                    Loading...
                  </Text>
                  <Text fw={700} size="xl" lh={1}>
                    --
                  </Text>
                </Stack>
                <ThemeIcon color="gray" variant="light" size={40} radius="md">
                  <IconActivity size={20} />
                </ThemeIcon>
              </Group>
              <Text size="xs" c="dimmed" lh={1.2}>
                Loading statistics...
              </Text>
            </Stack>
          </Card>
        ))}
      </SimpleGrid>
    );
  }

  return (
    <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
      {statCards.map((stat) => (
        <Card key={stat.title} padding="lg" radius="md" withBorder>
          <Stack gap="md">
            <Group justify="space-between" align="flex-start">
              <Stack gap={4} style={{ flex: 1 }}>
                <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                  {stat.title}
                </Text>
                <Text fw={700} size="xl" lh={1}>
                  {stat.value.toLocaleString()}
                </Text>
              </Stack>
              <ThemeIcon color={stat.color} variant="light" size={40} radius="md">
                <stat.icon size={20} />
              </ThemeIcon>
            </Group>
            <Text size="xs" c="dimmed" lh={1.2}>
              {stat.description}
            </Text>
          </Stack>
        </Card>
      ))}
    </SimpleGrid>
  );
}
