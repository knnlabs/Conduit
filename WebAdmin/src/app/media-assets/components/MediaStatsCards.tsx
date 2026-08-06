'use client';

import { Grid, Card, Text, Group, Stack, RingProgress, Skeleton, Table, Badge } from '@mantine/core';
import { IconPhoto, IconVideo, IconDatabase, IconCloud } from '@tabler/icons-react';
import { useMediaStats } from '../hooks/useMediaStats';
import { formatters } from '@/lib/utils/formatters';

export default function MediaStatsCards() {
  const { stats, isLoading } = useMediaStats();

  if (isLoading) {
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

  const constrainedGroups = stats.groupQuotaUsage.filter(
    group => group.maxStorageSizeBytes !== null && group.maxStorageSizeBytes !== undefined ||
      group.maxFileCount !== null && group.maxFileCount !== undefined
  );
  const overQuotaGroups = constrainedGroups.filter(group => group.isOverQuota);
  const totalStorageQuota = constrainedGroups.reduce(
    (total, group) => total + (group.maxStorageSizeBytes ?? 0),
    0
  );
  const quotaStorageUsage = constrainedGroups.reduce(
    (total, group) => total + (
      group.maxStorageSizeBytes === null || group.maxStorageSizeBytes === undefined
        ? 0
        : group.totalSizeBytes
    ),
    0
  );
  const totalUsagePercent = totalStorageQuota > 0
    ? Math.min((quotaStorageUsage / totalStorageQuota) * 100, 100)
    : 0;

  return (
    <Grid>
      <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
        <Card shadow="sm" p="lg" radius="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="sm" c="dimmed">Total Storage</Text>
            <IconDatabase size={20} opacity={0.5} />
          </Group>
          <Text fw={700} size="xl">{formatters.fileSize(stats.totalSizeBytes)}</Text>
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
            {formatters.fileSize(stats.byMediaType.image?.sizeBytes ?? 0)}
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
            {formatters.fileSize(stats.byMediaType.video?.sizeBytes ?? 0)}
          </Text>
          <Text size="xs" c="green" mt="md">
            {stats.totalSizeBytes > 0 ? ((stats.byMediaType.video?.sizeBytes ?? 0) / stats.totalSizeBytes * 100).toFixed(1) : '0'}% of total
          </Text>
        </Card>
      </Grid.Col>

      <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
        <Card shadow="sm" p="lg" radius="md" withBorder>
          <Group justify="space-between" mb="xs">
            <Text size="sm" c="dimmed">Quota Status</Text>
            <IconCloud size={20} opacity={0.5} />
          </Group>
          <Stack gap="xs">
            {overQuotaGroups.length > 0 && (
              <Text size="sm" c="red">
                {overQuotaGroups.length} {overQuotaGroups.length === 1 ? 'group is' : 'groups are'} over quota
              </Text>
            )}
            {overQuotaGroups.length === 0 && (
              <Text size="sm" c="green">
                All configured quotas healthy
              </Text>
            )}
            <Text size="xs" c="dimmed">
              {constrainedGroups.length} groups with limits
            </Text>
          </Stack>
        </Card>
      </Grid.Col>

      {constrainedGroups.length > 0 && (
        <Grid.Col span={12}>
          <Card shadow="sm" p="lg" radius="md" withBorder>
            <Text fw={600} mb="sm">Storage quota usage by virtual-key group</Text>
            <Table striped highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Group</Table.Th>
                  <Table.Th>Storage</Table.Th>
                  <Table.Th>Files</Table.Th>
                  <Table.Th>Over-quota behavior</Table.Th>
                  <Table.Th>Status</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {constrainedGroups.map(group => (
                  <Table.Tr key={group.virtualKeyGroupId}>
                    <Table.Td>
                      <Text size="sm" fw={500}>{group.virtualKeyGroupName}</Text>
                      <Text size="xs" c="dimmed">
                        {group.mediaRetentionPolicyName ?? 'No active policy'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">
                        {formatters.fileSize(group.totalSizeBytes)} /{' '}
                        {group.maxStorageSizeBytes === null || group.maxStorageSizeBytes === undefined
                          ? 'Unlimited'
                          : formatters.fileSize(group.maxStorageSizeBytes)}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">
                        {group.totalFiles.toLocaleString()} /{' '}
                        {group.maxFileCount?.toLocaleString() ?? 'Unlimited'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">
                        {group.quotaExceededBehavior === 'reject'
                          ? 'Reject generation'
                          : 'Allow and evict'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Badge color={group.isOverQuota ? 'red' : 'green'} variant="light">
                        {group.isOverQuota ? 'Over quota' : 'Within quota'}
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Card>
        </Grid.Col>
      )}
    </Grid>
  );
}
