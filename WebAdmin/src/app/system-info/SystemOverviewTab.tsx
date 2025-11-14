'use client';

import {
  Card,
  Title,
  Stack,
  Paper,
  Group,
  Text,
  Badge,
  Progress,
  Switch,
} from '@mantine/core';
import {
  IconCircleCheck,
  IconAlertTriangle,
} from '@tabler/icons-react';
import { SystemInfoDto, LLMCacheControlDto } from '@knn_labs/conduit-admin-client';
import { generateSystemMetrics, getStatusColor } from './helpers';

interface SystemOverviewTabProps {
  systemInfo: SystemInfoDto | null;
  cacheStatus: LLMCacheControlDto | null;
  onCacheToggle: (newValue: boolean) => void;
  isTogglingCache: boolean;
}

export function SystemOverviewTab({ systemInfo, cacheStatus, onCacheToggle, isTogglingCache }: SystemOverviewTabProps) {
  const systemMetrics = generateSystemMetrics(systemInfo, cacheStatus);

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'running':
      case 'healthy':
      case 'latest':
        return <IconCircleCheck size={16} />;
      case 'degraded':
      case 'warning':
      case 'outdated':
        return <IconAlertTriangle size={16} />;
      default:
        return <IconAlertTriangle size={16} />;
    }
  };

  return (
    <Card shadow="sm" p="md" radius="md" withBorder>
      <Title order={4} mb="md">System Resources</Title>
      <Stack gap="md">
        {systemMetrics.map((metric) => (
          <Paper key={metric.name} p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <div>
                <Text fw={500}>{metric.name}</Text>
                {metric.description && (
                  <Text size="xs" c="dimmed">{metric.description}</Text>
                )}
              </div>
              <Group gap="xs">
                {!metric.isToggleable && (
                  <Badge
                    leftSection={getStatusIcon(metric.status)}
                    color={getStatusColor(metric.status)}
                    variant="light"
                  >
                    {metric.status}
                  </Badge>
                )}
                {metric.isToggleable ? (
                  <Switch
                    checked={metric.toggleValue ?? false}
                    onChange={(event) => onCacheToggle(event.currentTarget.checked)}
                    disabled={isTogglingCache}
                    label={metric.toggleValue ? 'Enabled' : 'Disabled'}
                    color="green"
                  />
                ) : (
                  <Text fw={600}>
                    {String(metric.value)}{metric.unit ?? ''}
                  </Text>
                )}
              </Group>
            </Group>
            {typeof metric.value === 'number' && metric.unit === '%' && (
              <Progress
                value={metric.value}
                color={getStatusColor(metric.status)}
                size="sm"
                radius="md"
              />
            )}
          </Paper>
        ))}
      </Stack>
    </Card>
  );
}