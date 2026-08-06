'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import {
  Alert,
  Badge,
  Button,
  Card,
  Container,
  Group,
  LoadingOverlay,
  Modal,
  NumberInput,
  Select,
  SimpleGrid,
  Stack,
  Switch,
  Table,
  Text,
  TextInput,
  Textarea,
  Title,
} from '@mantine/core';
import { modals } from '@mantine/modals';
import {
  IconAlertTriangle,
  IconExternalLink,
  IconPlus,
  IconRefresh,
  IconSettings,
  IconTrash,
} from '@tabler/icons-react';
import type { components } from '@/generated/admin-api';
import type {
  GlobalSettingDefinitionDto,
  GlobalSettingDto,
} from '@/lib/admin-api';
import { withAdminClient } from '@/lib/client/adminClient';
import { notify } from '@/lib/notifications';

type RoutingDefaultsDto = components['schemas']['RoutingDefaultsDto'];
type AdvancedType = 'string' | 'json';

const routingKeys = new Set(['Routing.Defaults', 'Routing.Chat.Enabled']);

function looksLikeJson(value: string): boolean {
  const trimmed = value.trim();
  if (!(trimmed.startsWith('{') || trimmed.startsWith('['))) return false;
  try {
    JSON.parse(trimmed);
    return true;
  } catch {
    return false;
  }
}

