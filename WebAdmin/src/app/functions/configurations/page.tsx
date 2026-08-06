'use client';

import { useState, useEffect, useCallback } from 'react';
import {
  Container,
  Title,
  Text,
  Button,
  Group,
  Stack,
  Table,
  Badge,
  Select,
  TextInput,
  Textarea,
  NumberInput,
  Checkbox,
  Modal,
  Card,
  LoadingOverlay,
  Alert,
  ActionIcon,
  Menu
} from '@mantine/core';
import {
  IconPlus,
  IconRefresh,
  IconEdit,
  IconTrash,
  IconFilter,
  IconDots,
  IconTestPipe,
} from '@tabler/icons-react';
import { notify } from '@/lib/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import { modals } from '@mantine/modals';
import {
  FunctionConfigurationDto,
  CreateFunctionConfigurationDto,
  UpdateFunctionConfigurationDto,
  FunctionProviderType,
  FunctionPurpose,
  FunctionExecutionMode,
  getProviderTypeName,
  getPurposeName,
  getExecutionModeName,
  getAvailableFunctionProviders,
} from '../types';
import { TestFunctionModal } from '@/components/functions/TestFunctionModal';

export default function FunctionConfigurationsPage() {
  const { executeWithAdmin } = useAdminClient();
  const [configurations, setConfigurations] = useState<FunctionConfigurationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingConfig, setEditingConfig] = useState<FunctionConfigurationDto | null>(null);
  const [filterProvider, setFilterProvider] = useState<string>('all');
  const [filterPurpose, setFilterPurpose] = useState<string>('all');
  const [testingConfig, setTestingConfig] = useState<FunctionConfigurationDto | null>(null);
  const [showTestModal, setShowTestModal] = useState(false);
  const [invalidatingCache, setInvalidatingCache] = useState(false);

  // Form state
  const [formData, setFormData] = useState<CreateFunctionConfigurationDto>({
    configurationName: '',
    providerType: FunctionProviderType.Exa,
    purpose: FunctionPurpose.Search,
    description: '',
    defaultExecutionMode: FunctionExecutionMode.Synchronous,
    timeoutSeconds: 30,
    isEnabled: true,
    baseUrl: '',
    providerSettings: '',
    parameterSchema: '',
  });

  const isMcp = formData.providerType === FunctionProviderType.Mcp;

  const loadConfigurations = useCallback(async () => {
    try {
      setLoading(true);
      const response = await executeWithAdmin(client =>
        client.functionConfigurations.list()
      );
      setConfigurations(response);
    } catch (err) {
      console.warn('Error loading configurations:', err);
      notify.error(err, 'Failed to load configurations');
    } finally {
      setLoading(false);
    }
  }, [executeWithAdmin]);

  useEffect(() => {
    void loadConfigurations();
  }, [loadConfigurations]);

  const handleCreate = async () => {
    try {
      await executeWithAdmin(client =>
        client.functionConfigurations.create(formData)
      );
      notify.success('Configuration created successfully');
      setShowModal(false);
      resetForm();

      // Reset filters to ensure the new configuration is visible
      setFilterProvider('all');
      setFilterPurpose('all');

      await loadConfigurations();
    } catch (err) {
      console.warn('Error creating configuration:', err);
      notify.error(err, 'Failed to create configuration');
    }
  };

  const handleUpdate = async () => {
    if (!editingConfig) return;

    try {
      const updateData: UpdateFunctionConfigurationDto = {
        configurationName: formData.configurationName,
        purpose: formData.purpose,
        description: formData.description,
        defaultExecutionMode: formData.defaultExecutionMode,
        timeoutSeconds: formData.timeoutSeconds,
        isEnabled: formData.isEnabled,
        baseUrl: formData.baseUrl,
        providerSettings: formData.providerSettings,
        parameterSchema: formData.parameterSchema,
      };
      await executeWithAdmin(client =>
        client.functionConfigurations.update(editingConfig.id, updateData)
      );
      notify.success('Configuration updated successfully');
      setShowModal(false);
      setEditingConfig(null);
      resetForm();
      await loadConfigurations();
    } catch (err) {
      console.warn('Error updating configuration:', err);
      notify.error(err, 'Failed to update configuration');
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await executeWithAdmin(client =>
        client.functionConfigurations.deleteById(id)
      );
      notify.success('Configuration deleted successfully');
      await loadConfigurations();
    } catch (err) {
      console.warn('Error deleting configuration:', err);
      notify.error(err, 'Failed to delete configuration');
    }
  };

  const handleToggleEnabled = async (config: FunctionConfigurationDto) => {
    try {
      await executeWithAdmin(client =>
        client.functionConfigurations.update(config.id, {
          isEnabled: !config.isEnabled,
        })
      );
      notify.success(`Configuration ${config.isEnabled ? 'disabled' : 'enabled'}`);
      await loadConfigurations();
    } catch (err) {
      console.warn('Error toggling configuration:', err);
      notify.error(err, 'Failed to toggle configuration');
    }
  };

  const openCreateModal = () => {
    resetForm();
    setEditingConfig(null);
    setShowModal(true);
  };

  const openEditModal = (config: FunctionConfigurationDto) => {
    setEditingConfig(config);
    setFormData({
      configurationName: config.configurationName,
      providerType: config.providerType,
      purpose: config.purpose,
      description: config.description ?? '',
      defaultExecutionMode: config.defaultExecutionMode,
      timeoutSeconds: config.timeoutSeconds ?? undefined,
      isEnabled: config.isEnabled,
      baseUrl: config.baseUrl ?? '',
      providerSettings: config.providerSettings ?? '',
      parameterSchema: config.parameterSchema ?? '',
    });
    setShowModal(true);
  };

  const openTestModal = (config: FunctionConfigurationDto) => {
    setTestingConfig(config);
    setShowTestModal(true);
  };

  const resetForm = () => {
    setFormData({
      configurationName: '',
      providerType: FunctionProviderType.Exa,
      purpose: FunctionPurpose.Search,
      description: '',
      defaultExecutionMode: FunctionExecutionMode.Synchronous,
      timeoutSeconds: 30,
      isEnabled: true,
      baseUrl: '',
      providerSettings: '',
      parameterSchema: '',
    });
  };

  const filteredConfigurations = (configurations ?? []).filter((config) => {
    if (filterProvider !== 'all' && config.providerType.toString() !== filterProvider) return false;
    if (filterPurpose !== 'all' && config.purpose.toString() !== filterPurpose) return false;
    return true;
  });

  const confirmFunctionCacheInvalidation = () => {
    modals.openConfirmModal({
      title: 'Invalidate function discovery cache?',
      children: (
        <Text size="sm">
          Cached function tool definitions will be cleared across Gateway instances and
          rebuilt on demand.
        </Text>
      ),
      labels: { confirm: 'Invalidate cache', cancel: 'Cancel' },
      confirmProps: { color: 'orange' },
      onConfirm: () => {
        void (async () => {
          setInvalidatingCache(true);
          try {
            await executeWithAdmin(client => client.system.invalidateFunctionDiscoveryCache());
            notify.success('Function discovery cache invalidation requested');
          } catch (error) {
            notify.error(error, 'Failed to invalidate function discovery cache');
          } finally {
            setInvalidatingCache(false);
          }
        })();
      },
    });
  };

  return (
    <Container size="xl">
      <Stack gap="md">
        <Group justify="space-between" align="flex-end">
          <div>
            <Title order={2}>Function Configurations</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Configure function providers and execution settings
            </Text>
          </div>
          <Group gap="xs">
            <Button
              variant="light"
              color="orange"
              onClick={confirmFunctionCacheInvalidation}
              loading={invalidatingCache}
            >
              Invalidate Function Discovery Cache
            </Button>
            <Button
              leftSection={<IconRefresh size={16} />}
              variant="subtle"
              onClick={() => void loadConfigurations()}
            >
              Refresh
            </Button>
            <Button
              leftSection={<IconPlus size={16} />}
              onClick={openCreateModal}
            >
              Create Configuration
            </Button>
          </Group>
        </Group>

        <Card withBorder>
          <Card.Section p="md" withBorder>
            <Group gap="md">
              <IconFilter size={20} />
              <Text fw={500}>Filters</Text>
            </Group>
          </Card.Section>

          <Card.Section p="md">
            <Group gap="md">
              <Select
                label="Provider"
                placeholder="All Providers"
                value={filterProvider}
                onChange={(value) => setFilterProvider(value ?? 'all')}
                data={[
                  { value: 'all', label: 'All Providers' },
                  ...getAvailableFunctionProviders().map(p => ({
                    value: p.value.toString(),
                    label: p.label
                  }))
                ]}
                style={{ flex: 1 }}
              />
              <Select
                label="Purpose"
                placeholder="All Purposes"
                value={filterPurpose}
                onChange={(value) => setFilterPurpose(value ?? 'all')}
                data={[
                  { value: 'all', label: 'All Purposes' },
                  { value: FunctionPurpose.Search.toString(), label: 'Search' },
                  { value: FunctionPurpose.Answer.toString(), label: 'Answer' },
                  { value: FunctionPurpose.ContentRetrieval.toString(), label: 'Content Retrieval' },
                  { value: FunctionPurpose.RAG.toString(), label: 'RAG' },
                ]}
                style={{ flex: 1 }}
              />
            </Group>
          </Card.Section>
        </Card>

        <Card withBorder>
          <LoadingOverlay visible={loading} />
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Name</Table.Th>
                <Table.Th>Provider</Table.Th>
                <Table.Th>Purpose</Table.Th>
                <Table.Th>Mode</Table.Th>
                <Table.Th>Timeout</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Created</Table.Th>
                <Table.Th>Actions</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {filteredConfigurations.length === 0 ? (
                <Table.Tr>
                  <Table.Td colSpan={8}>
                    <Text ta="center" c="dimmed" py="xl">
                      No configurations found
                    </Text>
                  </Table.Td>
                </Table.Tr>
              ) : (
                filteredConfigurations.map((config) => (
                  <Table.Tr key={config.id}>
                    <Table.Td>
                      <div>
                        <Text fw={500}>{config.configurationName}</Text>
                        {config.description && (
                          <Text size="sm" c="dimmed">{config.description}</Text>
                        )}
                      </div>
                    </Table.Td>
                    <Table.Td>{getProviderTypeName(config.providerType)}</Table.Td>
                    <Table.Td>{getPurposeName(config.purpose)}</Table.Td>
                    <Table.Td>{getExecutionModeName(config.defaultExecutionMode)}</Table.Td>
                    <Table.Td>{config.timeoutSeconds}s</Table.Td>
                    <Table.Td>
                      <Badge
                        color={config.isEnabled ? 'green' : 'gray'}
                        variant="light"
                        style={{ cursor: 'pointer' }}
                        onClick={() => void handleToggleEnabled(config)}
                      >
                        {config.isEnabled ? 'Enabled' : 'Disabled'}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" c="dimmed">
                        {new Date(config.createdAt).toLocaleDateString()}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Menu shadow="md" width={200}>
                        <Menu.Target>
                          <ActionIcon variant="subtle" color="gray">
                            <IconDots size={16} />
                          </ActionIcon>
                        </Menu.Target>
                        <Menu.Dropdown>
                          <Menu.Item
                            leftSection={<IconTestPipe size={14} />}
                            onClick={() => openTestModal(config)}
                          >
                            Test
                          </Menu.Item>
                          <Menu.Item
                            leftSection={<IconEdit size={14} />}
                            onClick={() => openEditModal(config)}
                          >
                            Edit
                          </Menu.Item>
                          <Menu.Item
                            leftSection={<IconTrash size={14} />}
                            color="red"
                            onClick={() => void handleDelete(config.id)}
                          >
                            Delete
                          </Menu.Item>
                        </Menu.Dropdown>
                      </Menu>
                    </Table.Td>
                  </Table.Tr>
                ))
              )}
            </Table.Tbody>
          </Table>
        </Card>
      </Stack>

      <Modal
        opened={showModal}
        onClose={() => {
          setShowModal(false);
          setEditingConfig(null);
          resetForm();
        }}
        title={editingConfig ? 'Edit Configuration' : 'Create Configuration'}
        size="lg"
      >
        <Stack gap="md">
          <TextInput
            label="Configuration Name"
            placeholder="e.g., Exa Production Search"
            value={formData.configurationName}
            onChange={(e) => setFormData({ ...formData, configurationName: e.target.value })}
            required
          />

          <Select
            label="Provider Type"
            value={formData.providerType.toString()}
            onChange={(value) => setFormData({ ...formData, providerType: value as FunctionProviderType })}
            data={getAvailableFunctionProviders().map(p => ({
              value: p.value.toString(),
              label: p.label
            }))}
            disabled={!!editingConfig}
            required
          />
          {editingConfig && (
            <Alert color="blue" variant="light">
              Provider type cannot be changed
            </Alert>
          )}

          {isMcp && (
            <TextInput
              label="MCP Server URL"
              placeholder="https://mcp.example.com/sse"
              value={formData.baseUrl ?? ''}
              onChange={(e) => setFormData({ ...formData, baseUrl: e.target.value })}
              description="Remote MCP server endpoint (Streamable HTTP / SSE). Its tools are discovered automatically."
              required
            />
          )}

          <Select
            label="Purpose"
            value={formData.purpose.toString()}
            onChange={(value) => setFormData({ ...formData, purpose: value as FunctionPurpose })}
            data={[
              { value: FunctionPurpose.Search.toString(), label: 'Search' },
              { value: FunctionPurpose.Answer.toString(), label: 'Answer' },
              { value: FunctionPurpose.ContentRetrieval.toString(), label: 'Content Retrieval' },
              { value: FunctionPurpose.RAG.toString(), label: 'RAG' },
            ]}
            required
          />

          <Textarea
            label="Description"
            placeholder="Optional description"
            value={formData.description}
            onChange={(e) => setFormData({ ...formData, description: e.target.value })}
            rows={3}
          />

          <Select
            label="Execution Mode"
            value={formData.defaultExecutionMode?.toString() ?? FunctionExecutionMode.Synchronous.toString()}
            onChange={(value) => setFormData({ ...formData, defaultExecutionMode: value as FunctionExecutionMode })}
            data={[
              { value: FunctionExecutionMode.Synchronous.toString(), label: 'Synchronous' },
              { value: FunctionExecutionMode.Asynchronous.toString(), label: 'Asynchronous' },
            ]}
          />

          <NumberInput
            label="Timeout (seconds)"
            value={formData.timeoutSeconds}
            onChange={(value) => setFormData({ ...formData, timeoutSeconds: Number(value) })}
            min={1}
          />

          <Checkbox
            label="Enabled"
            checked={formData.isEnabled}
            onChange={(e) => setFormData({ ...formData, isEnabled: e.currentTarget.checked })}
          />

          <Textarea
            label={isMcp ? 'MCP Settings (JSON)' : 'Metadata (JSON)'}
            placeholder={isMcp
              ? '{"allowedTools": null, "authScheme": "Bearer", "authHeader": "Authorization", "allowPrivateNetwork": false}'
              : '{}'}
            value={formData.providerSettings}
            onChange={(e) => setFormData({ ...formData, providerSettings: e.target.value })}
            rows={4}
            styles={{ input: { fontFamily: 'monospace' } }}
            description={isMcp
              ? 'Optional. "allowedTools": null exposes every tool the server advertises, or list names to restrict. Configure the server token in Function Credentials (scoped to this configuration).'
              : undefined}
          />

          {!isMcp && (
            <Textarea
              label="Parameter Schema (JSON Schema)"
              placeholder='{"type": "object", "properties": {...}, "required": [...]}'
              value={formData.parameterSchema ?? ''}
              onChange={(e) => setFormData({ ...formData, parameterSchema: e.target.value })}
              rows={8}
              styles={{ input: { fontFamily: 'monospace', fontSize: 12 } }}
              description="JSON Schema defining the function parameters that the LLM can use"
            />
          )}

          <Group justify="flex-end" mt="md">
            <Button
              variant="subtle"
              onClick={() => {
                setShowModal(false);
                setEditingConfig(null);
                resetForm();
              }}
            >
              Cancel
            </Button>
            <Button
              onClick={() => void (editingConfig ? handleUpdate() : handleCreate())}
            >
              {editingConfig ? 'Update' : 'Create'}
            </Button>
          </Group>
        </Stack>
      </Modal>

      {testingConfig && (
        <TestFunctionModal
          opened={showTestModal}
          onClose={() => {
            setShowTestModal(false);
            setTestingConfig(null);
          }}
          configuration={testingConfig}
        />
      )}
    </Container>
  );
}
