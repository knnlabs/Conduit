'use client';

import { useState } from 'react';
import {
  Stack,
  Slider,
  Text,
  NumberInput,
  Textarea,
  Collapse,
  Button,
  Paper,
  Group,
  Badge,
  Tooltip,
  ActionIcon,
} from '@mantine/core';
import { IconSettings, IconChevronDown, IconChevronUp, IconRefresh } from '@tabler/icons-react';

export interface ChatParameters {
  temperature: number;
  topP: number;
  maxTokens: number;
  frequencyPenalty: number;
  presencePenalty: number;
  systemPrompt: string;
  stopSequences: string[];
}

interface AdvancedChatControlsProps {
  parameters: ChatParameters;
  onChange: (params: ChatParameters) => void;
  onReset?: () => void;
}

const DEFAULT_PARAMS: ChatParameters = {
  temperature: 0.7,
  topP: 1,
  maxTokens: 2048,
  frequencyPenalty: 0,
  presencePenalty: 0,
  systemPrompt: '',
  stopSequences: [],
};

export function AdvancedChatControls({
  parameters,
  onChange,
  onReset,
}: AdvancedChatControlsProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [stopSequencesText, setStopSequencesText] = useState(
    parameters.stopSequences.join('\n')
  );

  const updateParam = <K extends keyof ChatParameters>(
    key: K,
    value: ChatParameters[K]
  ) => {
    onChange({ ...parameters, [key]: value });
  };

  const handleStopSequencesChange = (text: string) => {
    setStopSequencesText(text);
    const sequences = text.split('\n').filter((s) => s.trim());
    updateParam('stopSequences', sequences);
  };

  const handleReset = () => {
    onChange(DEFAULT_PARAMS);
    setStopSequencesText('');
    onReset?.();
  };

  const hasNonDefaultValues = Object.entries(parameters).some(([key, value]) => {
    const defaultValue = DEFAULT_PARAMS[key as keyof ChatParameters];
    if (Array.isArray(value)) {
      return value.length > 0;
    }
    return value !== defaultValue;
  });

  return (
    <Paper shadow="xs" p="md" withBorder>
      <Group justify="space-between" mb={isExpanded ? 'md' : 0}>
        <Group gap="xs">
          <IconSettings size={20} />
          <Text fw={500}>Advanced Settings</Text>
          {hasNonDefaultValues && (
            <Badge size="xs" variant="filled">
              Modified
            </Badge>
          )}
        </Group>
        <Group gap="xs">
          {hasNonDefaultValues && (
            <Tooltip label="Reset to defaults">
              <ActionIcon
                variant="subtle"
                onClick={handleReset}
                size="sm"
              >
                <IconRefresh size={16} />
              </ActionIcon>
            </Tooltip>
          )}
          <ActionIcon
            variant="subtle"
            onClick={() => setIsExpanded(!isExpanded)}
          >
            {isExpanded ? <IconChevronUp size={20} /> : <IconChevronDown size={20} />}
          </ActionIcon>
        </Group>
      </Group>

      <Collapse in={isExpanded}>
        <Stack gap="md">
          <div>
            <Group justify="space-between" mb={4}>
              <Text size="sm" fw={500}>
                Temperature
              </Text>
              <Text size="xs" c="dimmed">
                {parameters.temperature}
              </Text>
            </Group>
            <Slider
              value={parameters.temperature}
              onChange={(value) => updateParam('temperature', value)}
              min={0}
              max={2}
              step={0.1}
              marks={[
                { value: 0, label: '0' },
                { value: 1, label: '1' },
                { value: 2, label: '2' },
              ]}
            />
            <Text size="xs" c="dimmed" mt={4}>
              Controls randomness: lower is more deterministic
            </Text>
          </div>

          <div>
            <Group justify="space-between" mb={4}>
              <Text size="sm" fw={500}>
                Top P
              </Text>
              <Text size="xs" c="dimmed">
                {parameters.topP}
              </Text>
            </Group>
            <Slider
              value={parameters.topP}
              onChange={(value) => updateParam('topP', value)}
              min={0}
              max={1}
              step={0.05}
              marks={[
                { value: 0, label: '0' },
                { value: 0.5, label: '0.5' },
                { value: 1, label: '1' },
              ]}
            />
            <Text size="xs" c="dimmed" mt={4}>
              Nucleus sampling: consider tokens with top_p probability mass
            </Text>
          </div>

          <div>
            <Text size="sm" fw={500} mb={4}>
              Max Tokens
            </Text>
            <NumberInput
              value={parameters.maxTokens}
              onChange={(value) => updateParam('maxTokens', Number(value) || 0)}
              min={1}
              max={128000}
              step={100}
              description="Maximum tokens to generate"
            />
          </div>

          <div>
            <Group justify="space-between" mb={4}>
              <Text size="sm" fw={500}>
                Frequency Penalty
              </Text>
              <Text size="xs" c="dimmed">
                {parameters.frequencyPenalty}
              </Text>
            </Group>
            <Slider
              value={parameters.frequencyPenalty}
              onChange={(value) => updateParam('frequencyPenalty', value)}
              min={-2}
              max={2}
              step={0.1}
              marks={[
                { value: -2, label: '-2' },
                { value: 0, label: '0' },
                { value: 2, label: '2' },
              ]}
            />
            <Text size="xs" c="dimmed" mt={4}>
              Penalize tokens based on existing frequency in text
            </Text>
          </div>

          <div>
            <Group justify="space-between" mb={4}>
              <Text size="sm" fw={500}>
                Presence Penalty
              </Text>
              <Text size="xs" c="dimmed">
                {parameters.presencePenalty}
              </Text>
            </Group>
            <Slider
              value={parameters.presencePenalty}
              onChange={(value) => updateParam('presencePenalty', value)}
              min={-2}
              max={2}
              step={0.1}
              marks={[
                { value: -2, label: '-2' },
                { value: 0, label: '0' },
                { value: 2, label: '2' },
              ]}
            />
            <Text size="xs" c="dimmed" mt={4}>
              Penalize tokens based on whether they appear in text so far
            </Text>
          </div>

          <div>
            <Text size="sm" fw={500} mb={4}>
              System Prompt
            </Text>
            <Textarea
              value={parameters.systemPrompt}
              onChange={(e) => updateParam('systemPrompt', e.target.value)}
              placeholder="You are a helpful assistant..."
              minRows={2}
              maxRows={6}
              autosize
              description="Initial instructions for the model"
            />
          </div>

          <div>
            <Text size="sm" fw={500} mb={4}>
              Stop Sequences
            </Text>
            <Textarea
              value={stopSequencesText}
              onChange={(e) => handleStopSequencesChange(e.target.value)}
              placeholder="Enter stop sequences (one per line)"
              minRows={2}
              maxRows={4}
              autosize
              description="Sequences where the model will stop generating"
            />
          </div>
        </Stack>
      </Collapse>
    </Paper>
  );
}