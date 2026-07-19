'use client';

import { useState, useEffect, useCallback } from 'react';
import {
  Stack,
  Group,
  Card,
  Text,
  Title,
  Badge,
  Button,
  Table,
  ActionIcon,
  Menu,
  Loader,
  Alert,
  Modal,
  TextInput,
  Textarea,
  NumberInput,
  Switch,
  SimpleGrid,
  Tooltip,
} from '@mantine/core';
import {
  IconPlus,
  IconRefresh,
  IconEdit,
  IconTrash,
  IconDotsVertical,
  IconAlertCircle,
  IconStar,
  IconCheck,
} from '@tabler/icons-react';
import { notify } from '@/lib/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import type { MediaRetentionPolicy, CreateMediaRetentionPolicyRequest, UpdateMediaRetentionPolicyRequest } from '@knn_labs/conduit-admin-client';

interface PolicyFormData {
  name: string;
  description: string;
  positiveBalanceRetentionDays: number;
  zeroBalanceRetentionDays: number;
  negativeBalanceRetentionDays: number;
  softDeleteGracePeriodDays: number;
  respectRecentAccess: boolean;
  recentAccessWindowDays: number;
  isDefault: boolean;
  isActive: boolean;
}

const DEFAULT_FORM_DATA: PolicyFormData = {
  name: '',
  description: '',
  positiveBalanceRetentionDays: 60,
  zeroBalanceRetentionDays: 14,
  negativeBalanceRetentionDays: 3,
  softDeleteGracePeriodDays: 7,
  respectRecentAccess: true,
  recentAccessWindowDays: 7,
  isDefault: false,
  isActive: true,
};

