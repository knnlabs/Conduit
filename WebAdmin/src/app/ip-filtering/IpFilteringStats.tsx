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
  IconShield,
  IconShieldCheck,
  IconShieldX,
  IconClock,
} from '@tabler/icons-react';
import { type IpStats } from '@/hooks/useSecurityApi';
import { StatCard } from './types';

interface IpFilteringStatsProps {
  stats: IpStats | null;
  isLoading?: boolean;
}

export function IpFilteringStats({ stats, isLoading = false }: IpFilteringStatsProps) {
  const statCards: StatCard[] = [
    {
      title: 'Total Rules',
      value: stats?.totalRules ?? 0,
      description: 'Active IP filtering rules',
      icon: IconShield,
      color: 'blue',
    },
    {
      title: 'Allow Rules',
      value: stats?.allowRules ?? 0,
      description: 'Whitelisted IPs',
      icon: IconShieldCheck,
      color: 'green',
    },
    {
      title: 'Block Rules',
      value: stats?.blockRules ?? 0,
      description: 'Blacklisted IPs',
      icon: IconShieldX,
      color: 'red',
    },
    {
      title: 'Blocked Today',
      value: stats?.blockedRequests24h ?? 0,
      description: 'Requests blocked in 24h',
      icon: IconClock,
      color: 'orange',
    },
  ];

  if (isLoading) {
    return (
      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
        {Array.from({ length: 4 }).map((item, index) => (
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
                  <IconShield size={20} />
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