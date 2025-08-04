'use client';

import {
  Modal,
  Stack,
  Group,
  Text,
  Badge,
  Button,
  Card,
  Table,
  LoadingOverlay,
  Alert,
} from '@mantine/core';
import {
  IconLayersLinked,
  IconKey,
  IconAlertCircle,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { formatters } from '@/lib/utils/formatters';
import type { VirtualKeyGroupDto, VirtualKeyDto } from '@knn_labs/conduit-admin-client';

interface ViewVirtualKeyGroupModalProps {
  opened: boolean;
  onClose: () => void;
  group: VirtualKeyGroupDto | null;
}

export function ViewVirtualKeyGroupModal({ opened, onClose, group }: ViewVirtualKeyGroupModalProps) {
  const [keys, setKeys] = useState<VirtualKeyDto[]>([]);
  const [isLoadingKeys, setIsLoadingKeys] = useState(false);

  useEffect(() => {
    const fetchKeys = async () => {
      if (!group) return;

      try {
        setIsLoadingKeys(true);
        const response = await fetch(`/api/virtualkeys/groups/${group.id}/keys`);
        
        if (response.ok) {
          const data = await response.json() as VirtualKeyDto[];
          setKeys(data);
        }
      } catch (error) {
        console.warn('Failed to fetch keys:', error);
      } finally {
        setIsLoadingKeys(false);
      }
    };
    
    if (opened && group) {
      void fetchKeys();
    }
  }, [opened, group]);

  if (!group) return null;

  const getBalanceColor = (balance: number) => {
    if (balance <= 0) return 'red';
    if (balance < 10) return 'orange';
    return 'green';
  };

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={
        <Group gap="sm">
          <IconLayersLinked size={20} />
          <Text fw={500}>Virtual Key Group Details</Text>
        </Group>
      }
      size="lg"
    >
      <Stack gap="lg">
        {/* Group Information */}
        <Card withBorder>
          <Stack gap="sm">
            <Group justify="space-between">
              <Text size="sm" c="dimmed">Group Name</Text>
              <Text fw={500}>{group.groupName}</Text>
            </Group>

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Group ID</Text>
              <Text size="sm">#{group.id}</Text>
            </Group>

            {group.externalGroupId && (
              <Group justify="space-between">
                <Text size="sm" c="dimmed">External ID</Text>
                <Text size="sm">{group.externalGroupId}</Text>
              </Group>
            )}

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Current Balance</Text>
              <Badge 
                color={getBalanceColor(group.balance)} 
                variant={group.balance <= 0 ? 'filled' : 'light'}
                size="lg"
              >
                {formatters.currency(group.balance)}
              </Badge>
            </Group>

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Lifetime Credits Added</Text>
              <Text size="sm">{formatters.currency(group.lifetimeCreditsAdded)}</Text>
            </Group>

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Lifetime Spent</Text>
              <Text size="sm">{formatters.currency(group.lifetimeSpent)}</Text>
            </Group>

            <Group justify="space-between">
              <Text size="sm" c="dimmed">Created</Text>
              <Text size="sm">{formatters.date(group.createdAt)}</Text>
            </Group>
          </Stack>
        </Card>

        {/* Virtual Keys in Group */}
        <Card withBorder>
          <Stack gap="md">
            <Group justify="space-between">
              <Group gap="xs">
                <IconKey size={16} />
                <Text fw={500}>Virtual Keys</Text>
              </Group>
              <Badge color="gray" variant="light">
                {group.virtualKeyCount} {group.virtualKeyCount === 1 ? 'key' : 'keys'}
              </Badge>
            </Group>

            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoadingKeys} />
              
              {keys.length > 0 ? (
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Key Name</Table.Th>
                      <Table.Th>Status</Table.Th>
                      <Table.Th>Created</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {keys.map((key) => (
                      <Table.Tr key={key.id}>
                        <Table.Td>{key.keyName}</Table.Td>
                        <Table.Td>
                          <Badge color={key.isEnabled ? 'green' : 'red'} variant="light" size="sm">
                            {key.isEnabled ? 'Active' : 'Disabled'}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm" c="dimmed">
                            {formatters.date(key.createdAt)}
                          </Text>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              ) : (
                <Text c="dimmed" ta="center" py="md">
                  No virtual keys in this group
                </Text>
              )}
            </div>
          </Stack>
        </Card>

        {/* Balance Warning */}
        {group.balance <= 0 && (
          <Alert icon={<IconAlertCircle size={16} />} color="red" variant="light">
            <Text size="sm">
              This group has insufficient balance. Virtual keys in this group may not function properly.
              Please add credits to restore functionality.
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