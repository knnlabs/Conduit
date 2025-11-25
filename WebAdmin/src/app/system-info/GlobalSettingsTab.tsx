'use client';

import {
  Card,
  Title,
  Stack,
  Group,
  Text,
  Badge,
  Button,
  Table,
  ScrollArea,
  TextInput,
  Textarea,
  Modal,
  ActionIcon,
  Paper,
  SimpleGrid,
  Tooltip,
} from '@mantine/core';
import {
  IconRefresh,
  IconPlus,
  IconTrash,
  IconCheck,
  IconX,
  IconEdit,
  IconChartBar,
} from '@tabler/icons-react';
import { useState } from 'react';
import type { GlobalSettingDto, GlobalSettingCacheStats } from '@knn_labs/conduit-admin-client';

/**
 * Function discovery cache statistics - local type definition
 * This data comes from the backend's function discovery cache endpoint
 */
export interface FunctionDiscoveryCacheStatistics {
  isEnabled: boolean;
  totalEntries: number;
  cacheHits: number;
  cacheMisses: number;
}

interface GlobalSettingsTabProps {
  settings: GlobalSettingDto[];
  cacheStats: GlobalSettingCacheStats | null;
  functionDiscoveryCacheStats: FunctionDiscoveryCacheStatistics | null;
  onUpdate: (id: number, value: string, description?: string) => Promise<void>;
  onCreate: (key: string, value: string, description?: string) => Promise<void>;
  onDelete: (id: number, key: string) => Promise<void>;
  onReloadCache: () => Promise<void>;
  onInvalidateFunctionDiscoveryCache: () => Promise<void>;
  isLoading: boolean;
}

