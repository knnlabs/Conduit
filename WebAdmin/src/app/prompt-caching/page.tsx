'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  Container,
  Title,
  Text,
  Switch,
  Button,
  Group,
  Stack,
  Paper,
  Badge,
  ActionIcon,
  Select,
  NumberInput,
  Alert,
  Tooltip,
  Skeleton,
} from '@mantine/core';
import { notify } from '@/lib/notifications';
import { IconTrash, IconPlus } from '@tabler/icons-react';
import { withAdminClient } from '@/lib/client/adminClient';

// --- Types (local to avoid SDK any-type leakage) ---

interface InjectionPoint {
  role?: string | null;
  index?: number | null;
}

interface PromptCachingConfig {
  autoInjectEnabled: boolean;
  injectionPoints: InjectionPoint[];
}

// --- Constants ---

interface Preset {
  label: string;
  description: string;
  points: InjectionPoint[];
}

const PRESETS: Preset[] = [
  {
    label: 'System prompt only',
    description: 'Cache the system message for reuse across turns',
    points: [{ role: 'system', index: 0 }],
  },
  {
    label: 'System + last user',
    description: 'Cache system message and the most recent user turn',
    points: [{ role: 'system', index: 0 }, { role: 'user', index: -1 }],
  },
  {
    label: 'Last 2 user turns',
    description: 'Cache the two most recent user messages',
    points: [{ role: 'user', index: -1 }, { role: 'user', index: -2 }],
  },
  {
    label: 'Clear all',
    description: 'Remove all injection points',
    points: [],
  },
];

interface MockMessage {
  role: 'system' | 'user' | 'assistant';
  content: string;
}

const MOCK_MESSAGES: MockMessage[] = [
  { role: 'system', content: 'You are a helpful assistant that answers questions clearly.' },
  { role: 'user', content: 'Hello, can you help me?' },
  { role: 'assistant', content: 'Of course! How can I help you today?' },
  { role: 'user', content: 'Tell me about prompt caching.' },
  { role: 'assistant', content: 'Prompt caching reduces costs by reusing...' },
  { role: 'user', content: 'How do I enable it?' },
];

const ROLE_OPTIONS = [
  { value: '', label: 'Any role' },
  { value: 'system', label: 'system' },
  { value: 'user', label: 'user' },
  { value: 'assistant', label: 'assistant' },
];

const ROLE_COLORS: Record<string, string> = {
  system: 'violet',
  user: 'blue',
  assistant: 'gray',
};

// --- Preview Logic ---

function computeHighlightedIndices(points: InjectionPoint[]): Set<number> {
  const highlighted = new Set<number>();

  for (const point of points) {
    // Filter messages by role (null/undefined matches all)
    const roleFilter = point.role ?? null;
    const matchingIndices: number[] = [];

    for (let i = 0; i < MOCK_MESSAGES.length; i++) {
      if (roleFilter === null || MOCK_MESSAGES[i].role === roleFilter) {
        matchingIndices.push(i);
      }
    }

    if (matchingIndices.length === 0) continue;

    if (point.index === null || point.index === undefined) {
      // null index = all matching messages
      for (const idx of matchingIndices) {
        highlighted.add(idx);
      }
    } else {
      // Resolve index (negative = from end)
      const resolvedIdx = point.index >= 0
        ? point.index
        : matchingIndices.length + point.index;

      if (resolvedIdx >= 0 && resolvedIdx < matchingIndices.length) {
        highlighted.add(matchingIndices[resolvedIdx]);
      }
    }
  }

  // Anthropic max 4 breakpoints
  if (highlighted.size > 4) {
    const arr = Array.from(highlighted);
    return new Set(arr.slice(0, 4));
  }

  return highlighted;
}

// --- Component ---

