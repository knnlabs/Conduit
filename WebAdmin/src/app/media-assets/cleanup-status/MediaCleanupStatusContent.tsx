'use client';

import { useState, useEffect, useCallback } from 'react';
import {
  Stack,
  Group,
  Card,
  Text,
  Title,
  Badge,
  Switch,
  Progress,
  SimpleGrid,
  Loader,
  Alert,
  Tooltip,
  Paper,
  Button,
  NumberInput,
  Divider,
} from '@mantine/core';
import {
  IconClock,
  IconTrash,
  IconDatabase,
  IconAlertCircle,
  IconCheck,
  IconX,
  IconCalendarTime,
  IconExternalLink,
} from '@tabler/icons-react';
import Link from 'next/link';
import { notify } from '@/lib/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import type { MediaCleanupStatus } from '@knn_labs/conduit-admin-client';

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
}

function formatDate(dateString: string | null): string {
  if (!dateString) return 'Never';
  const date = new Date(dateString);
  return date.toLocaleString();
}

function formatDuration(seconds: number | null): string {
  if (seconds === null) return 'N/A';
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;
  return `${minutes}m ${remainingSeconds.toFixed(0)}s`;
}

function getBudgetColor(percent: number): string {
  if (percent > 90) return 'red';
  if (percent > 75) return 'yellow';
  return 'green';
}

