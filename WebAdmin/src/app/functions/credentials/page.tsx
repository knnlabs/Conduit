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
  PasswordInput,
  NumberInput,
  Checkbox,
  Modal,
  Card,
  LoadingOverlay,
  Alert,
  ActionIcon,
  Menu,
  Code,
} from '@mantine/core';
import {
  IconPlus,
  IconRefresh,
  IconEdit,
  IconTrash,
  IconDots,
  IconTestPipe,
  IconKey,
} from '@tabler/icons-react';
import { notify } from '@/lib/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import {
  FunctionCredentialDto,
  CreateFunctionCredentialDto,
  UpdateFunctionCredentialDto,
  FunctionConfigurationDto,
  FunctionProviderType,
  getProviderTypeName,
  getAvailableFunctionProviders,
} from '../types';

const GLOBAL_SCOPE = 'global';

interface CredentialFormState {
  providerType: FunctionProviderType;
  keyName: string;
  apiKey: string;
  baseUrl: string;
  functionConfigurationId?: number;
  functionAccountGroup: number;
  isPrimary: boolean;
  isEnabled: boolean;
}

const emptyForm: CredentialFormState = {
  providerType: FunctionProviderType.Exa,
  keyName: '',
  apiKey: '',
  baseUrl: '',
  functionConfigurationId: undefined,
  functionAccountGroup: 0,
  isPrimary: false,
  isEnabled: true,
};

