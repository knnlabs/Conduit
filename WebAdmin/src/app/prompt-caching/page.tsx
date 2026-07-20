'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActionIcon, Alert, Badge, Button, Container, Group, NumberInput, Paper,
  Select, Skeleton, Stack, Switch, Text, TextInput, Title,
} from '@mantine/core';
import { IconPlus, IconTrash } from '@tabler/icons-react';
import { notify } from '@/lib/notifications';
import { withAdminClient } from '@/lib/client/adminClient';

type Strategy = 'OpenRouterAutomatic' | 'OpenRouterExplicit';
interface InjectionPoint { role?: 'system' | 'user' | 'assistant' | null; index?: number | null }
interface Rule {
  name: string; enabled: boolean; provider: 'OpenRouter'; modelPattern: string;
  strategy: Strategy; ttl?: '5m' | '1h' | null; injectionPoints: InjectionPoint[];
}
interface Config { schemaVersion: 2; enabled: boolean; rules: Rule[] }
interface Capability {
  provider: string; modelPattern: string; strategies: Strategy[]; ttls: string[];
  minimumTokens?: number | null; maxBreakpoints: number; providerManaged: boolean;
}

const emptyConfig: Config = { schemaVersion: 2, enabled: false, rules: [] };

export default function PromptCachingPage() {
  const [config, setConfig] = useState<Config>(emptyConfig);
  const [capabilities, setCapabilities] = useState<Capability[]>([]);
  const [snapshot, setSnapshot] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [legacyError, setLegacyError] = useState<string | null>(null);

  const managedCapabilities = useMemo(() => capabilities.filter(c => !c.providerManaged), [capabilities]);
  const providerManaged = useMemo(() => capabilities.filter(c => c.providerManaged), [capabilities]);
  const isDirty = JSON.stringify(config) !== snapshot;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [rawConfig, rawCapabilities] = await Promise.all([
        withAdminClient(client => client.configuration.getPromptCachingConfig()),
        withAdminClient(client => {
          const service = client.configuration as unknown as {
            getPromptCachingCapabilities: () => Promise<Capability[]>;
          };
          return service.getPromptCachingCapabilities();
        }),
      ]);
      const next = rawConfig as unknown as Config;
      setConfig(next);
      setSnapshot(JSON.stringify(next));
      setCapabilities(rawCapabilities);
      setLegacyError(null);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Stored configuration must be replaced.';
      setConfig(emptyConfig);
      setSnapshot('');
      setLegacyError(message);
      try {
        const rawCapabilities = await withAdminClient(client => {
          const service = client.configuration as unknown as {
            getPromptCachingCapabilities: () => Promise<Capability[]>;
          };
          return service.getPromptCachingCapabilities();
        });
        setCapabilities(rawCapabilities);
      } catch { /* retain an empty catalog and surface the configuration error */ }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const updateRule = (index: number, update: Partial<Rule>) => {
    setConfig(current => ({
      ...current,
      rules: current.rules.map((rule, i) => i === index ? { ...rule, ...update } : rule),
    }));
  };

  const addRule = () => {
    const capability = managedCapabilities[0];
    if (!capability) return;
    const strategy = capability.strategies[0];
    setConfig(current => ({
      ...current,
      enabled: true,
      rules: [...current.rules, {
        name: `Caching rule ${current.rules.length + 1}`,
        enabled: true,
        provider: 'OpenRouter',
        modelPattern: capability.modelPattern,
        strategy,
        ttl: (capability.ttls[0] as '5m' | '1h' | undefined) ?? null,
        injectionPoints: strategy === 'OpenRouterExplicit' ? [{ role: 'system', index: 0 }] : [],
      }],
    }));
  };

  const save = async () => {
    setSaving(true);
    try {
      const raw = await withAdminClient(client =>
        client.configuration.updatePromptCachingConfig(config as never));
      const next = raw as unknown as Config;
      setConfig(next);
      setSnapshot(JSON.stringify(next));
      setLegacyError(null);
      notify.success('Provider-aware prompt caching policy saved.', 'Saved');
    } catch (error) {
      notify.error(error instanceof Error ? error.message : 'Failed to save prompt caching policy.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <Container size="lg" py="xl"><Skeleton height={280} /></Container>;

  return (
    <Container size="lg" py="xl">
      <Stack gap="lg">
        <div>
          <Group gap="sm"><Title order={2}>Prompt Caching</Title><Badge>Policy v2</Badge></Group>
          <Text c="dimmed" size="sm">
            Managed directives are emitted only for supported OpenRouter routes. Providers with automatic
            prefix caching remain untouched and are observed through usage metrics.
          </Text>
        </div>

        {legacyError && <Alert color="red" title="Configuration replacement required">{legacyError}</Alert>}

        <Switch
          size="lg"
          label="Enable managed prompt caching rules"
          checked={config.enabled}
          onChange={event => setConfig(current => ({ ...current, enabled: event.currentTarget.checked }))}
        />

        <Group justify="space-between">
          <Text fw={600}>Ordered rules (first match wins)</Text>
          <Button leftSection={<IconPlus size={16} />} onClick={addRule} disabled={!managedCapabilities.length}>Add rule</Button>
        </Group>

        {config.rules.length === 0 && <Alert color="blue">No managed rules configured. Automatic provider caching is unaffected.</Alert>}

        {config.rules.map((rule, index) => {
          const capability = managedCapabilities.find(c => c.modelPattern === rule.modelPattern);
          const explicit = rule.strategy === 'OpenRouterExplicit';
          return (
            <Paper key={`${rule.name}-${index}`} withBorder p="md">
              <Stack gap="sm">
                <Group justify="space-between">
                  <Switch checked={rule.enabled} label={`Rule ${index + 1}`} onChange={event => updateRule(index, { enabled: event.currentTarget.checked })} />
                  <ActionIcon color="red" variant="subtle" aria-label="Delete rule" onClick={() => setConfig(current => ({ ...current, rules: current.rules.filter((ruleItem, i) => ruleItem && i !== index) }))}><IconTrash size={16} /></ActionIcon>
                </Group>
                <TextInput label="Name" value={rule.name} onChange={event => updateRule(index, { name: event.currentTarget.value })} />
                <Select
                  label="Supported route"
                  data={managedCapabilities.map(c => ({ value: c.modelPattern, label: `${c.provider} · ${c.modelPattern}` }))}
                  value={rule.modelPattern}
                  onChange={value => {
                    const nextCapability = managedCapabilities.find(c => c.modelPattern === value);
                    if (!nextCapability) return;
                    const strategy = nextCapability.strategies[0];
                    updateRule(index, {
                      modelPattern: nextCapability.modelPattern,
                      strategy,
                      ttl: (nextCapability.ttls[0] as '5m' | '1h' | undefined) ?? null,
                      injectionPoints: strategy === 'OpenRouterExplicit' ? [{ role: 'system', index: 0 }] : [],
                    });
                  }}
                />
                <Group grow align="end">
                  <Select label="Strategy" data={(capability?.strategies ?? []).map(s => ({ value: s, label: s === 'OpenRouterAutomatic' ? 'Automatic conversation caching' : 'Explicit breakpoints' }))} value={rule.strategy} onChange={value => {
                    if (!value) return;
                    let injectionPoints: InjectionPoint[] = [];
                    if (value === 'OpenRouterExplicit') {
                      injectionPoints = rule.injectionPoints.length > 0
                        ? rule.injectionPoints
                        : [{ role: 'system', index: 0 }];
                    }
                    updateRule(index, { strategy: value as Strategy, injectionPoints });
                  }} />
                  <Select label="TTL" data={(capability?.ttls ?? []).map(ttl => ({ value: ttl, label: ttl }))} value={rule.ttl ?? null} onChange={value => updateRule(index, { ttl: value as '5m' | '1h' | null })} />
                </Group>
                {capability?.minimumTokens && <Text size="xs" c="dimmed">Prefixes shorter than approximately {capability.minimumTokens.toLocaleString()} tokens may not be cached.</Text>}
                {explicit && (
                  <Stack gap="xs">
                    <Group justify="space-between"><Text size="sm" fw={600}>Breakpoints ({rule.injectionPoints.length}/{capability?.maxBreakpoints ?? 4})</Text><Button size="xs" variant="light" disabled={rule.injectionPoints.length >= (capability?.maxBreakpoints ?? 4)} onClick={() => updateRule(index, { injectionPoints: [...rule.injectionPoints, { role: 'user', index: -1 }] })}>Add breakpoint</Button></Group>
                    {rule.injectionPoints.map((point, pointIndex) => <Group key={pointIndex} grow>
                      <Select data={['system', 'user', 'assistant']} value={point.role ?? null} onChange={value => updateRule(index, { injectionPoints: rule.injectionPoints.map((p, i) => i === pointIndex ? { ...p, role: value as InjectionPoint['role'] } : p) })} />
                      <NumberInput value={point.index ?? ''} placeholder="All matching" allowDecimal={false} min={-100} max={100} onChange={value => updateRule(index, { injectionPoints: rule.injectionPoints.map((p, i) => i === pointIndex ? { ...p, index: value === '' ? null : Number(value) } : p) })} />
                      <ActionIcon color="red" variant="subtle" onClick={() => updateRule(index, { injectionPoints: rule.injectionPoints.filter((pointItem, i) => pointItem && i !== pointIndex) })}><IconTrash size={16} /></ActionIcon>
                    </Group>)}
                  </Stack>
                )}
              </Stack>
            </Paper>
          );
        })}

        {providerManaged.length > 0 && <Alert title="Provider-managed automatic caching" color="gray">{providerManaged.map(c => `${c.provider} ${c.modelPattern}`).join(' · ')}</Alert>}
        <Group justify="flex-end"><Button variant="default" onClick={() => void load()}>Reset</Button><Button loading={saving} disabled={!isDirty} onClick={() => void save()}>Save policy</Button></Group>
      </Stack>
    </Container>
  );
}
