'use client';

import {
  Card,
  Group,
  Stack,
  Text,
  Badge,
  ThemeIcon,
  Skeleton,
} from '@mantine/core';
import {
  IconShieldCheck,
  IconShieldExclamation,
  IconAlertTriangle,
} from '@tabler/icons-react';
import type { SecurityOverview } from './types';

interface SecurityOverviewCardProps {
  overview: SecurityOverview;
  isLoading?: boolean;
}

type ThreatLevel = SecurityOverview['threatLevel'];

interface ThreatLevelConfig {
  color: string;
  label: string;
  icon: typeof IconShieldCheck;
}

const THREAT_LEVEL_CONFIG: Record<ThreatLevel, ThreatLevelConfig> = {
  low: { color: 'green', label: 'Low', icon: IconShieldCheck },
  medium: { color: 'yellow', label: 'Medium', icon: IconShieldExclamation },
  high: { color: 'orange', label: 'High', icon: IconAlertTriangle },
  critical: { color: 'red', label: 'Critical', icon: IconAlertTriangle },
};

export function SecurityOverviewCard({
  overview,
  isLoading = false,
}: SecurityOverviewCardProps) {
  const threatConfig = THREAT_LEVEL_CONFIG[overview.threatLevel];
  const ThreatIcon = threatConfig.icon;

  if (isLoading) {
    return (
      <Card shadow="sm" padding="lg" radius="md" withBorder>
        <Group justify="space-between" align="flex-start">
          <Stack gap="sm">
            <Skeleton height={14} width={120} />
            <Group gap="md">
              <Skeleton height={60} width={60} radius="md" />
              <Stack gap={4}>
                <Skeleton height={24} width={100} />
                <Skeleton height={28} width={80} />
              </Stack>
            </Group>
          </Stack>
          <Group gap="xl">
            <Stack gap={2} ta="center">
              <Skeleton height={28} width={40} />
              <Skeleton height={12} width={80} />
            </Stack>
            <Stack gap={2} ta="center">
              <Skeleton height={28} width={40} />
              <Skeleton height={12} width={80} />
            </Stack>
          </Group>
        </Group>
      </Card>
    );
  }

  return (
    <Card shadow="sm" padding="lg" radius="md" withBorder>
      <Group justify="space-between" align="flex-start" wrap="wrap" gap="md">
        <Stack gap="sm">
          <Text size="sm" fw={600} c="dimmed" tt="uppercase">
            Security Overview
          </Text>
          <Group gap="md">
            <ThemeIcon
              size={60}
              radius="md"
              color={threatConfig.color}
              variant="light"
            >
              <ThreatIcon size={32} />
            </ThemeIcon>
            <Stack gap={4}>
              <Text size="lg" fw={700}>
                Threat Level
              </Text>
              <Badge size="lg" color={threatConfig.color} variant="filled">
                {threatConfig.label}
              </Badge>
            </Stack>
          </Group>
        </Stack>

        <Group gap="xl" wrap="wrap">
          <Stack gap={2} ta="center">
            <Text size="xl" fw={700}>
              {overview.activeThreatsCount}
            </Text>
            <Text size="xs" c="dimmed">
              Active Threats
            </Text>
          </Stack>
          <Stack gap={2} ta="center">
            <Text size="xl" fw={700}>
              {overview.eventsLast24h}
            </Text>
            <Text size="xs" c="dimmed">
              Events (24h)
            </Text>
          </Stack>
          {overview.complianceScore !== null && (
            <Stack gap={2} ta="center">
              <Text size="xl" fw={700}>
                {overview.complianceScore}%
              </Text>
              <Text size="xs" c="dimmed">
                Compliance
              </Text>
            </Stack>
          )}
        </Group>
      </Group>
    </Card>
  );
}
