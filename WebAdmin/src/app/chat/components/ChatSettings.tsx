import {
  ActionIcon,
  Stack,
  Slider,
  Text,
  NumberInput,
  Select,
  Textarea,
  Switch,
  Group,
  Divider,
  Badge,
  MultiSelect
} from '@mantine/core';
import { IconRefresh } from '@tabler/icons-react';
import { useChatStore } from '../hooks/useChatStore';
import { CHAT_PRESETS, findMatchingPreset } from '@knn_labs/conduit-gateway-client';
import { getPresetIcon } from '../utils/presets';
import { ChatParameters } from '../types';
import type { FunctionConfigurationDto } from '@knn_labs/conduit-admin-client';

interface ChatSettingsProps {
  reasoningExpanded?: boolean;
  onReasoningExpandedChange?: (expanded: boolean) => void;
  availableFunctions?: FunctionConfigurationDto[];
  selectedFunctionIds?: number[];
  onFunctionIdsChange?: (ids: number[]) => void;
}

export function ChatSettings({
  reasoningExpanded = true,
  onReasoningExpandedChange,
  availableFunctions = [],
  selectedFunctionIds = [],
  onFunctionIdsChange
}: ChatSettingsProps) {
  const { getActiveSession, updateSessionParameters } = useChatStore();
  const activeSession = getActiveSession();

  if (!activeSession) return null;

  const parameters = activeSession.parameters;

  // Helper to get provider type name
  const getProviderTypeName = (providerType: number): string => {
    switch (providerType) {
      case 1:
        return 'Exa';
      case 2:
        return 'Tavily';
      case 3:
        return 'Custom';
      default:
        return 'Unknown';
    }
  };

  const handleParameterChange = (updates: Partial<ChatParameters>) => {
    updateSessionParameters(activeSession.id, updates);
  };

  const handlePresetSelect = (presetId: string | null) => {
    if (!presetId) return;
    
    const preset = CHAT_PRESETS.find(p => p.id === presetId);
    if (preset) {
      // The preset parameters from SDK match our ChatParameters interface
      handleParameterChange({
        temperature: preset.parameters.temperature,
        topP: preset.parameters.topP,
        frequencyPenalty: preset.parameters.frequencyPenalty,
        presencePenalty: preset.parameters.presencePenalty,
      });
    }
  };

  const resetToDefaults = () => {
    handleParameterChange({
      temperature: 0.7,
      maxTokens: 2048,
      topP: 1,
      frequencyPenalty: 0,
      presencePenalty: 0,
      responseFormat: 'text',
      seed: undefined,
      stop: undefined,
      stream: true,
    });
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Text fw={600}>Chat Settings</Text>
        <ActionIcon 
          variant="subtle" 
          size="sm"
          onClick={resetToDefaults}
          title="Reset to defaults"
        >
          <IconRefresh size={16} />
        </ActionIcon>
      </Group>
          
          <Select
            label="Preset"
            placeholder="Select a preset"
            data={CHAT_PRESETS.map(preset => ({
              value: preset.id,
              label: preset.name,
            }))}
            onChange={handlePresetSelect}
            clearable
            leftSection={(() => {
              const matchingPreset = findMatchingPreset({
                temperature: parameters.temperature,
                topP: parameters.topP,
                frequencyPenalty: parameters.frequencyPenalty,
                presencePenalty: parameters.presencePenalty,
              });
              if (matchingPreset?.icon) {
                const Icon = getPresetIcon(matchingPreset.icon);
                return <Icon size={16} />;
              }
              return undefined;
            })()}
          />

          {availableFunctions.length > 0 && (
            <MultiSelect
              label="Functions"
              description="Enable AI function calling for web search, RAG, and more"
              placeholder={selectedFunctionIds.length === 0 ? "No functions selected" : ""}
              data={availableFunctions.map(f => ({
                value: String(f.id),
                label: f.configurationName || 'Unnamed Function',
              }))}
              value={selectedFunctionIds.map(String)}
              onChange={(values) => {
                if (onFunctionIdsChange) {
                  onFunctionIdsChange(values.map(Number));
                }
              }}
              searchable
              clearable
              maxDropdownHeight={300}
              renderOption={({ option }) => {
                const func = availableFunctions.find(f => String(f.id) === option.value);
                if (!func) return option.label;
                return (
                  <div>
                    <Text size="sm" fw={500}>{func.configurationName || 'Unnamed Function'}</Text>
                    <Text size="xs" c="dimmed">{getProviderTypeName(func.providerType || 3)}</Text>
                  </div>
                );
              }}
            />
          )}

          <Divider />
          
          <div>
            <Group justify="space-between" mb={4}>
              <Text size="sm">Temperature</Text>
              <Badge size="sm" variant="light">{parameters.temperature}</Badge>
            </Group>
            <Slider
              value={parameters.temperature}
              onChange={(value) => handleParameterChange({ temperature: value })}
              min={0}
              max={2}
              step={0.1}
              marks={[
                { value: 0, label: '0' },
                { value: 1, label: '1' },
                { value: 2, label: '2' },
              ]}
            />
          </div>
          
          <NumberInput
            label="Max Tokens"
            value={parameters.maxTokens}
            onChange={(value) => handleParameterChange({ maxTokens: value as number })}
            min={1}
            max={32000}
            step={100}
          />
          
          <div>
            <Group justify="space-between" mb={4}>
              <Text size="sm">Top P</Text>
              <Badge size="sm" variant="light">{parameters.topP}</Badge>
            </Group>
            <Slider
              value={parameters.topP}
              onChange={(value) => handleParameterChange({ topP: value })}
              min={0}
              max={1}
              step={0.01}
              marks={[
                { value: 0, label: '0' },
                { value: 0.5, label: '0.5' },
                { value: 1, label: '1' },
              ]}
            />
          </div>
          
          <div>
            <Group justify="space-between" mb={4}>
              <Text size="sm">Frequency Penalty</Text>
              <Badge size="sm" variant="light">{parameters.frequencyPenalty}</Badge>
            </Group>
            <Slider
              value={parameters.frequencyPenalty}
              onChange={(value) => handleParameterChange({ frequencyPenalty: value })}
              min={-2}
              max={2}
              step={0.1}
              marks={[
                { value: -2, label: '-2' },
                { value: 0, label: '0' },
                { value: 2, label: '2' },
              ]}
            />
          </div>
          
          <div>
            <Group justify="space-between" mb={4}>
              <Text size="sm">Presence Penalty</Text>
              <Badge size="sm" variant="light">{parameters.presencePenalty}</Badge>
            </Group>
            <Slider
              value={parameters.presencePenalty}
              onChange={(value) => handleParameterChange({ presencePenalty: value })}
              min={-2}
              max={2}
              step={0.1}
              marks={[
                { value: -2, label: '-2' },
                { value: 0, label: '0' },
                { value: 2, label: '2' },
              ]}
            />
          </div>
          
          <Select
            label="Response Format"
            value={parameters.responseFormat}
            onChange={(value) => handleParameterChange({ responseFormat: value as 'text' | 'json_object' })}
            data={[
              { value: 'text', label: 'Text' },
              { value: 'json_object', label: 'JSON' },
            ]}
          />
          
          <Switch
            label="Streaming"
            checked={parameters.stream ?? true}
            onChange={(event) => handleParameterChange({ stream: event.currentTarget.checked })}
            description="Stream responses as they are generated"
          />
          
          <Switch
            label="Expand Reasoning"
            checked={reasoningExpanded}
            onChange={(event) => onReasoningExpandedChange?.(event.currentTarget.checked)}
            description="Show reasoning content expanded by default"
          />
          
          <NumberInput
            label="Seed (optional)"
            placeholder="Random"
            value={parameters.seed ?? ''}
            onChange={(value) => handleParameterChange({ seed: value ? Number(value) : undefined })}
            min={0}
          />
          
          <Textarea
            label="System Prompt"
            placeholder="You are a helpful assistant..."
            value={parameters.systemPrompt ?? ''}
            onChange={(e) => handleParameterChange({ systemPrompt: e.currentTarget.value })}
            minRows={3}
            maxRows={6}
          />
          
          <Textarea
            label="Stop Sequences"
            placeholder="Enter stop sequences, one per line..."
            value={parameters.stop?.join('\n') ?? ''}
            onChange={(e) => {
              const sequences = e.currentTarget.value
                .split('\n')
                .filter(s => s.trim().length > 0);
              handleParameterChange({ stop: sequences.length > 0 ? sequences : undefined });
            }}
            minRows={2}
            maxRows={4}
            description="The model will stop generating when it encounters any of these sequences"
          />
    </Stack>
  );
}