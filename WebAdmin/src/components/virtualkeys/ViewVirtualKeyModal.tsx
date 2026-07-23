'use client';

import {
  Modal,
  Stack,
  Group,
  Text,
  Badge,
  Alert,
  Button,
  Card,
  Code,
} from '@mantine/core';
import {
  IconKey,
  IconAlertCircle,
} from '@tabler/icons-react';
import type { VirtualKeyDto, VirtualKeyGroupDto } from '@/lib/admin-api';
import { TimeDisplay } from '@/components/common/TimeDisplay';

interface ViewVirtualKeyModalProps {
  opened: boolean;
  onClose: () => void;
  virtualKey: VirtualKeyDto | null;
  virtualKeyGroup?: VirtualKeyGroupDto;
}

function formatMetadata(metadata: Record<string, unknown>): string {
  return JSON.stringify(metadata, null, 2);
}

export function ViewVirtualKeyModal({
  opened,
  onClose,
  virtualKey,
  virtualKeyGroup,
}: ViewVirtualKeyModalProps) {
  if (!virtualKey) {
    return null;
  }

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={
        <Group gap="sm">
          <IconKey size={20} />
          <Text fw={500}>Virtual Key Details</Text>
        </Group>
      }
      size="lg"
      centered
    >
      <Stack gap="lg">
        {/* Key Information */}
        <Card withBorder>
          <Stack gap="sm">
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Key Name</Text>
              <Text fw={500}>{virtualKey.keyName}</Text>
            </Group>

            {virtualKey.description && (
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Description</Text>
                <Text size="sm" ta="right">{virtualKey.description}</Text>
              </Group>
            )}

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Key Prefix</Text>
              <Text size="sm" ff="monospace">{virtualKey.keyPrefix ?? 'N/A'}</Text>
            </Group>

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Customer</Text>
              <Text size="sm" ta="right">
                {virtualKeyGroup?.groupName ?? `Group #${virtualKey.virtualKeyGroupId}`}
              </Text>
            </Group>

            {virtualKeyGroup?.externalGroupId && (
              <Group justify="space-between">
                <Text size="sm" c="dimmed">External Customer ID</Text>
                <Text size="sm" ff="monospace">{virtualKeyGroup.externalGroupId}</Text>
              </Group>
            )}

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Status</Text>
              <Badge color={virtualKey.isEnabled ? 'green' : 'red'}>
                {virtualKey.isEnabled ? 'Active' : 'Disabled'}
              </Badge>
            </Group>

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Created</Text>
              <Text size="sm"><TimeDisplay date={virtualKey.createdAt} format="datetime" /></Text>
            </Group>

            {virtualKey.expiresAt && (
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Expires</Text>
                <Text size="sm"><TimeDisplay date={virtualKey.expiresAt} format="datetime" /></Text>
              </Group>
            )}
          </Stack>
        </Card>

        {/* Rate Limits */}
        {(virtualKey.rateLimitRpm ?? virtualKey.rateLimitRpd) && (
          <Card withBorder>
            <Stack gap="sm">
              <Text size="sm" fw={500}>Rate Limits</Text>
              {virtualKey.rateLimitRpm && (
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Requests per minute</Text>
                  <Text size="sm">{virtualKey.rateLimitRpm}</Text>
                </Group>
              )}
              {virtualKey.rateLimitRpd && (
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">Requests per day</Text>
                  <Text size="sm">{virtualKey.rateLimitRpd}</Text>
                </Group>
              )}
            </Stack>
          </Card>
        )}

        {/* Allowed Models */}
        {virtualKey.allowedModels && (
          <Card withBorder>
            <Stack gap="sm">
              <Text size="sm" fw={500}>Allowed Models</Text>
              <Text size="sm" c="dimmed">{virtualKey.allowedModels}</Text>
            </Stack>
          </Card>
        )}

        {virtualKey.metadata && (
          <Card withBorder>
            <Stack gap="sm">
              <Text size="sm" fw={500}>Metadata</Text>
              <Code block style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
                {formatMetadata(virtualKey.metadata)}
              </Code>
            </Stack>
          </Card>
        )}

        {/* Info about group-based billing */}
        <Alert icon={<IconAlertCircle size={16} />} color="blue" variant="light">
          <Text size="sm">
            This key belongs to {virtualKeyGroup?.groupName ?? `Virtual Key Group #${virtualKey.virtualKeyGroupId}`}.
            Balance and usage are tracked at the group level.
          </Text>
        </Alert>

        <Group justify="flex-end">
          <Button onClick={onClose}>Close</Button>
        </Group>
      </Stack>
    </Modal>
  );
}