export default function PromptCachingPage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [autoInjectEnabled, setAutoInjectEnabled] = useState(false);
  const [injectionPoints, setInjectionPoints] = useState<InjectionPoint[]>([]);
  const [originalConfig, setOriginalConfig] = useState<string>('');

  const isDirty = useMemo(() => {
    return JSON.stringify({ autoInjectEnabled, injectionPoints }) !== originalConfig;
  }, [autoInjectEnabled, injectionPoints, originalConfig]);

  const highlightedIndices = useMemo(
    () => computeHighlightedIndices(injectionPoints),
    [injectionPoints]
  );

  const fetchConfig = useCallback(async () => {
    try {
      const raw = await withAdminClient(client =>
        client.configuration.getPromptCachingConfig()
      );
      const config = raw as unknown as PromptCachingConfig;
      setAutoInjectEnabled(config.autoInjectEnabled);
      setInjectionPoints(config.injectionPoints);
      const snapshot = JSON.stringify({
        autoInjectEnabled: config.autoInjectEnabled,
        injectionPoints: config.injectionPoints,
      });
      setOriginalConfig(snapshot);
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      notify.error(`Failed to load prompt caching config: ${message}`);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchConfig();
  }, [fetchConfig]);

  const handleAddPoint = useCallback(() => {
    if (injectionPoints.length >= 4) return;
    setInjectionPoints(prev => [...prev, { role: 'system', index: 0 } as InjectionPoint]);
  }, [injectionPoints.length]);

  const handleRemovePoint = useCallback((idx: number) => {
    setInjectionPoints(prev => prev.filter((point, i) => i !== idx));
  }, []);

  const handleUpdatePointRole = useCallback((idx: number, val: string | null) => {
    setInjectionPoints(prev =>
      prev.map((p, i): InjectionPoint => (i === idx ? { ...p, role: val === '' ? null : val } : p))
    );
  }, []);

  const handleUpdatePointIndex = useCallback((idx: number, val: string | number) => {
    setInjectionPoints(prev =>
      prev.map((p, i): InjectionPoint =>
        i === idx ? { ...p, index: val === '' ? null : Number(val) } : p
      )
    );
  }, []);

  const handleApplyPreset = useCallback((preset: Preset) => {
    setInjectionPoints(preset.points.map((p): InjectionPoint => ({ ...p })));
    if (preset.points.length > 0) {
      setAutoInjectEnabled(true);
    }
  }, []);

  const handleSave = useCallback(async () => {
    setSaving(true);
    try {
      const raw = await withAdminClient(client =>
        client.configuration.updatePromptCachingConfig({
          autoInjectEnabled,
          injectionPoints,
        })
      );
      const result = raw as unknown as PromptCachingConfig;
      setAutoInjectEnabled(result.autoInjectEnabled);
      setInjectionPoints(result.injectionPoints);
      const snapshot = JSON.stringify({
        autoInjectEnabled: result.autoInjectEnabled,
        injectionPoints: result.injectionPoints,
      });
      setOriginalConfig(snapshot);
      notify.success('Prompt caching configuration updated successfully', 'Saved');
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      notify.error(`Failed to save config: ${message}`);
    } finally {
      setSaving(false);
    }
  }, [autoInjectEnabled, injectionPoints]);

  if (loading) {
    return (
      <Container size="md" py="xl">
        <Stack gap="md">
          <Skeleton height={40} width={250} />
          <Skeleton height={20} width={400} />
          <Skeleton height={50} />
          <Skeleton height={200} />
        </Stack>
      </Container>
    );
  }

  return (
    <Container size="md" py="xl">
      <Stack gap="lg">
        {/* Section 1: Header + Toggle */}
        <div>
          <Group gap="sm" mb="xs">
            <Title order={2}>Prompt Caching</Title>
            <Badge color={autoInjectEnabled ? 'green' : 'gray'} variant="filled">
              {autoInjectEnabled ? 'Enabled' : 'Disabled'}
            </Badge>
          </Group>
          <Text c="dimmed" size="sm" mb="md">
            Automatically inject cache_control breakpoints into messages sent to providers
            that support prompt caching (e.g., Anthropic). This can significantly reduce costs
            for repeated prefixes.
          </Text>
          <Switch
            size="lg"
            label="Enable automatic cache injection"
            checked={autoInjectEnabled}
            onChange={(event) => setAutoInjectEnabled(event.currentTarget.checked)}
          />
        </div>

        {autoInjectEnabled && (
          <>
            {/* Section 2: Quick Presets */}
            <div>
              <Text fw={600} mb="xs">Quick Presets</Text>
              <Group gap="xs">
                {PRESETS.map((preset) => (
                  <Tooltip key={preset.label} label={preset.description}>
                    <Button
                      variant="light"
                      size="xs"
                      onClick={() => handleApplyPreset(preset)}
                    >
                      {preset.label}
                    </Button>
                  </Tooltip>
                ))}
              </Group>
            </div>

            {/* Section 3: Injection Points Editor */}
            <div>
              <Group justify="space-between" mb="xs">
                <Text fw={600}>
                  Injection Points{' '}
                  <Text span c="dimmed" size="sm">
                    {injectionPoints.length}/4 points
                  </Text>
                </Text>
                <Button
                  size="xs"
                  variant="light"
                  leftSection={<IconPlus size={14} />}
                  disabled={injectionPoints.length >= 4}
                  onClick={handleAddPoint}
                >
                  Add Point
                </Button>
              </Group>

              {injectionPoints.length === 0 ? (
                <Alert color="yellow" variant="light">
                  No injection points configured. Use a preset above or add points manually.
                </Alert>
              ) : (
                <Stack gap="xs">
                  {injectionPoints.map((point, idx) => (
                    <Paper key={idx} withBorder p="sm">
                      <Group gap="md">
                        <Select
                          label="Role"
                          data={ROLE_OPTIONS}
                          value={point.role ?? ''}
                          onChange={(val) => handleUpdatePointRole(idx, val)}
                          w={160}
                          size="sm"
                        />
                        <NumberInput
                          label="Index"
                          placeholder="All"
                          value={point.index ?? ''}
                          onChange={(val) => handleUpdatePointIndex(idx, val)}
                          min={-100}
                          max={100}
                          w={120}
                          size="sm"
                        />
                        <ActionIcon
                          color="red"
                          variant="light"
                          onClick={() => handleRemovePoint(idx)}
                          mt="xl"
                        >
                          <IconTrash size={16} />
                        </ActionIcon>
                      </Group>
                    </Paper>
                  ))}
                </Stack>
              )}
            </div>

            {/* Section 4: Live Preview */}
            <div>
              <Text fw={600} mb="xs">Live Preview</Text>
              <Text c="dimmed" size="xs" mb="sm">
                Messages highlighted in blue will have cache_control injected.
              </Text>
              <Stack gap="xs">
                {MOCK_MESSAGES.map((msg, idx) => {
                  const isHighlighted = highlightedIndices.has(idx);
                  return (
                    <Paper
                      key={idx}
                      withBorder
                      p="sm"
                      style={isHighlighted ? {
                        borderColor: 'var(--mantine-color-blue-5)',
                        borderWidth: 2,
                        background: 'var(--mantine-color-blue-light)',
                      } : undefined}
                    >
                      <Group gap="sm">
                        <Badge
                          color={ROLE_COLORS[msg.role] ?? 'gray'}
                          variant="filled"
                          size="sm"
                          w={80}
                        >
                          {msg.role}
                        </Badge>
                        <Text size="sm" style={{ flex: 1 }}>
                          {msg.content}
                        </Text>
                        {isHighlighted && (
                          <Badge color="blue" variant="light" size="xs">
                            cached
                          </Badge>
                        )}
                      </Group>
                    </Paper>
                  );
                })}
              </Stack>
            </div>
          </>
        )}

        {/* Save Button */}
        <Group justify="flex-end">
          <Button
            size="md"
            loading={saving}
            disabled={!isDirty}
            onClick={() => void handleSave()}
          >
            Save Configuration
          </Button>
        </Group>
      </Stack>
    </Container>
  );
}
