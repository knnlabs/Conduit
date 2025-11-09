'use client';

import { Grid, Card, Text, Group, Stack, RingProgress, Skeleton } from '@mantine/core';
import { IconPhoto, IconVideo, IconDatabase, IconCloud } from '@tabler/icons-react';
import { useMediaStats } from '../hooks/useMediaStats';
import { formatBytes } from '../utils/formatters';

export default function MediaStatsCards() {
  const { stats, loading } = useMediaStats();

  if (loading) {
    return (
      <Grid>
        {[1, 2, 3, 4].map((i) => (
          <Grid.Col key={i} span={{ base: 12, sm: 6, md: 3 }}>
            <Card shadow="sm" p="lg" radius="md" withBorder>
              <Skeleton height={120} />
            </Card>
          </Grid.Col>
        ))}
      </Grid>
    );
  }

  if (!stats) return null;

  const totalUsagePercent = Math.min((stats.totalSizeBytes / (10 * 1024 * 1024 * 1024)) * 100, 100); // 10GB example limit

  return (
    <Grid>
      <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
        <Card shadow="sm" p="lg" radius="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="sm" c="dimmed">Total Storage</Text>
            <IconDatabase size={20} opacity={0.5} />
          </Group>
          <Text fw={700} size="xl">{formatBytes(stats.totalSizeBytes)}</Text>
          <Text size="xs" c="dimmed" mt="xs">
            {stats.totalFiles} files
          </Text>
          <RingProgress
            size={80}
            thickness={8}
            sections={[{ value: totalUsagePercent, color: 'blue' }]}
            mt="md"
          />
        </Card>
      </Grid.Col>

      <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
        <Card shadow="sm" p="lg" radius="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="sm" c="dimmed">Images</Text>
            <IconPhoto size={20} opacity={0.5} />
          </Group>
          <Text fw={700} size="xl">{stats.byMediaType.image?.fileCount ?? 0}</Text>
          <Text size="xs" c="dimmed" mt="xs">
            {formatBytes(stats.byMediaType.image?.sizeBytes ?? 0)}
          </Text>
          <Text size="xs" c="blue" mt="md">
            {stats.totalSizeBytes > 0 ? ((stats.byMediaType.image?.sizeBytes ?? 0) / stats.totalSizeBytes * 100).toFixed(1) : '0'}% of total
          </Text>
        </Card>
      </Grid.Col>

      <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
        <Card shadow="sm" p="lg" radius="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="sm" c="dimmed">Videos</Text>
            <IconVideo size={20} opacity={0.5} />
          </Group>
          <Text fw={700} size="xl">{stats.byMediaType.video?.fileCount ?? 0}</Text>
          <Text size="xs" c="dimmed" mt="xs">
            {formatBytes(stats.byMediaType.video?.sizeBytes ?? 0)}
          </Text>
          <Text size="xs" c="green" mt="md">
            {stats.totalSizeBytes > 0 ? ((stats.byMediaType.video?.sizeBytes ?? 0) / stats.totalSizeBytes * 100).toFixed(1) : '0'}% of total
          </Text>
        </Card>
      </Grid.Col>

      <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
        <Card shadow="sm" p="lg" radius="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="sm" c="dimmed">Health Status</Text>
            <IconCloud size={20} opacity={0.5} />
          </Group>
          <Stack gap="xs">
            {stats.orphanedFiles > 0 && (
              <Text size="sm" c="orange">
                {stats.orphanedFiles} orphaned files
              </Text>
            )}
            {stats.orphanedFiles === 0 && (
              <Text size="sm" c="green">
                All files healthy
              </Text>
            )}
          </Stack>
        </Card>
      </Grid.Col>
    </Grid>
  );
}