export default function FunctionCredentialsPage() {
  const { executeWithAdmin } = useAdminClient();
  const [credentials, setCredentials] = useState<FunctionCredentialDto[]>([]);
  const [configurations, setConfigurations] = useState<FunctionConfigurationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState<FunctionCredentialDto | null>(null);
  const [form, setForm] = useState<CredentialFormState>(emptyForm);
  const [testingId, setTestingId] = useState<number | null>(null);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      const creds = await executeWithAdmin(client => client.functionCredentials.list());
      const configs = await executeWithAdmin(client => client.functionConfigurations.list());
      setCredentials(creds);
      setConfigurations(configs);
    } catch (err) {
      console.warn('Error loading credentials:', err);
      notify.error(err, 'Failed to load credentials');
    } finally {
      setLoading(false);
    }
  }, [executeWithAdmin]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const configName = (id?: number | null): string => {
    if (id === null || id === undefined) return '';
    return configurations.find(c => c.id === id)?.configurationName ?? `#${id}`;
  };

  // Configurations selectable as a scope target for the currently-chosen provider type.
  const scopeConfigOptions = configurations
    .filter(c => c.providerType === form.providerType)
    .map(c => ({ value: c.id.toString(), label: c.configurationName }));

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm);
    setShowModal(true);
  };

  const openEdit = (credential: FunctionCredentialDto) => {
    setEditing(credential);
    setForm({
      providerType: credential.providerType,
      keyName: credential.keyName ?? '',
      apiKey: '', // never surface the stored secret; blank = keep unchanged
      baseUrl: credential.baseUrl ?? '',
      functionConfigurationId: credential.functionConfigurationId ?? undefined,
      functionAccountGroup: credential.functionAccountGroup,
      isPrimary: credential.isPrimary,
      isEnabled: credential.isEnabled,
    });
    setShowModal(true);
  };

  const closeModal = () => {
    setShowModal(false);
    setEditing(null);
    setForm(emptyForm);
  };

  const handleProviderChange = (value: string | null) => {
    const providerType = value as FunctionProviderType;
    // MCP credentials are per-server, so default them to config-scoped.
    const scopeToConfig = providerType === FunctionProviderType.Mcp;
    setForm({
      ...form,
      providerType,
      functionConfigurationId: scopeToConfig ? form.functionConfigurationId : undefined,
    });
  };

  const handleScopeChange = (value: string | null) => {
    if (value === GLOBAL_SCOPE || value === null) {
      setForm({ ...form, functionConfigurationId: undefined });
    } else {
      setForm({ ...form, functionConfigurationId: Number(value) });
    }
  };

  const validate = (): string | null => {
    if (!form.keyName.trim()) return 'Key name is required';
    if (!editing && !form.apiKey.trim()) return 'API key / token is required';
    return null;
  };

  const handleCreate = async () => {
    const error = validate();
    if (error) {
      notify.error(new Error(error), error);
      return;
    }
    try {
      const payload: CreateFunctionCredentialDto = {
        providerType: form.providerType,
        keyName: form.keyName.trim(),
        apiKey: form.apiKey,
        baseUrl: form.baseUrl.trim() || undefined,
        functionConfigurationId: form.functionConfigurationId,
        functionAccountGroup: form.functionAccountGroup,
        isPrimary: form.isPrimary,
        isEnabled: form.isEnabled,
      };
      await executeWithAdmin(client => client.functionCredentials.create(payload));
      notify.success('Credential created successfully');
      closeModal();
      await loadData();
    } catch (err) {
      console.warn('Error creating credential:', err);
      notify.error(err, 'Failed to create credential');
    }
  };

  const handleUpdate = async () => {
    if (!editing) return;
    const error = validate();
    if (error) {
      notify.error(new Error(error), error);
      return;
    }
    try {
      // A blank token means "keep the existing secret": re-send the stored (encrypted) value,
      // which the server treats as already-protected and leaves unchanged. baseUrl/organization
      // are round-tripped so the entity-binding update does not wipe them.
      const payload: UpdateFunctionCredentialDto = {
        id: editing.id,
        keyName: form.keyName.trim(),
        apiKey: form.apiKey.trim() ? form.apiKey : (editing.apiKey ?? undefined),
        baseUrl: form.baseUrl.trim() || null,
        organization: editing.organization ?? null,
        functionAccountGroup: form.functionAccountGroup,
        isPrimary: form.isPrimary,
        isEnabled: form.isEnabled,
      };
      await executeWithAdmin(client => client.functionCredentials.update(editing.id, payload));
      notify.success('Credential updated successfully');
      closeModal();
      await loadData();
    } catch (err) {
      console.warn('Error updating credential:', err);
      notify.error(err, 'Failed to update credential');
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await executeWithAdmin(client => client.functionCredentials.deleteById(id));
      notify.success('Credential deleted successfully');
      await loadData();
    } catch (err) {
      console.warn('Error deleting credential:', err);
      notify.error(err, 'Failed to delete credential');
    }
  };

  const handleTest = async (credential: FunctionCredentialDto) => {
    setTestingId(credential.id);
    try {
      const result = await executeWithAdmin(client =>
        client.functionCredentials.testCredential({ credentialId: credential.id })
      );
      if (result.success) {
        notify.success(result.message ?? 'Credential is valid');
      } else {
        notify.error(new Error(result.message ?? 'Test failed'), result.message ?? 'Credential test failed');
      }
    } catch (err) {
      console.warn('Error testing credential:', err);
      notify.error(err, 'Failed to test credential');
    } finally {
      setTestingId(null);
    }
  };

  const providerOptions = getAvailableFunctionProviders().map(p => ({
    value: p.value.toString(),
    label: p.label,
  }));

  const isMcp = form.providerType === FunctionProviderType.Mcp;
  const scopeValue = form.functionConfigurationId?.toString() ?? GLOBAL_SCOPE;

  return (
    <Container size="xl">
      <Stack gap="md">
        <Group justify="space-between" align="flex-end">
          <div>
            <Title order={2}>Function Credentials</Title>
            <Text c="dimmed" size="sm" mt={4}>
              API keys and MCP server tokens for function providers. Secrets are write-only and stored securely.
            </Text>
          </div>
          <Group gap="xs">
            <Button leftSection={<IconRefresh size={16} />} variant="subtle" onClick={() => void loadData()}>
              Refresh
            </Button>
            <Button leftSection={<IconPlus size={16} />} onClick={openCreate}>
              Add Credential
            </Button>
          </Group>
        </Group>

        <Card withBorder>
          <LoadingOverlay visible={loading} />
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Key Name</Table.Th>
                <Table.Th>Provider</Table.Th>
                <Table.Th>Scope</Table.Th>
                <Table.Th>Primary</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Group</Table.Th>
                <Table.Th>Actions</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {credentials.length === 0 ? (
                <Table.Tr>
                  <Table.Td colSpan={7}>
                    <Text ta="center" c="dimmed" py="xl">
                      No credentials found
                    </Text>
                  </Table.Td>
                </Table.Tr>
              ) : (
                credentials.map((credential) => (
                  <Table.Tr key={credential.id}>
                    <Table.Td>
                      <Group gap="xs">
                        <IconKey size={14} />
                        <Text fw={500}>{credential.keyName ?? `Credential #${credential.id}`}</Text>
                      </Group>
                    </Table.Td>
                    <Table.Td>{getProviderTypeName(credential.providerType)}</Table.Td>
                    <Table.Td>
                      {credential.functionConfigurationId ? (
                        <Badge variant="light" color="grape">{configName(credential.functionConfigurationId)}</Badge>
                      ) : (
                        <Badge variant="light" color="gray">Provider-global</Badge>
                      )}
                    </Table.Td>
                    <Table.Td>
                      {credential.isPrimary ? <Badge color="blue" variant="light">Primary</Badge> : <Text c="dimmed" size="sm">—</Text>}
                    </Table.Td>
                    <Table.Td>
                      <Badge color={credential.isEnabled ? 'green' : 'gray'} variant="light">
                        {credential.isEnabled ? 'Enabled' : 'Disabled'}
                      </Badge>
                    </Table.Td>
                    <Table.Td><Text size="sm" c="dimmed">{credential.functionAccountGroup}</Text></Table.Td>
                    <Table.Td>
                      <Menu shadow="md" width={180}>
                        <Menu.Target>
                          <ActionIcon variant="subtle" color="gray" loading={testingId === credential.id}>
                            <IconDots size={16} />
                          </ActionIcon>
                        </Menu.Target>
                        <Menu.Dropdown>
                          <Menu.Item leftSection={<IconTestPipe size={14} />} onClick={() => void handleTest(credential)}>
                            Test
                          </Menu.Item>
                          <Menu.Item leftSection={<IconEdit size={14} />} onClick={() => openEdit(credential)}>
                            Edit
                          </Menu.Item>
                          <Menu.Item leftSection={<IconTrash size={14} />} color="red" onClick={() => void handleDelete(credential.id)}>
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

      <Modal opened={showModal} onClose={closeModal} title={editing ? 'Edit Credential' : 'Add Credential'} size="lg">
        <Stack gap="md">
          <Select
            label="Provider Type"
            value={form.providerType.toString()}
            onChange={handleProviderChange}
            data={providerOptions}
            disabled={!!editing}
            required
          />

          {editing ? (
            <TextInput
              label="Scope"
              value={editing.functionConfigurationId ? configName(editing.functionConfigurationId) : 'Provider-global'}
              description="Scope is fixed when the credential is created"
              disabled
            />
          ) : (
            <>
              <Select
                label="Scope"
                value={scopeValue}
                onChange={handleScopeChange}
                data={[
                  { value: GLOBAL_SCOPE, label: 'Provider-global (shared by all configurations)' },
                  ...scopeConfigOptions,
                ]}
                description={isMcp
                  ? 'MCP tokens should be scoped to a single server configuration.'
                  : 'Global credentials are shared across all configurations of this provider.'}
              />
              {isMcp && scopeValue === GLOBAL_SCOPE && (
                <Alert color="yellow" variant="light">
                  MCP servers each have their own token — select the configuration this token belongs to.
                </Alert>
              )}
            </>
          )}

          <TextInput
            label="Key Name"
            placeholder="e.g., Acme MCP token"
            value={form.keyName}
            onChange={(e) => setForm({ ...form, keyName: e.target.value })}
            required
          />

          <PasswordInput
            label={isMcp ? 'Server Token' : 'API Key'}
            placeholder={editing ? '•••••••• (leave blank to keep current)' : 'Enter the secret'}
            value={form.apiKey}
            onChange={(e) => setForm({ ...form, apiKey: e.target.value })}
            description={editing ? 'Leave blank to keep the existing secret.' : undefined}
            required={!editing}
          />

          <TextInput
            label="Base URL override (optional)"
            placeholder="Overrides the configuration's URL for this credential"
            value={form.baseUrl}
            onChange={(e) => setForm({ ...form, baseUrl: e.target.value })}
          />

          <NumberInput
            label="Account Group"
            description="0–32, groups credentials that share an upstream account/quota"
            value={form.functionAccountGroup}
            onChange={(value) => setForm({ ...form, functionAccountGroup: Number(value) })}
            min={0}
            max={32}
          />

          <Group gap="xl">
            <Checkbox
              label="Primary"
              checked={form.isPrimary}
              onChange={(e) => setForm({ ...form, isPrimary: e.currentTarget.checked })}
            />
            <Checkbox
              label="Enabled"
              checked={form.isEnabled}
              onChange={(e) => setForm({ ...form, isEnabled: e.currentTarget.checked })}
            />
          </Group>

          {isMcp && (
            <Alert color="blue" variant="light">
              For an open MCP server that needs no authentication, you can skip creating a credential — just
              configure the server URL under <Code>Configurations</Code>.
            </Alert>
          )}

          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={closeModal}>Cancel</Button>
            <Button onClick={() => void (editing ? handleUpdate() : handleCreate())}>
              {editing ? 'Update' : 'Create'}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Container>
  );
}