export default function SettingsPage() {
  const [settings, setSettings] = useState<GlobalSettingDto[]>([]);
  const [definitions, setDefinitions] = useState<GlobalSettingDefinitionDto[]>([]);
  const [values, setValues] = useState<Record<string, string>>({});
  const [routing, setRouting] = useState<RoutingDefaultsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [savingKey, setSavingKey] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [editingCustom, setEditingCustom] = useState<GlobalSettingDto | null>(null);
  const [customKey, setCustomKey] = useState('');
  const [customValue, setCustomValue] = useState('');
  const [customDescription, setCustomDescription] = useState('');
  const [customType, setCustomType] = useState<AdvancedType>('string');
  const [customError, setCustomError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [settingsResult, definitionResult, routingResult] = await Promise.all([
        withAdminClient(client => client.settings.getGlobalSettings()),
        withAdminClient(client => client.settings.getDefinitions()),
        withAdminClient(client => client.configuration.getRoutingDefaults()),
      ]);
      setSettings(settingsResult.settings);
      setDefinitions(definitionResult);
      setRouting(routingResult);
      const persisted = new Map(settingsResult.settings.map(item => [item.key, item.value]));
      setValues(Object.fromEntries(
        definitionResult.map(definition => [
          definition.key,
          persisted.get(definition.key) ?? definition.defaultValue,
        ]),
      ));
    } catch (cause) {
      const message = cause instanceof Error ? cause.message : 'Unable to load settings';
      setError(message);
      notify.error(cause, 'Failed to load settings');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const definitionKeys = useMemo(
    () => new Set(definitions.map(definition => definition.key)),
    [definitions],
  );
  const editableDefinitions = definitions.filter(
    definition => !definition.isFeatureOwned && !routingKeys.has(definition.key),
  );
  const featureDefinitions = definitions.filter(definition => definition.isFeatureOwned);
  const customSettings = settings.filter(
    setting => setting.key !== 'WebAdmin_VirtualKey' && !definitionKeys.has(setting.key),
  );

  const saveTypedSetting = async (definition: GlobalSettingDefinitionDto) => {
    const value = values[definition.key] ?? definition.defaultValue;
    const minValue = Number(values['Agentic.MinIterations'] ?? '1');
    const maxValue = Number(values['Agentic.MaxIterations'] ?? '5');
    if (minValue > maxValue) {
      setError('Minimum agentic iterations cannot be greater than the maximum.');
      return;
    }

    setSavingKey(definition.key);
    setError(null);
    try {
      await withAdminClient(client =>
        client.settings.updateGlobalSetting(
          definition.key,
          value,
          definition.description,
        ));
      notify.success(`${definition.displayName} updated`);
      await load();
    } catch (cause) {
      notify.error(cause, `Failed to update ${definition.displayName}`);
    } finally {
      setSavingKey(null);
    }
  };

  const saveRouting = async () => {
    if (!routing) return;
    const weightTotal = Number(routing.costWeight ?? 0)
      + Number(routing.speedWeight ?? 0)
      + Number(routing.qualityWeight ?? 0);
    if (weightTotal <= 0) {
      setError('At least one routing score weight must be positive.');
      return;
    }
    setSavingKey('Routing.Defaults');
    try {
      await withAdminClient(client => client.configuration.updateRoutingDefaults(routing));
      notify.success('Structured routing defaults updated');
      await load();
    } catch (cause) {
      notify.error(cause, 'Failed to update routing defaults');
    } finally {
      setSavingKey(null);
    }
  };

  const openAdvanced = (setting?: GlobalSettingDto) => {
    setEditingCustom(setting ?? null);
    setCustomKey(setting?.key ?? '');
    setCustomValue(setting?.value ?? '');
    setCustomDescription(setting?.description ?? '');
    setCustomType(setting && looksLikeJson(setting.value) ? 'json' : 'string');
    setCustomError(null);
    setAdvancedOpen(true);
  };

  const saveAdvanced = async () => {
    if (!customKey.trim()) {
      setCustomError('A key is required.');
      return;
    }
    if (customType === 'json') {
      try {
        JSON.parse(customValue);
      } catch {
        setCustomError('The value must be valid JSON.');
        return;
      }
    }

    setSavingKey(customKey);
    try {
      if (editingCustom) {
        await withAdminClient(client =>
          client.settings.updateGlobalSetting(
            editingCustom.key,
            customValue,
            customDescription || undefined,
          ));
      } else {
        await withAdminClient(client =>
          client.settings.createGlobalSetting({
            key: customKey.trim(),
            value: customValue,
            description: customDescription || undefined,
          }));
      }
      setAdvancedOpen(false);
      notify.success(`Custom setting ${editingCustom ? 'updated' : 'created'}`);
      await load();
    } catch (cause) {
      notify.error(cause, 'Failed to save custom setting');
    } finally {
      setSavingKey(null);
    }
  };

  const confirmDelete = (setting: GlobalSettingDto) => {
    modals.openConfirmModal({
      title: 'Delete custom setting?',
      children: (
        <Text size="sm">
          Delete <strong>{setting.key}</strong>? Services using it will fall back to
          their defaults. This cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete setting', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => {
        void (async () => {
          try {
            await withAdminClient(client => client.settings.deleteGlobalSetting(setting.key));
            notify.success('Custom setting deleted');
            await load();
          } catch (cause) {
            notify.error(cause, 'Failed to delete custom setting');
          }
        })();
      },
    });
  };

  const confirmReload = () => {
    modals.openConfirmModal({
      title: 'Reload global settings caches?',
      children: (
        <Text size="sm">
          Every Admin and Gateway instance will reload global settings from the database.
        </Text>
      ),
      labels: { confirm: 'Reload cluster caches', cancel: 'Cancel' },
      onConfirm: () => {
        void (async () => {
          try {
            const accepted = await withAdminClient(client => client.settings.reloadCache());
            notify.success(accepted.message);
          } catch (cause) {
            notify.error(cause, 'Failed to request cache reload');
          }
        })();
      },
    });
  };

  return (
    <Container size="xl">
      <Stack gap="lg" pos="relative">
        <LoadingOverlay visible={loading} />
        <Group justify="space-between">
          <div>
            <Title order={2}>Settings</Title>
            <Text c="dimmed">Typed global configuration and cluster maintenance</Text>
          </div>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={() => void load()}
          >
            Refresh
          </Button>
        </Group>

        {error && (
          <Alert color="red" icon={<IconAlertTriangle size={16} />}>
            {error}
          </Alert>
        )}

        <Card withBorder>
          <Title order={3} mb="md">Application settings</Title>
          <Stack gap="lg">
            {editableDefinitions.map(definition => (
              <Group key={definition.key} justify="space-between" align="flex-end" wrap="nowrap">
                <div style={{ flex: 1 }}>
                  <Text fw={600}>{definition.displayName}</Text>
                  <Text size="sm" c="dimmed">{definition.description}</Text>
                  <Text size="xs" c="dimmed">{definition.key}</Text>
                </div>
                {definition.type === 'boolean' ? (
                  <Switch
                    checked={(values[definition.key] ?? definition.defaultValue) === 'true'}
                    onChange={event => setValues(current => ({
                      ...current,
                      [definition.key]: String(event.currentTarget.checked),
                    }))}
                  />
                ) : (
                  <NumberInput
                    w={150}
                    value={Number(values[definition.key] ?? definition.defaultValue)}
                    min={definition.minimum ?? undefined}
                    max={definition.maximum ?? undefined}
                    onChange={value => setValues(current => ({
                      ...current,
                      [definition.key]: String(value),
                    }))}
                  />
                )}
                <Button
                  size="xs"
                  onClick={() => void saveTypedSetting(definition)}
                  loading={savingKey === definition.key}
                >
                  Save
                </Button>
              </Group>
            ))}
          </Stack>
        </Card>

        {routing && (
          <Card withBorder>
            <Title order={3}>Structured routing defaults</Title>
            <Text size="sm" c="dimmed" mb="md">
              Default provider-aware policy. Alias-specific policies continue to override it.
            </Text>
            <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
              <Switch
                label="Chat routing enabled"
                checked={routing.chatRoutingEnabled ?? true}
                onChange={event => setRouting({
                  ...routing,
                  chatRoutingEnabled: event.currentTarget.checked,
                })}
              />
              <Switch
                label="Cache affinity"
                checked={routing.cacheAffinityEnabled ?? true}
                onChange={event => setRouting({
                  ...routing,
                  cacheAffinityEnabled: event.currentTarget.checked,
                })}
              />
              {([
                ['costWeight', 'Cost weight', 0, 1],
                ['speedWeight', 'Speed weight', 0, 1],
                ['qualityWeight', 'Quality weight', 0, 1],
                ['maxAffinityScorePenalty', 'Max affinity penalty', 0, 1],
                ['affinityTtlSeconds', 'Affinity TTL (seconds)', 1, 86400],
                ['mappingPriority', 'Mapping priority', undefined, undefined],
                ['mappingWeight', 'Mapping weight', 0.1, 2],
              ] as const).map(([key, label, min, max]) => (
                <NumberInput
                  key={key}
                  label={label}
                  value={Number(routing[key] ?? 0)}
                  min={min}
                  max={max}
                  decimalScale={key.includes('Weight') || key.includes('Penalty') ? 2 : 0}
                  onChange={value => setRouting({ ...routing, [key]: Number(value) })}
                />
              ))}
            </SimpleGrid>
            <Group justify="flex-end" mt="md">
              <Button
                onClick={() => void saveRouting()}
                loading={savingKey === 'Routing.Defaults'}
              >
                Save routing defaults
              </Button>
            </Group>
          </Card>
        )}

        <Card withBorder>
          <Title order={3}>Feature-owned settings</Title>
          <Text size="sm" c="dimmed" mb="md">
            These settings are edited on the feature page that validates their full policy.
          </Text>
          <SimpleGrid cols={{ base: 1, md: 2 }}>
            {featureDefinitions.map(definition => (
              <Card key={definition.key} withBorder padding="sm">
                <Group justify="space-between" wrap="nowrap">
                  <div>
                    <Text fw={600}>{definition.displayName}</Text>
                    <Text size="xs" c="dimmed">{definition.key}</Text>
                  </div>
                  {definition.featureRoute && (
                    <Button
                      component={Link}
                      href={definition.featureRoute}
                      variant="subtle"
                      size="xs"
                      rightSection={<IconExternalLink size={14} />}
                    >
                      Configure
                    </Button>
                  )}
                </Group>
              </Card>
            ))}
          </SimpleGrid>
        </Card>

        <Card withBorder>
          <Group justify="space-between" mb="md">
            <div>
              <Title order={3}>Advanced custom settings</Title>
              <Text size="sm" c="dimmed">
                Unregistered string and JSON settings. Changes take effect cluster-wide.
              </Text>
            </div>
            <Button
              leftSection={<IconPlus size={16} />}
              variant="light"
              onClick={() => openAdvanced()}
            >
              Add custom setting
            </Button>
          </Group>
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Key</Table.Th>
                <Table.Th>Type</Table.Th>
                <Table.Th>Value</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {customSettings.map(setting => (
                <Table.Tr key={setting.key}>
                  <Table.Td>{setting.key}</Table.Td>
                  <Table.Td>
                    <Badge variant="light">{looksLikeJson(setting.value) ? 'JSON' : 'String'}</Badge>
                  </Table.Td>
                  <Table.Td>
                    <Text lineClamp={1} maw={500}>{setting.value}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Group justify="flex-end" gap="xs">
                      <Button size="xs" variant="subtle" onClick={() => openAdvanced(setting)}>
                        Edit
                      </Button>
                      <Button
                        size="xs"
                        variant="subtle"
                        color="red"
                        leftSection={<IconTrash size={14} />}
                        onClick={() => confirmDelete(setting)}
                      >
                        Delete
                      </Button>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Card>

        <Card withBorder>
          <Group justify="space-between">
            <div>
              <Title order={3}>Cluster maintenance</Title>
              <Text size="sm" c="dimmed">
                Reload process-local global settings caches on every Admin and Gateway instance.
              </Text>
            </div>
            <Button
              variant="light"
              leftSection={<IconSettings size={16} />}
              onClick={confirmReload}
            >
              Reload global settings caches
            </Button>
          </Group>
        </Card>
      </Stack>

      <Modal
        opened={advancedOpen}
        onClose={() => setAdvancedOpen(false)}
        title={editingCustom ? 'Edit custom setting' : 'Add custom setting'}
        size="lg"
      >
        <Stack>
          {customError && <Alert color="red">{customError}</Alert>}
          <TextInput
            label="Key"
            value={customKey}
            disabled={Boolean(editingCustom)}
            onChange={event => setCustomKey(event.currentTarget.value)}
          />
          <Select
            label="Value type"
            value={customType}
            data={[
              { value: 'string', label: 'String' },
              { value: 'json', label: 'JSON' },
            ]}
            onChange={value => setCustomType((value ?? 'string') as AdvancedType)}
          />
          <Textarea
            label="Value"
            minRows={customType === 'json' ? 8 : 3}
            autosize
            value={customValue}
            onChange={event => setCustomValue(event.currentTarget.value)}
          />
          <TextInput
            label="Description"
            value={customDescription}
            onChange={event => setCustomDescription(event.currentTarget.value)}
          />
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setAdvancedOpen(false)}>Cancel</Button>
            <Button onClick={() => void saveAdvanced()} loading={savingKey === customKey}>
              Save
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Container>
  );
}
