'use client';

import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Table,
  ScrollArea,
  Checkbox,
  Badge,
  Text,
  Group,
  Button,
  ActionIcon,
  Menu,
  LoadingOverlay,
  Center,
  Stack,
  Card,
  Divider,
  Modal,
  Box,
} from '@mantine/core';
import { IconDots, IconEye, IconCheck, IconX } from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import type { DriftItemDto, DriftItemFilter } from '@/lib/admin-api';
import { formatters } from '@/lib/utils/formatters';
import {
  fetchDrift,
  DRIFT_QUERY_KEY,
  useApplyDrift,
  useDismissDrift,
  useBulkApplyDrift,
  useBulkDismissDrift,
} from '../hooks/useProviderSyncApi';
import {
  driftTypeColor,
  driftTypeLabel,
  driftStatusColor,
  driftWarnings,
} from '../utils/driftHelpers';
import { DriftDiffView } from './DriftDiffView';
import { DriftFilters } from './DriftFilters';
import { useBulkSelection } from '@/hooks/useBulkSelection';

const getDriftId = (item: DriftItemDto) => item.id;
const isPendingDrift = (item: DriftItemDto) => item.status === 'Pending';

export function DriftTable() {
  const [status, setStatus] = useState<string | null>('Pending');
  const [driftType, setDriftType] = useState<string | null>(null);
  const [viewingItem, setViewingItem] = useState<DriftItemDto | null>(null);

  const filter: DriftItemFilter = useMemo(
    () => ({
      status: status ?? undefined,
      driftType: driftType ?? undefined,
      pageSize: 200,
    }),
    [status, driftType]
  );

  const { data, isLoading, error } = useQuery({
    queryKey: [DRIFT_QUERY_KEY, status, driftType],
    queryFn: () => fetchDrift(filter),
  });

  const items = useMemo(() => data ?? [], [data]);

  const applyMutation = useApplyDrift();
  const dismissMutation = useDismissDrift();
  const bulkApplyMutation = useBulkApplyDrift();
  const bulkDismissMutation = useBulkDismissDrift();

  // Only Pending items are actionable (apply/dismiss).
  const actionableItems = useMemo(
    () => items.filter(isPendingDrift),
    [items],
  );
  const {
    selectedKeys: selectedIds,
    isAllSelected: allSelected,
    isIndeterminate: someSelected,
    toggleOne,
    toggleAll,
    clearSelection,
    deselect,
  } = useBulkSelection({
    items: actionableItems,
    getKey: getDriftId,
  });
  const busy =
    applyMutation.isPending ||
    dismissMutation.isPending ||
    bulkApplyMutation.isPending ||
    bulkDismissMutation.isPending;

  const applyItem = (item: DriftItemDto) => {
    const warnings = driftWarnings(item);
    const run = () =>
      applyMutation.mutate(item.id, {
        onSuccess: () => {
          deselect(item.id);
          setViewingItem(null);
        },
      });
    if (warnings.length > 0) {
      modals.openConfirmModal({
        title: 'Apply drift with warnings',
        children: (
          <Stack gap="xs">
            {warnings.map((w, idx) => (
              <Text key={idx} size="sm">
                {w.message}
              </Text>
            ))}
          </Stack>
        ),
        labels: { confirm: 'Apply anyway', cancel: 'Cancel' },
        confirmProps: { color: 'orange' },
        onConfirm: run,
      });
    } else {
      run();
    }
  };

  const dismissItem = (item: DriftItemDto) => {
    dismissMutation.mutate(item.id, {
      onSuccess: () => {
        deselect(item.id);
        setViewingItem(null);
      },
    });
  };

  const handleBulkApply = () => {
    const ids = Array.from(selectedIds);
    const hasWarnings = actionableItems
      .filter((i) => selectedIds.has(i.id))
      .some((i) => driftWarnings(i).length > 0);
    modals.openConfirmModal({
      title: `Apply ${ids.length} drift item${ids.length > 1 ? 's' : ''}`,
      children: (
        <Text size="sm">
          {hasWarnings
            ? 'Some selected items carry warnings (zero-price or capability drift). '
            : ''}
          Apply the proposed changes for {ids.length} item{ids.length > 1 ? 's' : ''}?
        </Text>
      ),
      labels: { confirm: 'Apply', cancel: 'Cancel' },
      confirmProps: { color: hasWarnings ? 'orange' : undefined },
      onConfirm: () => bulkApplyMutation.mutate(ids, { onSuccess: clearSelection }),
    });
  };

  const handleBulkDismiss = () => {
    const ids = Array.from(selectedIds);
    modals.openConfirmModal({
      title: `Dismiss ${ids.length} drift item${ids.length > 1 ? 's' : ''}`,
      children: (
        <Text size="sm">
          Dismiss {ids.length} item{ids.length > 1 ? 's' : ''} without applying?
        </Text>
      ),
      labels: { confirm: 'Dismiss', cancel: 'Cancel' },
      onConfirm: () => bulkDismissMutation.mutate(ids, { onSuccess: clearSelection }),
    });
  };

  return (
    <Card withBorder padding={0}>
      <Card.Section p="md">
        <DriftFilters
          status={status}
          driftType={driftType}
          onStatusChange={setStatus}
          onDriftTypeChange={setDriftType}
        />
      </Card.Section>

      {selectedIds.size > 0 && (
        <Group
          p="md"
          style={{
            backgroundColor: 'var(--mantine-color-blue-light)',
          }}
        >
          <Text fw={500} size="sm">
            {selectedIds.size} selected
          </Text>
          <Divider orientation="vertical" />
          <Button
            size="xs"
            variant="light"
            color="green"
            leftSection={<IconCheck size={14} />}
            onClick={handleBulkApply}
            loading={bulkApplyMutation.isPending}
          >
            Apply selected
          </Button>
          <Button
            size="xs"
            variant="light"
            color="gray"
            leftSection={<IconX size={14} />}
            onClick={handleBulkDismiss}
            loading={bulkDismissMutation.isPending}
          >
            Dismiss selected
          </Button>
          <Button size="xs" variant="subtle" onClick={clearSelection} ml="auto">
            Clear
          </Button>
        </Group>
      )}

      <Card.Section p="md" pt="sm">
        <Box pos="relative">
          <LoadingOverlay visible={isLoading} />

          {error && (
            <Center py="xl">
              <Text c="red">Failed to load drift items</Text>
            </Center>
          )}

          {!error && items.length === 0 && !isLoading && (
            <Center py="xl">
              <Text c="dimmed">No drift items for this filter</Text>
            </Center>
          )}

          {!error && items.length > 0 && (
            <ScrollArea>
              <Table verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th w={40}>
                      <Checkbox
                        checked={allSelected}
                        indeterminate={someSelected}
                        onChange={toggleAll}
                        disabled={actionableItems.length === 0}
                        aria-label="Select all"
                      />
                    </Table.Th>
                    <Table.Th>Model</Table.Th>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Drift</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>First detected</Table.Th>
                    <Table.Th w={60}></Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {items.map((item) => {
                    const pending = item.status === 'Pending';
                    const warnings = driftWarnings(item);
                    return (
                      <Table.Tr key={item.id}>
                        <Table.Td>
                          <Checkbox
                            checked={selectedIds.has(item.id)}
                            onChange={() => toggleOne(item.id)}
                            disabled={!pending}
                            aria-label={`Select ${item.modelAlias}`}
                          />
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm" fw={500}>
                            {item.modelAlias}
                          </Text>
                          <Text size="xs" c="dimmed">
                            {item.openRouterModelId}
                          </Text>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm">{item.providerName}</Text>
                        </Table.Td>
                        <Table.Td>
                          <Group gap={6}>
                            <Badge color={driftTypeColor(item.driftType)} variant="light" size="sm">
                              {driftTypeLabel(item.driftType)}
                            </Badge>
                            {warnings.length > 0 && (
                              <Badge color="red" variant="outline" size="sm">
                                warning
                              </Badge>
                            )}
                          </Group>
                        </Table.Td>
                        <Table.Td>
                          <Badge color={driftStatusColor(item.status)} variant="light" size="sm">
                            {item.status}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Text size="xs" c="dimmed">
                            {formatters.date(item.firstDetectedAt)}
                          </Text>
                        </Table.Td>
                        <Table.Td>
                          <Menu position="bottom-end" withinPortal>
                            <Menu.Target>
                              <ActionIcon variant="subtle" size="sm">
                                <IconDots size={16} />
                              </ActionIcon>
                            </Menu.Target>
                            <Menu.Dropdown>
                              <Menu.Item
                                leftSection={<IconEye size={14} />}
                                onClick={() => setViewingItem(item)}
                              >
                                View details
                              </Menu.Item>
                              {pending && (
                                <>
                                  <Menu.Divider />
                                  <Menu.Item
                                    leftSection={<IconCheck size={14} />}
                                    color="green"
                                    disabled={busy}
                                    onClick={() => applyItem(item)}
                                  >
                                    Apply
                                  </Menu.Item>
                                  <Menu.Item
                                    leftSection={<IconX size={14} />}
                                    disabled={busy}
                                    onClick={() => dismissItem(item)}
                                  >
                                    Dismiss
                                  </Menu.Item>
                                </>
                              )}
                            </Menu.Dropdown>
                          </Menu>
                        </Table.Td>
                      </Table.Tr>
                    );
                  })}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          )}
        </Box>
      </Card.Section>

      <Modal
        opened={viewingItem !== null}
        onClose={() => setViewingItem(null)}
        title="Drift details"
        size="lg"
      >
        {viewingItem && (
          <Stack gap="lg">
            <DriftDiffView item={viewingItem} />
            {viewingItem.status === 'Pending' && (
              <Group justify="flex-end">
                <Button
                  variant="light"
                  color="gray"
                  leftSection={<IconX size={16} />}
                  onClick={() => dismissItem(viewingItem)}
                  loading={dismissMutation.isPending}
                >
                  Dismiss
                </Button>
                <Button
                  color="green"
                  leftSection={<IconCheck size={16} />}
                  onClick={() => applyItem(viewingItem)}
                  loading={applyMutation.isPending}
                >
                  Apply
                </Button>
              </Group>
            )}
          </Stack>
        )}
      </Modal>
    </Card>
  );
}