export default function RetentionPoliciesContent() {
  const [policies, setPolicies] = useState<MediaRetentionPolicy[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Modal state
  const [modalOpen, setModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState<'create' | 'edit'>('create');
  const [editingPolicy, setEditingPolicy] = useState<MediaRetentionPolicy | null>(null);
  const [formData, setFormData] = useState<PolicyFormData>(DEFAULT_FORM_DATA);
  const [saving, setSaving] = useState(false);

  // Delete confirmation
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [deletingPolicy, setDeletingPolicy] = useState<MediaRetentionPolicy | null>(null);
  const [deleting, setDeleting] = useState(false);

  const fetchPolicies = useCallback(async () => {
    try {
      setError(null);
      const result = await withAdminClient(client =>
        client.media.getRetentionPolicies()
      );
      setPolicies(result);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch policies';
      setError(message);
      console.error('Failed to fetch retention policies:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchPolicies();
  }, [fetchPolicies]);

  const handleOpenCreateModal = () => {
    setModalMode('create');
    setEditingPolicy(null);
    setFormData(DEFAULT_FORM_DATA);
    setModalOpen(true);
  };

  const handleOpenEditModal = (policy: MediaRetentionPolicy) => {
    setModalMode('edit');
    setEditingPolicy(policy);
    setFormData({
      name: policy.name,
      description: policy.description ?? '',
      positiveBalanceRetentionDays: policy.positiveBalanceRetentionDays,
      zeroBalanceRetentionDays: policy.zeroBalanceRetentionDays,
      negativeBalanceRetentionDays: policy.negativeBalanceRetentionDays,
      softDeleteGracePeriodDays: policy.softDeleteGracePeriodDays,
      respectRecentAccess: policy.respectRecentAccess,
      recentAccessWindowDays: policy.recentAccessWindowDays,
      isDefault: policy.isDefault,
      isActive: policy.isActive,
    });
    setModalOpen(true);
  };

  const handleSave = async () => {
    if (!formData.name.trim()) {
      notify.error('Policy name is required');
      return;
    }

    setSaving(true);
    try {
      if (modalMode === 'create') {
        const createData: CreateMediaRetentionPolicyRequest = {
          name: formData.name,
          description: formData.description || undefined,
          positiveBalanceRetentionDays: formData.positiveBalanceRetentionDays,
          zeroBalanceRetentionDays: formData.zeroBalanceRetentionDays,
          negativeBalanceRetentionDays: formData.negativeBalanceRetentionDays,
          softDeleteGracePeriodDays: formData.softDeleteGracePeriodDays,
          respectRecentAccess: formData.respectRecentAccess,
          recentAccessWindowDays: formData.recentAccessWindowDays,
          isDefault: formData.isDefault,
          isActive: formData.isActive,
        };
        await withAdminClient(client =>
          client.media.createRetentionPolicy(createData)
        );
        notify.success(`Policy "${formData.name}" created`);
      } else if (editingPolicy) {
        const updateData: UpdateMediaRetentionPolicyRequest = {
          name: formData.name,
          description: formData.description || undefined,
          positiveBalanceRetentionDays: formData.positiveBalanceRetentionDays,
          zeroBalanceRetentionDays: formData.zeroBalanceRetentionDays,
          negativeBalanceRetentionDays: formData.negativeBalanceRetentionDays,
          softDeleteGracePeriodDays: formData.softDeleteGracePeriodDays,
          respectRecentAccess: formData.respectRecentAccess,
          recentAccessWindowDays: formData.recentAccessWindowDays,
          isActive: formData.isActive,
        };
        await withAdminClient(client =>
          client.media.updateRetentionPolicy(editingPolicy.id, updateData)
        );
        notify.success(`Policy "${formData.name}" updated`);
      }
      setModalOpen(false);
      void fetchPolicies();
    } catch (err) {
      notify.error(err, 'Failed to save policy');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!deletingPolicy) return;

    setDeleting(true);
    try {
      await withAdminClient(client =>
        client.media.deleteRetentionPolicy(deletingPolicy.id)
      );
      notify.success(`Policy "${deletingPolicy.name}" deleted`);
      setDeleteModalOpen(false);
      setDeletingPolicy(null);
      void fetchPolicies();
    } catch (err) {
      notify.error(err, 'Failed to delete policy');
    } finally {
      setDeleting(false);
    }
  };

  const handleSetDefault = async (policy: MediaRetentionPolicy) => {
    try {
      await withAdminClient(client =>
        client.media.setDefaultRetentionPolicy(policy.id)
      );
      notify.success(`"${policy.name}" is now the default policy`);
      void fetchPolicies();
    } catch (err) {
      notify.error(err, 'Failed to set default policy');
    }
  };

  if (loading) {
    return (
      <Stack align="center" py="xl">
        <Loader size="lg" />
        <Text c="dimmed">Loading retention policies...</Text>
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

  const rows = policies.map((policy) => (
    <Table.Tr key={policy.id}>
      <Table.Td>
        <Group gap="xs">
          <Text fw={500}>{policy.name}</Text>
          {policy.isDefault && (
            <Badge color="blue" size="xs" variant="filled">Default</Badge>
          )}
          {!policy.isActive && (
            <Badge color="gray" size="xs" variant="outline">Inactive</Badge>
          )}
        </Group>
        {policy.description && (
          <Text size="xs" c="dimmed" mt={4}>{policy.description}</Text>
        )}
      </Table.Td>
      <Table.Td>
        <Badge color="green" variant="light">{policy.positiveBalanceRetentionDays}d</Badge>
      </Table.Td>
      <Table.Td>
        <Badge color="yellow" variant="light">{policy.zeroBalanceRetentionDays}d</Badge>
      </Table.Td>
      <Table.Td>
        <Badge color="red" variant="light">{policy.negativeBalanceRetentionDays}d</Badge>
      </Table.Td>
      <Table.Td>
        <Text size="sm">{policy.softDeleteGracePeriodDays}d</Text>
      </Table.Td>
      <Table.Td>
        {policy.respectRecentAccess ? (
          <Tooltip label={`Within ${policy.recentAccessWindowDays} days`}>
            <Badge color="teal" variant="light">Yes</Badge>
          </Tooltip>
        ) : (
          <Badge color="gray" variant="light">No</Badge>
        )}
      </Table.Td>
      <Table.Td>
        <Menu shadow="md" width={200}>
          <Menu.Target>
            <ActionIcon variant="subtle" color="gray">
              <IconDotsVertical size={16} />
            </ActionIcon>
          </Menu.Target>
          <Menu.Dropdown>
            <Menu.Item
              leftSection={<IconEdit size={14} />}
              onClick={() => handleOpenEditModal(policy)}
            >
              Edit
            </Menu.Item>
            {!policy.isDefault && policy.isActive && (
              <Menu.Item
                leftSection={<IconStar size={14} />}
                onClick={() => void handleSetDefault(policy)}
              >
                Set as Default
              </Menu.Item>
            )}
            <Menu.Divider />
            <Menu.Item
              color="red"
              leftSection={<IconTrash size={14} />}
              onClick={() => {
                setDeletingPolicy(policy);
                setDeleteModalOpen(true);
              }}
              disabled={policy.isDefault}
            >
              Delete
            </Menu.Item>
          </Menu.Dropdown>
        </Menu>
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Stack gap="lg">
      {/* Header with actions */}
      <Card withBorder shadow="sm">
        <Group justify="space-between">
          <div>
            <Title order={4}>Retention Policies</Title>
            <Text size="sm" c="dimmed">
              {policies.length} {policies.length === 1 ? 'policy' : 'policies'} configured
            </Text>
          </div>
          <Group>
            <Button
              variant="subtle"
              leftSection={<IconRefresh size={16} />}
              onClick={() => void fetchPolicies()}
            >
              Refresh
            </Button>
            <Button
              leftSection={<IconPlus size={16} />}
              onClick={handleOpenCreateModal}
            >
              Create Policy
            </Button>
          </Group>
        </Group>
      </Card>

      {/* Policies table */}
      {policies.length === 0 ? (
        <Alert icon={<IconAlertCircle size={16} />} color="yellow">
          No retention policies configured. Create one to get started.
        </Alert>
      ) : (
        <Card withBorder shadow="sm" p={0}>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Name</Table.Th>
                <Table.Th>Positive Balance</Table.Th>
                <Table.Th>Zero Balance</Table.Th>
                <Table.Th>Negative Balance</Table.Th>
                <Table.Th>Grace Period</Table.Th>
                <Table.Th>Recent Access</Table.Th>
                <Table.Th w={50}></Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>{rows}</Table.Tbody>
          </Table>
        </Card>
      )}

      {/* Create/Edit Modal */}
      <Modal
        opened={modalOpen}
        onClose={() => setModalOpen(false)}
        title={modalMode === 'create' ? 'Create Retention Policy' : 'Edit Retention Policy'}
        size="lg"
      >
        <Stack gap="md">
          <TextInput
            label="Policy Name"
            placeholder="e.g., Default, Standard, Enterprise"
            required
            value={formData.name}
            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
          />

          <Textarea
            label="Description"
            placeholder="Describe this policy..."
            value={formData.description}
            onChange={(e) => setFormData({ ...formData, description: e.target.value })}
          />

          <SimpleGrid cols={3}>
            <NumberInput
              label="Positive Balance Days"
              description="Retention for positive balance"
              value={formData.positiveBalanceRetentionDays}
              onChange={(val) => setFormData({ ...formData, positiveBalanceRetentionDays: Number(val) || 60 })}
              min={1}
              max={365}
            />
            <NumberInput
              label="Zero Balance Days"
              description="Retention for zero balance"
              value={formData.zeroBalanceRetentionDays}
              onChange={(val) => setFormData({ ...formData, zeroBalanceRetentionDays: Number(val) || 14 })}
              min={1}
              max={365}
            />
            <NumberInput
              label="Negative Balance Days"
              description="Retention for negative balance"
              value={formData.negativeBalanceRetentionDays}
              onChange={(val) => setFormData({ ...formData, negativeBalanceRetentionDays: Number(val) || 3 })}
              min={1}
              max={365}
            />
          </SimpleGrid>

          <SimpleGrid cols={2}>
            <NumberInput
              label="Soft Delete Grace Period"
              description="Days before permanent deletion"
              value={formData.softDeleteGracePeriodDays}
              onChange={(val) => setFormData({ ...formData, softDeleteGracePeriodDays: Number(val) || 7 })}
              min={0}
              max={30}
            />
            <NumberInput
              label="Recent Access Window"
              description="Days to consider as recent"
              value={formData.recentAccessWindowDays}
              onChange={(val) => setFormData({ ...formData, recentAccessWindowDays: Number(val) || 7 })}
              min={1}
              max={30}
              disabled={!formData.respectRecentAccess}
            />
          </SimpleGrid>

          <SimpleGrid cols={2}>
            <Switch
              label="Respect Recent Access"
              description="Don't delete recently accessed media"
              checked={formData.respectRecentAccess}
              onChange={(e) => setFormData({ ...formData, respectRecentAccess: e.currentTarget.checked })}
            />
            <Switch
              label="Active"
              description="Policy can be assigned"
              checked={formData.isActive}
              onChange={(e) => setFormData({ ...formData, isActive: e.currentTarget.checked })}
            />
          </SimpleGrid>

          {modalMode === 'create' && (
            <Switch
              label="Set as Default"
              description="New groups use this policy"
              checked={formData.isDefault}
              onChange={(e) => setFormData({ ...formData, isDefault: e.currentTarget.checked })}
            />
          )}

          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={() => setModalOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => void handleSave()}
              loading={saving}
              leftSection={<IconCheck size={16} />}
            >
              {modalMode === 'create' ? 'Create' : 'Save'}
            </Button>
          </Group>
        </Stack>
      </Modal>

      {/* Delete Confirmation Modal */}
      <Modal
        opened={deleteModalOpen}
        onClose={() => setDeleteModalOpen(false)}
        title="Delete Policy"
        size="sm"
      >
        <Stack gap="md">
          <Text>
            Are you sure you want to delete the policy <strong>{deletingPolicy?.name}</strong>?
          </Text>
          <Text size="sm" c="dimmed">
            This action cannot be undone. Virtual key groups using this policy will fall back to the default policy.
          </Text>
          <Group justify="flex-end">
            <Button variant="subtle" onClick={() => setDeleteModalOpen(false)}>
              Cancel
            </Button>
            <Button
              color="red"
              onClick={() => void handleDelete()}
              loading={deleting}
              leftSection={<IconTrash size={16} />}
            >
              Delete
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}
