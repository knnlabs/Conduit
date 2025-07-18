'use client';

import {
  Modal,
  Stack,
  Group,
  Text,
  Badge,
  Alert,
  Button,
  CopyButton,
  Tooltip,
  ActionIcon,
  Card,
  SimpleGrid,
  Progress,
  // Removed unused Title import
} from '@mantine/core';
import {
  IconKey,
  // Removed unused IconClock import
  IconActivity,
  IconCreditCard,
  IconCopy,
  IconCheck,
  IconAlertCircle,
} from '@tabler/icons-react';
import type { VirtualKeyDto } from '@knn_labs/conduit-admin-client';

// Extend VirtualKeyDto with UI-specific fields added by the API
interface VirtualKeyWithUI extends VirtualKeyDto {
  displayKey: string;
}

interface ViewVirtualKeyModalProps {
  opened: boolean;
  onClose: () => void;
  virtualKey: VirtualKeyWithUI | null;
}

export function ViewVirtualKeyModal({ opened, onClose, virtualKey }: ViewVirtualKeyModalProps) {
  if (!virtualKey) {
    return null;
  }

  const spendPercentage = virtualKey.maxBudget 
    ? (virtualKey.currentSpend / virtualKey.maxBudget) * 100 
    : 0;

  const formatDate = (date: string) => {
    return new Date(date).toLocaleString();
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
    }).format(amount);
  };

  const getSpendColor = (percentage: number) => {
    if (percentage > 90) return 'red';
    if (percentage > 75) return 'orange';
    return 'blue';
  };

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

            {virtualKey.metadata && (
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Description</Text>
                <Text size="sm">{JSON.stringify(virtualKey.metadata)}</Text>
              </Group>
            )}

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Key Hash</Text>
              <Group gap="xs">
                <Text size="sm" ff="monospace">{virtualKey.displayKey}</Text>
                <CopyButton value={virtualKey.displayKey}>
                  {({ copied, copy }) => (
                    <Tooltip label={copied ? 'Copied' : 'Copy'}>
                      <ActionIcon 
                        size="sm" 
                        variant="subtle" 
                        onClick={copy}
                        color={copied ? 'green' : 'gray'}
                      >
                        {copied ? <IconCheck size={14} /> : <IconCopy size={14} />}
                      </ActionIcon>
                    </Tooltip>
                  )}
                </CopyButton>
              </Group>
            </Group>

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Status</Text>
              <Badge color={virtualKey.isEnabled ? 'green' : 'red'}>
                {virtualKey.isEnabled ? 'Active' : 'Disabled'}
              </Badge>
            </Group>

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Created</Text>
              <Text size="sm">{formatDate(virtualKey.createdAt)}</Text>
            </Group>

            {virtualKey.lastUsedAt && (
              <Group justify="space-between">
                <Text size="sm" c="dimmed">Last Used</Text>
                <Text size="sm">{formatDate(virtualKey.lastUsedAt)}</Text>
              </Group>
            )}
          </Stack>
        </Card>

        {/* Usage Statistics */}
        <SimpleGrid cols={2} spacing="md">
          <Card withBorder>
            <Stack gap="xs">
              <Group justify="space-between">
                <IconActivity size={20} color="var(--mantine-color-blue-6)" />
                <Text size="xs" c="dimmed">Requests</Text>
              </Group>
              <Text size="xl" fw={700}>{virtualKey.requestCount?.toLocaleString() ?? '0'}</Text>
            </Stack>
          </Card>

          <Card withBorder>
            <Stack gap="xs">
              <Group justify="space-between">
                <IconCreditCard size={20} color="var(--mantine-color-green-6)" />
                <Text size="xs" c="dimmed">Spend</Text>
              </Group>
              <Text size="xl" fw={700}>{formatCurrency(virtualKey.currentSpend)}</Text>
            </Stack>
          </Card>
        </SimpleGrid>

        {/* Budget Progress */}
        {virtualKey.maxBudget && (
          <Card withBorder>
            <Stack gap="sm">
              <Group justify="space-between">
                <Text size="sm" fw={500}>Budget Usage</Text>
                <Text size="sm" c="dimmed">
                  {formatCurrency(virtualKey.currentSpend)} / {formatCurrency(virtualKey.maxBudget)}
                </Text>
              </Group>
              <Progress 
                value={spendPercentage} 
                color={getSpendColor(spendPercentage)}
                size="lg"
              />
              <Text size="xs" c="dimmed" ta="center">
                {spendPercentage.toFixed(1)}% of budget used
              </Text>
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

        {/* Warning for no budget limit */}
        {!virtualKey.maxBudget && (
          <Alert icon={<IconAlertCircle size={16} />} color="yellow" variant="light">
            <Text size="sm">
              This key has no budget limit. Usage is unrestricted and may incur unexpected costs.
            </Text>
          </Alert>
        )}

        <Group justify="flex-end">
          <Button onClick={onClose}>Close</Button>
        </Group>
      </Stack>
    </Modal>
  );
}