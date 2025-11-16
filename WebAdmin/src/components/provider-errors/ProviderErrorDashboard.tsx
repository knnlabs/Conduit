import { SimpleGrid, Card, Text, Group, ThemeIcon, Stack, RingProgress } from '@mantine/core';
import {
  IconAlertCircle,
  IconAlertTriangle,
  IconCircleX,
  IconKey,
} from '@tabler/icons-react';
import type { components } from '@knn_labs/conduit-admin-client';

type ErrorStatisticsDto = components['schemas']['ConduitLLM.Admin.DTOs.ErrorStatisticsDto'];

interface ProviderErrorDashboardProps {
  stats: ErrorStatisticsDto | null;
}

export function ProviderErrorDashboard({ stats }: ProviderErrorDashboardProps) {
  if (!stats) {
    return null;
  }

  const totalErrors = stats.totalErrors ?? 0;
  const fatalErrors = stats.fatalErrors ?? 0;
  const warnings = stats.warnings ?? 0;
  const disabledKeys = stats.disabledKeys ?? 0;

  const fatalPercentage = totalErrors > 0 ? (fatalErrors / totalErrors) * 100 : 0;
  const warningPercentage = totalErrors > 0 ? (warnings / totalErrors) * 100 : 0;

  return (
    <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }}>
      <Card>
        <Stack gap="xs">
          <Group justify="space-between">
            <ThemeIcon size="lg" radius="md" color="gray">
              <IconAlertCircle size={20} />
            </ThemeIcon>
            <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
              Total Errors
            </Text>
          </Group>
          <Text size="xl" fw={700}>
            {totalErrors.toLocaleString()}
          </Text>
          <Text size="xs" c="dimmed">
            In last {stats.timeWindow ? Math.floor(parseFloat(stats.timeWindow) / 3600000) : 0} hours
          </Text>
        </Stack>
      </Card>

      <Card>
        <Stack gap="xs">
          <Group justify="space-between">
            <ThemeIcon size="lg" radius="md" color="red">
              <IconCircleX size={20} />
            </ThemeIcon>
            <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
              Fatal Errors
            </Text>
          </Group>
          <Group align="flex-end" gap="xs">
            <Text size="xl" fw={700} c="red">
              {fatalErrors.toLocaleString()}
            </Text>
            {totalErrors > 0 && (
              <Text size="sm" c="dimmed">
                ({fatalPercentage.toFixed(1)}%)
              </Text>
            )}
          </Group>
          <RingProgress
            size={60}
            thickness={4}
            sections={[
              { value: fatalPercentage, color: 'red' },
              { value: 100 - fatalPercentage, color: 'gray.2' },
            ]}
          />
        </Stack>
      </Card>

      <Card>
        <Stack gap="xs">
          <Group justify="space-between">
            <ThemeIcon size="lg" radius="md" color="yellow">
              <IconAlertTriangle size={20} />
            </ThemeIcon>
            <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
              Warnings
            </Text>
          </Group>
          <Group align="flex-end" gap="xs">
            <Text size="xl" fw={700} c="yellow.7">
              {warnings.toLocaleString()}
            </Text>
            {totalErrors > 0 && (
              <Text size="sm" c="dimmed">
                ({warningPercentage.toFixed(1)}%)
              </Text>
            )}
          </Group>
          <RingProgress
            size={60}
            thickness={4}
            sections={[
              { value: warningPercentage, color: 'yellow' },
              { value: 100 - warningPercentage, color: 'gray.2' },
            ]}
          />
        </Stack>
      </Card>

      <Card>
        <Stack gap="xs">
          <Group justify="space-between">
            <ThemeIcon size="lg" radius="md" color="orange">
              <IconKey size={20} />
            </ThemeIcon>
            <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
              Disabled Keys
            </Text>
          </Group>
          <Text size="xl" fw={700} c={disabledKeys > 0 ? 'orange' : undefined}>
            {disabledKeys}
          </Text>
          <Text size="xs" c="dimmed">
            {disabledKeys > 0 ? 'Keys disabled due to errors' : 'All keys operational'}
          </Text>
        </Stack>
      </Card>
    </SimpleGrid>
  );
}