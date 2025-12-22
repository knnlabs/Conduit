'use client';

import {
  Card,
  Stack,
  Group,
  Text,
  Badge,
  ThemeIcon,
  Alert,
  Skeleton,
  ScrollArea,
} from '@mantine/core';
import {
  IconAlertTriangle,
  IconCheck,
  IconClock,
  IconShieldCheck,
} from '@tabler/icons-react';
import { formatters } from '@/lib/utils/formatters';
import type { ThreatDetection } from './types';

interface ActiveThreatsPanelProps {
  threats: ThreatDetection[];
  isLoading?: boolean;
}

type ThreatSeverity = ThreatDetection['severity'];
type ThreatStatus = ThreatDetection['status'];

const SEVERITY_COLORS: Record<ThreatSeverity, string> = {
  minor: 'blue',
  major: 'orange',
  critical: 'red',
};

const STATUS_ICONS: Record<ThreatStatus, typeof IconAlertTriangle> = {
  active: IconAlertTriangle,
  acknowledged: IconClock,
  resolved: IconCheck,
};

export function ActiveThreatsPanel({
  threats,
  isLoading = false,
}: ActiveThreatsPanelProps) {
  // Filter to show active and acknowledged threats (exclude resolved)
  const activeThreats = threats.filter(t => t.status !== 'resolved');

  if (isLoading) {
    return (
      <Stack gap="md">
        {Array.from({ length: 3 }).map((_, index) => (
          <Card key={index} padding="md" radius="md" withBorder>
            <Group justify="space-between" align="flex-start" mb="sm">
              <Group gap="sm">
                <Skeleton height={32} width={32} radius="md" />
                <Stack gap={4}>
                  <Skeleton height={16} width={120} />
                  <Skeleton height={12} width={80} />
                </Stack>
              </Group>
              <Group gap="xs">
                <Skeleton height={20} width={60} />
                <Skeleton height={20} width={60} />
              </Group>
            </Group>
            <Skeleton height={14} width="100%" mb="sm" />
            <Skeleton height={12} width={100} />
          </Card>
        ))}
      </Stack>
    );
  }

  if (activeThreats.length === 0) {
    return (
      <Alert
        color="green"
        variant="light"
        icon={<IconShieldCheck size={20} />}
      >
        <Text size="sm">
          No active threats detected. System security is nominal.
        </Text>
      </Alert>
    );
  }

  return (
    <ScrollArea.Autosize mah={500}>
      <Stack gap="md">
        {activeThreats.map((threat) => {
          const StatusIcon = STATUS_ICONS[threat.status];
          return (
            <Card key={threat.id} padding="md" radius="md" withBorder>
              <Group justify="space-between" align="flex-start" mb="sm">
                <Group gap="sm">
                  <ThemeIcon
                    color={SEVERITY_COLORS[threat.severity]}
                    variant="light"
                    size="md"
                  >
                    <StatusIcon size={16} />
                  </ThemeIcon>
                  <div>
                    <Text fw={600} size="sm">
                      {threat.title}
                    </Text>
                    <Text size="xs" c="dimmed">
                      {threat.type}
                    </Text>
                  </div>
                </Group>
                <Group gap="xs">
                  <Badge
                    color={SEVERITY_COLORS[threat.severity]}
                    variant="filled"
                    size="sm"
                  >
                    {threat.severity}
                  </Badge>
                  <Badge color="gray" variant="light" size="sm">
                    {threat.status}
                  </Badge>
                </Group>
              </Group>

              <Text size="sm" c="dimmed" mb="sm" lineClamp={2}>
                {threat.description}
              </Text>

              {threat.affectedResources.length > 0 && (
                <Group gap="xs" mb="sm">
                  <Text size="xs" fw={500}>
                    Affected:
                  </Text>
                  {threat.affectedResources.slice(0, 3).map((resource, idx) => (
                    <Badge key={idx} size="xs" variant="outline">
                      {resource}
                    </Badge>
                  ))}
                  {threat.affectedResources.length > 3 && (
                    <Badge size="xs" variant="outline" color="gray">
                      +{threat.affectedResources.length - 3} more
                    </Badge>
                  )}
                </Group>
              )}

              <Text size="xs" c="dimmed">
                Detected: {formatters.date(threat.detectedAt, { includeTime: true })}
              </Text>
            </Card>
          );
        })}
      </Stack>
    </ScrollArea.Autosize>
  );
}
