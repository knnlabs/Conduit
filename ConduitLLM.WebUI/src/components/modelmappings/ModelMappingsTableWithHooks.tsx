import { useState, useEffect, useMemo } from 'react';
import {
  Table,
  Group,
  Text,
  ActionIcon,
  Badge,
  Menu,
  rem,
  Box,
  LoadingOverlay,
  Alert,
  Checkbox,
} from '@mantine/core';
import {
  IconEdit,
  IconTrash,
  IconDotsVertical,
  IconArrowRight,
  IconAlertCircle,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { useRouter } from 'next/navigation';
import { 
  useModelMappings, 
  useDeleteModelMapping,
  useBulkDeleteModelMappings,
  useBulkEnableModelMappings,
  useBulkDisableModelMappings,
} from '@/hooks/useModelMappingsApi';
import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';
import { BulkActionsBar } from './BulkActionsBar';

// Extend the DTO type to ensure provider property is available
interface ExtendedModelProviderMappingDto extends ModelProviderMappingDto {
  provider?: {
    id: number;
    providerType: number;
    displayName: string;
    isEnabled: boolean;
  };
}

interface ModelMappingsTableProps {
  onRefresh?: () => void;
}

export function ModelMappingsTable({ onRefresh }: ModelMappingsTableProps) {
  const { mappings, isLoading, error, refetch } = useModelMappings();
  const deleteMapping = useDeleteModelMapping();
  const bulkDelete = useBulkDeleteModelMappings();
  const bulkEnable = useBulkEnableModelMappings();
  const bulkDisable = useBulkDisableModelMappings();
  const router = useRouter();
  
  // Selection state
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  
  // Computed values for selection
  
  const isAllSelected = useMemo(() => {
    if (mappings.length === 0) return false;
    return mappings.every(m => selectedIds.has(m.id));
  }, [mappings, selectedIds]);
  
  const isIndeterminate = useMemo(() => {
    if (selectedIds.size === 0) return false;
    return selectedIds.size > 0 && selectedIds.size < mappings.length;
  }, [selectedIds, mappings]);

  // Selection handlers
  const handleSelectAll = () => {
    if (isAllSelected) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(mappings.map(m => m.id)));
    }
  };
  
  const handleSelectOne = (id: number) => {
    const newSelection = new Set(selectedIds);
    if (newSelection.has(id)) {
      newSelection.delete(id);
    } else {
      newSelection.add(id);
    }
    setSelectedIds(newSelection);
  };
  
  const handleClearSelection = () => {
    setSelectedIds(new Set());
  };
  
  // Bulk action handlers
  const handleBulkDelete = () => {
    const ids = Array.from(selectedIds);
    void bulkDelete.mutateAsync(ids).then(() => {
      setSelectedIds(new Set());
    });
  };
  
  const handleBulkEnable = () => {
    const ids = Array.from(selectedIds);
    void bulkEnable.mutateAsync(ids).then(() => {
      setSelectedIds(new Set());
    });
  };
  
  const handleBulkDisable = () => {
    const ids = Array.from(selectedIds);
    void bulkDisable.mutateAsync(ids).then(() => {
      setSelectedIds(new Set());
    });
  };

  // Refresh data when onRefresh changes
  useEffect(() => {
    if (onRefresh) {
      void refetch();
    }
  }, [onRefresh, refetch]);

  const handleEdit = (mapping: ExtendedModelProviderMappingDto) => {
    router.push(`/model-mappings/edit/${mapping.id}`);
  };


  const handleDelete = (mapping: ExtendedModelProviderMappingDto) => {
    modals.openConfirmModal({
      title: 'Delete Model Mapping',
      children: (
        <Text size="sm">
          Are you sure you want to delete the mapping for model &quot;{mapping.modelAlias}&quot;?
          This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => { void deleteMapping.mutateAsync(mapping.id); },
    });
  };

  const getCapabilityBadges = (mapping: ExtendedModelProviderMappingDto) => {
    // Note: Capabilities are now stored in the Model entity
    // This shows basic mapping information
    const badges = [];
    
    // Add badges based on available mapping properties
    if (mapping.notes) {
      badges.push({ label: 'Has Notes', color: 'gray' });
    }
    
    return badges.slice(0, 5).map((badge) => (
      <Badge key={`${badge.label}-${badge.color}`} size="xs" variant="dot" color={badge.color}>
        {badge.label}
      </Badge>
    ));
  };

  if (error) {
    return (
      <Alert icon={<IconAlertCircle size={16} />} title="Error" color="red">
        Failed to load model mappings: {error instanceof Error ? error.message : 'Unknown error'}
      </Alert>
    );
  }

  if (mappings.length === 0 && !isLoading) {
    return (
      <Box p="md" pos="relative">
        <Text c="dimmed" ta="center">
          No model mappings found. Create your first mapping to get started.
        </Text>
      </Box>
    );
  }

  const rows = (mappings as ExtendedModelProviderMappingDto[]).map((mapping) => (
    <Table.Tr key={mapping.id} bg={selectedIds.has(mapping.id) ? 'blue.0' : undefined}>
      <Table.Td style={{ width: 60 }}>
        <Checkbox
          checked={selectedIds.has(mapping.id)}
          onChange={() => handleSelectOne(mapping.id)}
        />
      </Table.Td>
      <Table.Td>
        <Group gap="xs">
          <Text size="sm" fw={500}>{mapping.modelAlias}</Text>
          <IconArrowRight size={14} style={{ color: 'var(--mantine-color-dimmed)' }} />
          <Text size="sm" c="dimmed">{mapping.providerModelId}</Text>
        </Group>
      </Table.Td>
      
      <Table.Td>
        <Text size="sm">
          {mapping.provider?.displayName ?? mapping.providerId}
        </Text>
      </Table.Td>

      <Table.Td>
        <Group gap={4}>
          {getCapabilityBadges(mapping)}
        </Group>
      </Table.Td>

      <Table.Td>
        <Text size="sm">{mapping.priority}</Text>
      </Table.Td>

      <Table.Td>
        <Badge
          color={mapping.isEnabled ? 'green' : 'gray'}
          variant="light"
          size="sm"
        >
          {mapping.isEnabled ? 'Enabled' : 'Disabled'}
        </Badge>
      </Table.Td>

      <Table.Td>
        <Group gap={0} justify="flex-end">
          <Menu position="bottom-end" withinPortal>
            <Menu.Target>
              <ActionIcon variant="subtle" color="gray" size="sm">
                <IconDotsVertical style={{ width: rem(16), height: rem(16) }} />
              </ActionIcon>
            </Menu.Target>
            <Menu.Dropdown>
              <Menu.Item
                leftSection={<IconEdit style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => handleEdit(mapping)}
              >
                Edit
              </Menu.Item>
              <Menu.Divider />
              <Menu.Item
                color="red"
                leftSection={<IconTrash style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => handleDelete(mapping)}
              >
                Delete
              </Menu.Item>
            </Menu.Dropdown>
          </Menu>
        </Group>
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <>
      <Box pos="relative">
        <LoadingOverlay visible={isLoading} />
        <Table.ScrollContainer minWidth={800}>
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th style={{ width: 60 }}>
                  <Checkbox
                    checked={isAllSelected}
                    indeterminate={isIndeterminate}
                    onChange={handleSelectAll}
                  />
                </Table.Th>
                <Table.Th>Model Mapping</Table.Th>
                <Table.Th>Provider</Table.Th>
                <Table.Th>Capabilities</Table.Th>
                <Table.Th>Priority</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>{rows}</Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Box>
      
      <BulkActionsBar
        selectedCount={selectedIds.size}
        onDelete={handleBulkDelete}
        onEnable={handleBulkEnable}
        onDisable={handleBulkDisable}
        onClearSelection={handleClearSelection}
        isDeleting={bulkDelete.isPending}
        isEnabling={bulkEnable.isPending}
        isDisabling={bulkDisable.isPending}
      />
    </>
  );
}