export function GlobalSettingsTab({
  settings,
  cacheStats,
  functionDiscoveryCacheStats,
  onUpdate,
  onCreate,
  onDelete,
  onReloadCache,
  onInvalidateFunctionDiscoveryCache,
  isLoading,
}: GlobalSettingsTabProps) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editValue, setEditValue] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [newKey, setNewKey] = useState('');
  const [newValue, setNewValue] = useState('');
  const [newDescription, setNewDescription] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  const handleEdit = (setting: GlobalSettingDto) => {
    setEditingId(setting.id);
    setEditValue(setting.value);
    setEditDescription(setting.description ?? '');
  };

  const handleSave = async (id: number) => {
    setIsSaving(true);
    try {
      await onUpdate(id, editValue, editDescription);
      setEditingId(null);
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => {
    setEditingId(null);
    setEditValue('');
    setEditDescription('');
  };

  const handleCreate = async () => {
    if (!newKey.trim() || !newValue.trim()) {
      return;
    }

    setIsSaving(true);
    try {
      await onCreate(newKey, newValue, newDescription || undefined);
      setCreateModalOpen(false);
      setNewKey('');
      setNewValue('');
      setNewDescription('');
    } finally {
      setIsSaving(false);
    }
  };

  const getCacheHitRateColor = (hitRate: number) => {
    if (hitRate >= 90) return 'green';
    if (hitRate >= 70) return 'yellow';
    return 'red';
  };

  const isAgenticSetting = (key: string) => {
    return key.startsWith('Agentic.');
  };

  return (
    <Stack gap="md">
      {/* Cache Statistics */}
      {cacheStats && (
        <Card shadow="sm" p="md" radius="md" withBorder>
          <Group justify="space-between" mb="md">
            <Group gap="xs">
              <IconChartBar size={20} />
              <Title order={4}>Cache Statistics</Title>
            </Group>
            <Button
              variant="light"
              leftSection={<IconRefresh size={16} />}
              onClick={() => void onReloadCache()}
              loading={isLoading}
              size="sm"
            >
              Reload Cache
            </Button>
          </Group>

          <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="md">
            <Paper p="md" withBorder>
              <Text size="xs" c="dimmed" mb={4}>Cache Size</Text>
              <Text size="xl" fw={700}>{cacheStats.cacheSize}</Text>
            </Paper>

            <Paper p="md" withBorder>
              <Text size="xs" c="dimmed" mb={4}>Hit Rate</Text>
              <Group gap="xs">
                <Text size="xl" fw={700}>{cacheStats.hitRate.toFixed(1)}%</Text>
                <Badge color={getCacheHitRateColor(cacheStats.hitRate)} variant="light" size="sm">
                  {(() => {
                    if (cacheStats.hitRate >= 90) return 'Excellent';
                    if (cacheStats.hitRate >= 70) return 'Good';
                    return 'Poor';
                  })()}
                </Badge>
              </Group>
            </Paper>

            <Paper p="md" withBorder>
              <Text size="xs" c="dimmed" mb={4}>Cache Hits</Text>
              <Text size="xl" fw={700}>{cacheStats.cacheHits.toLocaleString()}</Text>
            </Paper>

            <Paper p="md" withBorder>
              <Text size="xs" c="dimmed" mb={4}>Invalidations</Text>
              <Text size="xl" fw={700}>{cacheStats.invalidations.toLocaleString()}</Text>
            </Paper>
          </SimpleGrid>

          <Text size="xs" c="dimmed" mt="md">
            Last loaded: {new Date(cacheStats.lastLoadTime).toLocaleString()}
          </Text>
        </Card>
      )}

      {/* Function Discovery Cache Statistics */}
      {functionDiscoveryCacheStats && (
        <Card shadow="sm" p="md" radius="md" withBorder>
          <Group justify="space-between" mb="md">
            <Group gap="xs">
              <IconChartBar size={20} />
              <Title order={4}>Function Discovery Cache</Title>
            </Group>
            <Button
              variant="light"
              color="red"
              leftSection={<IconTrash size={16} />}
              onClick={() => void onInvalidateFunctionDiscoveryCache()}
              loading={isLoading}
              size="sm"
            >
              Invalidate Cache
            </Button>
          </Group>

          <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="md">
            <Paper p="md" withBorder>
              <Text size="xs" c="dimmed" mb={4}>Cache Enabled</Text>
              <Badge color={functionDiscoveryCacheStats.isEnabled ? 'green' : 'gray'} size="lg" variant="light">
                {functionDiscoveryCacheStats.isEnabled ? 'Enabled' : 'Disabled'}
              </Badge>
            </Paper>

            <Paper p="md" withBorder>
              <Text size="xs" c="dimmed" mb={4}>Cached Entries</Text>
              <Text size="xl" fw={700}>{functionDiscoveryCacheStats.totalEntries}</Text>
            </Paper>

            <Paper p="md" withBorder>
              <Text size="xs" c="dimmed" mb={4}>Cache Hits</Text>
              <Text size="xl" fw={700}>{functionDiscoveryCacheStats.cacheHits.toLocaleString()}</Text>
            </Paper>

            <Paper p="md" withBorder>
              <Text size="xs" c="dimmed" mb={4}>Cache Misses</Text>
              <Text size="xl" fw={700}>{functionDiscoveryCacheStats.cacheMisses.toLocaleString()}</Text>
            </Paper>
          </SimpleGrid>

          {functionDiscoveryCacheStats.isEnabled && (
            <Text size="xs" c="dimmed" mt="md">
              Configure per-function cache TTL in Function Configurations. Controlled by <Text span ff="monospace">Functions.DiscoveryCacheEnabled</Text> global setting.
            </Text>
          )}
          {!functionDiscoveryCacheStats.isEnabled && (
            <Text size="xs" c="orange" mt="md">
              Function discovery caching is disabled. Enable it via the <Text span ff="monospace">Functions.DiscoveryCacheEnabled</Text> global setting below.
            </Text>
          )}
        </Card>
      )}

      {/* Settings Table */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" mb="md">
          <Title order={4}>Global Settings ({settings.length})</Title>
          <Button
            variant="filled"
            leftSection={<IconPlus size={16} />}
            onClick={() => setCreateModalOpen(true)}
          >
            Create Setting
          </Button>
        </Group>

        <ScrollArea>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Key</Table.Th>
                <Table.Th>Value</Table.Th>
                <Table.Th>Description</Table.Th>
                <Table.Th>Updated</Table.Th>
                <Table.Th style={{ width: 100 }}>Actions</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {settings.map((setting) => {
                const isEditing = editingId === setting.id;
                const isAgentic = isAgenticSetting(setting.key);

                return (
                  <Table.Tr key={setting.id}>
                    <Table.Td>
                      <Group gap="xs">
                        <Text ff="monospace" size="sm">{setting.key}</Text>
                        {isAgentic && (
                          <Badge size="xs" variant="light" color="blue">Agentic</Badge>
                        )}
                      </Group>
                    </Table.Td>
                    <Table.Td>
                      {isEditing ? (
                        <TextInput
                          value={editValue}
                          onChange={(e) => setEditValue(e.currentTarget.value)}
                          size="sm"
                          disabled={isSaving}
                        />
                      ) : (
                        <Text ff="monospace" size="sm">{setting.value}</Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      {isEditing ? (
                        <Textarea
                          value={editDescription}
                          onChange={(e) => setEditDescription(e.currentTarget.value)}
                          size="sm"
                          minRows={1}
                          disabled={isSaving}
                        />
                      ) : (
                        <Text size="sm" c="dimmed">{setting.description ?? '—'}</Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <Text size="xs" c="dimmed">
                        {new Date(setting.updatedAt).toLocaleString()}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      {isEditing ? (
                        <Group gap="xs">
                          <Tooltip label="Save">
                            <ActionIcon
                              color="green"
                              variant="light"
                              onClick={() => void handleSave(setting.id)}
                              loading={isSaving}
                            >
                              <IconCheck size={16} />
                            </ActionIcon>
                          </Tooltip>
                          <Tooltip label="Cancel">
                            <ActionIcon
                              color="gray"
                              variant="light"
                              onClick={handleCancel}
                              disabled={isSaving}
                            >
                              <IconX size={16} />
                            </ActionIcon>
                          </Tooltip>
                        </Group>
                      ) : (
                        <Group gap="xs">
                          <Tooltip label="Edit">
                            <ActionIcon
                              color="blue"
                              variant="light"
                              onClick={() => handleEdit(setting)}
                            >
                              <IconEdit size={16} />
                            </ActionIcon>
                          </Tooltip>
                          <Tooltip label="Delete">
                            <ActionIcon
                              color="red"
                              variant="light"
                              onClick={() => void onDelete(setting.id, setting.key)}
                            >
                              <IconTrash size={16} />
                            </ActionIcon>
                          </Tooltip>
                        </Group>
                      )}
                    </Table.Td>
                  </Table.Tr>
                );
              })}
            </Table.Tbody>
          </Table>
        </ScrollArea>

        {settings.length === 0 && (
          <Text c="dimmed" ta="center" py="xl">
            No settings found. Click &quot;Create Setting&quot; to add your first setting.
          </Text>
        )}
      </Card>

      {/* Create Setting Modal */}
      <Modal
        opened={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        title="Create Global Setting"
        size="md"
      >
        <Stack gap="md">
          <TextInput
            label="Key"
            placeholder="e.g., Agentic.MaxIterations"
            value={newKey}
            onChange={(e) => setNewKey(e.currentTarget.value)}
            required
            disabled={isSaving}
            maxLength={100}
          />

          <TextInput
            label="Value"
            placeholder="Setting value"
            value={newValue}
            onChange={(e) => setNewValue(e.currentTarget.value)}
            required
            disabled={isSaving}
            maxLength={2000}
          />

          <Textarea
            label="Description"
            placeholder="Optional description"
            value={newDescription}
            onChange={(e) => setNewDescription(e.currentTarget.value)}
            minRows={2}
            disabled={isSaving}
            maxLength={500}
          />

          <Group justify="flex-end" gap="sm">
            <Button
              variant="light"
              onClick={() => setCreateModalOpen(false)}
              disabled={isSaving}
            >
              Cancel
            </Button>
            <Button
              onClick={() => void handleCreate()}
              loading={isSaving}
              disabled={!newKey.trim() || !newValue.trim()}
            >
              Create
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}