export default function MediaCleanupStatusContent() {
  const [status, setStatus] = useState<MediaCleanupStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [toggleLoading, setToggleLoading] = useState(false);
  const [retentionLoading, setRetentionLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Local state for simple retention override
  const [simpleRetentionDays, setSimpleRetentionDays] = useState<number | null>(null);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

  const fetchStatus = useCallback(async () => {
    try {
      setError(null);
      const result = await withAdminClient(client =>
        client.media.getCleanupServiceStatus()
      );
      setStatus(result);
      // Sync local retention state
      setSimpleRetentionDays(result.simpleRetentionOverrideDays);
      setHasUnsavedChanges(false);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch status';
      setError(message);
      console.error('Failed to fetch cleanup status:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchStatus();
    // Refresh every 30 seconds
    const interval = setInterval(() => {
      void fetchStatus();
    }, 30000);
    return () => clearInterval(interval);
  }, [fetchStatus]);

  const handleToggleEnabled = async (enabled: boolean) => {
    setToggleLoading(true);
    try {
      const response = await withAdminClient(client =>
        client.media.setCleanupServiceEnabled(enabled)
      );
      notify.success(response.message ?? `Cleanup service ${enabled ? 'enabled' : 'disabled'}`);
      void fetchStatus();
    } catch (err) {
      notify.error(err, 'Failed to toggle service');
    } finally {
      setToggleLoading(false);
    }
  };

  const handleSaveSimpleRetention = async () => {
    setRetentionLoading(true);
    try {
      const response = await withAdminClient(client =>
        client.media.setSimpleRetentionOverride(simpleRetentionDays)
      );
      notify.success(response.message ?? 'Simple retention override updated');
      setHasUnsavedChanges(false);
      void fetchStatus();
    } catch (err) {
      notify.error(err, 'Failed to update retention');
    } finally {
      setRetentionLoading(false);
    }
  };

  const handleRetentionChange = (value: number | string) => {
    // Handle empty string or null - set to null (clear override)
    if (value === '' || value === null || value === undefined) {
      if (simpleRetentionDays !== null) {
        setSimpleRetentionDays(null);
        setHasUnsavedChanges(true);
      }
      return;
    }
    const numValue = typeof value === 'string' ? parseInt(value, 10) : value;
    if (!isNaN(numValue) && numValue >= 1 && numValue <= 365) {
      setSimpleRetentionDays(numValue);
      setHasUnsavedChanges(numValue !== status?.simpleRetentionOverrideDays);
    }
  };

  const handleClearOverride = () => {
    setSimpleRetentionDays(null);
    setHasUnsavedChanges(status?.simpleRetentionOverrideDays !== null);
  };

  if (loading) {
    return (
      <Stack align="center" py="xl">
        <Loader size="lg" />
        <Text c="dimmed">Loading cleanup service status...</Text>
      </Stack>
    );
  }

  if (error) {
    return (
      <Alert icon={<IconAlertCircle size={16} />} title="Error" color="red">
        {error}
      </Alert>
    );
  }

  if (!status) {
    return (
      <Alert icon={<IconAlertCircle size={16} />} title="No Data" color="yellow">
        No cleanup service status available
      </Alert>
    );
  }

  const budgetPercent = Math.min(status.monthlyBudgetUsedPercent, 100);
  const budgetColor = getBudgetColor(budgetPercent);

  return (
    <Stack gap="lg">
      {/* Service Control */}
      <Card withBorder shadow="sm">
        <Group justify="space-between" align="center">
          <div>
            <Title order={4}>Service Control</Title>
            <Text size="sm" c="dimmed">
              Enable or disable the cleanup service at runtime
            </Text>
          </div>
          <Group gap="md">
            <Badge
              color={status.isEnabled ? 'green' : 'gray'}
              variant="filled"
              size="lg"
              leftSection={status.isEnabled ? <IconCheck size={14} /> : <IconX size={14} />}
            >
              {status.isEnabled ? 'Enabled' : 'Disabled'}
            </Badge>
            <Switch
              size="lg"
              checked={status.isEnabled}
              disabled={toggleLoading}
              onChange={(event) => void handleToggleEnabled(event.currentTarget.checked)}
              label={toggleLoading ? 'Updating...' : undefined}
            />
          </Group>
        </Group>
        {status.isDryRunMode && (
          <Alert color="yellow" mt="md" icon={<IconAlertCircle size={16} />}>
            <Text size="sm" fw={500}>Dry Run Mode Active</Text>
            <Text size="xs" c="dimmed">
              The service will log what would be deleted but won&apos;t actually remove any files.
            </Text>
          </Alert>
        )}
      </Card>

      {/* Simple Retention Override */}
      <Card withBorder shadow="sm">
        <Group justify="space-between" align="flex-start">
          <div>
            <Title order={4}>Simple Retention Override</Title>
            <Text size="sm" c="dimmed">
              When enabled, all media is deleted after the specified number of days,
              ignoring account balance-based policies.
            </Text>
          </div>
          <Badge
            color={simpleRetentionDays !== null ? 'orange' : 'gray'}
            variant="filled"
            size="lg"
          >
            {simpleRetentionDays !== null ? 'Override Active' : 'Using Policies'}
          </Badge>
        </Group>

        <Divider my="md" />

        <Stack gap="md">
          <Group align="flex-end" gap="md">
            <NumberInput
              label="Retention Days"
              description="All media will be deleted after this many days (1-365)"
              placeholder="Enter days or leave empty to use policies"
              value={simpleRetentionDays ?? ''}
              onChange={handleRetentionChange}
              min={1}
              max={365}
              step={1}
              style={{ flex: 1 }}
              leftSection={<IconCalendarTime size={16} />}
            />
            <Button
              variant="light"
              color="gray"
              onClick={handleClearOverride}
              disabled={simpleRetentionDays === null || retentionLoading}
            >
              Clear Override
            </Button>
            <Button
              color="blue"
              onClick={() => void handleSaveSimpleRetention()}
              loading={retentionLoading}
              disabled={!hasUnsavedChanges}
            >
              Save
            </Button>
          </Group>

          {simpleRetentionDays !== null && (
            <Alert color="orange" icon={<IconAlertCircle size={16} />}>
              <Text size="sm" fw={500}>
                Override is active: All media will be deleted after {simpleRetentionDays} day{simpleRetentionDays !== 1 ? 's' : ''}
              </Text>
              <Text size="xs" c="dimmed">
                This overrides all balance-based retention policies. Recently accessed media will also be deleted.
              </Text>
            </Alert>
          )}

          {hasUnsavedChanges && (
            <Alert color="yellow" icon={<IconAlertCircle size={16} />}>
              <Text size="sm">You have unsaved changes</Text>
            </Alert>
          )}
        </Stack>

        <Divider my="md" />

        <Text size="xs" c="dimmed">
          <strong>Tip:</strong> Use the simple override for quick cleanup adjustments.
          For fine-grained control based on account balances, leave this empty and configure retention policies.
        </Text>
      </Card>

      {/* Budget Overview */}
      <Card withBorder shadow="sm">
        <Title order={4} mb="md">Monthly Budget</Title>
        <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="md">
          <Paper p="md" withBorder>
            <Group gap="xs" mb="xs">
              <IconTrash size={16} color="gray" />
              <Text size="sm" c="dimmed">Deletions This Month</Text>
            </Group>
            <Text size="xl" fw={700}>
              {status.monthlyDeleteCount.toLocaleString()}
            </Text>
          </Paper>
          <Paper p="md" withBorder>
            <Group gap="xs" mb="xs">
              <IconDatabase size={16} color="gray" />
              <Text size="sm" c="dimmed">Budget Limit</Text>
            </Group>
            <Text size="xl" fw={700}>
              {status.monthlyDeleteBudget.toLocaleString()}
            </Text>
          </Paper>
          <Paper p="md" withBorder>
            <Group gap="xs" mb="xs">
              <IconCheck size={16} color="gray" />
              <Text size="sm" c="dimmed">Remaining</Text>
            </Group>
            <Text size="xl" fw={700} c={budgetColor}>
              {status.monthlyDeleteBudgetRemaining.toLocaleString()}
            </Text>
          </Paper>
        </SimpleGrid>
        <Stack gap="xs" mt="md">
          <Group justify="space-between">
            <Text size="sm">Budget Used</Text>
            <Text size="sm" fw={500} c={budgetColor}>
              {status.monthlyBudgetUsedPercent.toFixed(1)}%
            </Text>
          </Group>
          <Progress value={budgetPercent} color={budgetColor} size="lg" />
        </Stack>
      </Card>

      {/* Last Run Info */}
      <Card withBorder shadow="sm">
        <Title order={4} mb="md">Last Run</Title>
        <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
          <Paper p="md" withBorder>
            <Group gap="xs" mb="xs">
              <IconClock size={16} color="gray" />
              <Text size="sm" c="dimmed">Time</Text>
            </Group>
            <Text size="lg" fw={500}>
              {formatDate(status.lastRunTimeUtc)}
            </Text>
          </Paper>
          <Paper p="md" withBorder>
            <Group gap="xs" mb="xs">
              <IconAlertCircle size={16} color="gray" />
              <Text size="sm" c="dimmed">Status</Text>
            </Group>
            <Badge
              color={status.lastRunStatus === 'Completed' ? 'green' : 'yellow'}
              variant="light"
            >
              {status.lastRunStatus ?? 'No runs yet'}
            </Badge>
          </Paper>
          <Paper p="md" withBorder>
            <Group gap="xs" mb="xs">
              <IconTrash size={16} color="gray" />
              <Text size="sm" c="dimmed">Files Deleted</Text>
            </Group>
            <Text size="lg" fw={500}>
              {status.lastRunFilesDeleted.toLocaleString()}
            </Text>
          </Paper>
          <Paper p="md" withBorder>
            <Group gap="xs" mb="xs">
              <IconDatabase size={16} color="gray" />
              <Text size="sm" c="dimmed">Space Freed</Text>
            </Group>
            <Text size="lg" fw={500}>
              {formatBytes(status.lastRunBytesFreed)}
            </Text>
          </Paper>
        </SimpleGrid>
        {status.lastRunDurationSeconds !== null && (
          <Text size="sm" c="dimmed" mt="md">
            Duration: {formatDuration(status.lastRunDurationSeconds)}
          </Text>
        )}
      </Card>

      {/* Schedule Info */}
      <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="lg">
        <Card withBorder shadow="sm">
          <Title order={4} mb="md">Schedule</Title>
          <Stack gap="sm">
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Interval</Text>
              <Text size="sm" fw={500}>{status.scheduleIntervalMinutes} minutes</Text>
            </Group>
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Max Batch Size</Text>
              <Text size="sm" fw={500}>{status.maxBatchSize} items</Text>
            </Group>
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Next Scheduled Run</Text>
              <Tooltip label={status.nextScheduledRunUtc ?? 'Not scheduled'}>
                <Text size="sm" fw={500}>
                  {status.nextScheduledRunUtc
                    ? formatDate(status.nextScheduledRunUtc)
                    : 'Not scheduled'}
                </Text>
              </Tooltip>
            </Group>
            {status.currentLeaderInstanceId && (
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Current Leader</Text>
                <Badge variant="dot" color="blue">
                  {status.currentLeaderInstanceId}
                </Badge>
              </Group>
            )}
          </Stack>
        </Card>

        {/* Retention Policy */}
        <Card withBorder shadow="sm">
          <Group justify="space-between" mb="md">
            <Title order={4}>Default Retention Policy</Title>
            <Button
              component={Link}
              href="/media-assets/retention-policies"
              variant="subtle"
              size="xs"
              rightSection={<IconExternalLink size={14} />}
            >
              Manage Policies
            </Button>
          </Group>
          {status.defaultRetentionPolicy ? (
            <Stack gap="sm">
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Policy Name</Text>
                <Text size="sm" fw={500}>{status.defaultRetentionPolicy.name}</Text>
              </Group>
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Positive Balance</Text>
                <Badge color="green" variant="light">
                  {status.defaultRetentionPolicy.positiveBalanceRetentionDays} days
                </Badge>
              </Group>
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Zero Balance</Text>
                <Badge color="yellow" variant="light">
                  {status.defaultRetentionPolicy.zeroBalanceRetentionDays} days
                </Badge>
              </Group>
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Negative Balance</Text>
                <Badge color="red" variant="light">
                  {status.defaultRetentionPolicy.negativeBalanceRetentionDays} days
                </Badge>
              </Group>
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Total Active Policies</Text>
                <Text size="sm" fw={500}>{status.activeRetentionPoliciesCount}</Text>
              </Group>
            </Stack>
          ) : (
            <Stack gap="md">
              <Alert color="yellow" icon={<IconAlertCircle size={16} />}>
                No default retention policy configured
              </Alert>
              <Button
                component={Link}
                href="/media-assets/retention-policies"
                variant="light"
              >
                Create a Retention Policy
              </Button>
            </Stack>
          )}
        </Card>
      </SimpleGrid>
    </Stack>
  );
}
