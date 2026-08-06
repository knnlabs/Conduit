'use client';

import { useState, useEffect } from 'react';
import { Table, Badge, Group, Text, ActionIcon, LoadingOverlay } from '@mantine/core';
import { IconEdit, IconTrash } from '@tabler/icons-react';
import { notify } from '@/lib/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import { EditProviderToolModal } from './EditProviderToolModal';
import { modals } from '@mantine/modals';
import type { ProviderTool } from '@/lib/admin-api';

interface ProviderToolsTableProps {
  onRefresh: () => void;
}

export function ProviderToolsTable({ onRefresh }: ProviderToolsTableProps) {
  const { executeWithAdmin } = useAdminClient();
  const [tools, setTools] = useState<ProviderTool[]>([]);
  const [loading, setLoading] = useState(true);
  const [editingTool, setEditingTool] = useState<ProviderTool | null>(null);

  useEffect(() => {
    const fetchTools = async () => {
      try {
        setLoading(true);
        const data = await executeWithAdmin(client =>
          client.providerTools.getProviderTools()
        );
        setTools(data);
      } catch (error) {
        console.error('Failed to load provider tools:', error);
        notify.error(error, 'Failed to load provider tools');
      } finally {
        setLoading(false);
      }
    };

    void fetchTools();
  }, [executeWithAdmin]);

  const loadTools = async () => {
    try {
      setLoading(true);
      const data = await executeWithAdmin(client =>
        client.providerTools.getProviderTools()
      );
      setTools(data);
    } catch (error) {
      console.error('Failed to load provider tools:', error);
      notify.error(error, 'Failed to load provider tools');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = (tool: ProviderTool) => {
    modals.openConfirmModal({
      title: 'Delete Provider Tool',
      children: (
        <Text size="sm">
          Are you sure you want to delete the tool &quot;{tool.toolName}&quot; for {tool.providerName ?? 'Unknown Provider'}?
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => {
        void (async () => {
          try {
            await executeWithAdmin(client =>
              client.providerTools.deleteProviderTool(tool.id)
            );
            notify.success(`Successfully deleted ${tool.toolName}`);
            onRefresh();
            void loadTools();
          } catch (error) {
            console.error('Failed to delete tool:', error);
            notify.error(error, 'Failed to delete provider tool');
          }
        })();
      },
    });
  };

  const formatCostPerUnit = (costPerUnit?: number | null, billingUnit?: string | null) => {
    if (!costPerUnit) return 'Not configured';
    const formattedCost = costPerUnit < 0.01 ? costPerUnit.toExponential(2) : costPerUnit.toFixed(4);
    return `$${formattedCost}${billingUnit ? ` per ${billingUnit}` : ''}`;
  };

  const rows = tools.map((tool) => (
    <Table.Tr key={tool.id}>
      <Table.Td>{tool.providerName ?? 'Unknown'}</Table.Td>
      <Table.Td>{tool.toolName}</Table.Td>
      <Table.Td>{formatCostPerUnit(tool.costPerUnit, tool.billingUnit)}</Table.Td>
      <Table.Td>{tool.costDescription ?? '-'}</Table.Td>
      <Table.Td>
        <Badge color={tool.isActive ? 'green' : 'gray'}>
          {tool.isActive ? 'Active' : 'Inactive'}
        </Badge>
      </Table.Td>
      <Table.Td>
        <Text size="xs" c="dimmed">
          {new Date(tool.updatedAt).toLocaleDateString()}
        </Text>
      </Table.Td>
      <Table.Td>
        <Group gap="xs">
          <ActionIcon
            variant="subtle"
            onClick={() => setEditingTool(tool)}
          >
            <IconEdit size={16} />
          </ActionIcon>
          <ActionIcon
            variant="subtle"
            color="red"
            onClick={() => handleDelete(tool)}
          >
            <IconTrash size={16} />
          </ActionIcon>
        </Group>
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <>
      <div style={{ position: 'relative', minHeight: 200 }}>
        <LoadingOverlay visible={loading} />
        {!loading && tools.length === 0 ? (
          <Text ta="center" py="xl" c="dimmed">
            No provider tools configured yet
          </Text>
        ) : (
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Provider</Table.Th>
                <Table.Th>Tool Name</Table.Th>
                <Table.Th>Cost</Table.Th>
                <Table.Th>Description</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Updated</Table.Th>
                <Table.Th>Actions</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>{rows}</Table.Tbody>
          </Table>
        )}
      </div>

      {editingTool && (
        <EditProviderToolModal
          isOpen={!!editingTool}
          tool={editingTool}
          onClose={() => setEditingTool(null)}
          onSuccess={() => {
            setEditingTool(null);
            onRefresh();
            void loadTools();
          }}
        />
      )}
    </>
  );
}
