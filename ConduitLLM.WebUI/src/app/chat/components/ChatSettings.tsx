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
  Button,
  Divider,
  Badge
} from '@mantine/core';
import { IconRefresh } from '@tabler/icons-react';
import { useChatStore } from '../hooks/useChatStore';
import { CHAT_PRESETS, getPresetIcon } from '../utils/presets';
import { ChatParameters } from '../types';

export function ChatSettings() {
  const { getActiveSession, updateSessionParameters } = useChatStore();
  const activeSession = getActiveSession();
  
  if (!activeSession) return null;
  
  const parameters = activeSession.parameters;

  const handleParameterChange = (updates: Partial<ChatParameters>) => {
    updateSessionParameters(activeSession.id, updates);
  };

  const handlePresetSelect = (presetId: string | null) => {
    if (!presetId) return;
    
    const preset = CHAT_PRESETS.find(p => p.id === presetId);
    if (preset) {
      handleParameterChange(preset.parameters);
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
              const matchingPreset = CHAT_PRESETS.find(p => 
                p.parameters.temperature === parameters.temperature &&
                p.parameters.topP === parameters.topP &&
                p.parameters.frequencyPenalty === parameters.frequencyPenalty &&
                p.parameters.presencePenalty === parameters.presencePenalty
              );
              if (matchingPreset) {
                const Icon = getPresetIcon(matchingPreset.icon);
                return <Icon size={16} />;
              }
              return undefined;
            })()}
          />
          
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
          
          <NumberInput
            label="Seed (optional)"
            placeholder="Random"
            value={parameters.seed || ''}
            onChange={(value) => handleParameterChange({ seed: value ? Number(value) : undefined })}
            min={0}
          />
          
          <Textarea
            label="System Prompt"
            placeholder="You are a helpful assistant..."
            value={parameters.systemPrompt || ''}
            onChange={(e) => handleParameterChange({ systemPrompt: e.currentTarget.value })}
            minRows={3}
            maxRows={6}
          />
          
          <Textarea
            label="Stop Sequences"
            placeholder="Enter stop sequences, one per line..."
            value={parameters.stop?.join('\n') || ''}
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