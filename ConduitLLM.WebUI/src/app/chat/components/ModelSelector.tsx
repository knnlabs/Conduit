import { Select, Group, Text, Badge, Stack } from '@mantine/core';
import { 
  IconEye, 
  IconTool, 
  IconBraces
} from '@tabler/icons-react';
import { useChatStore } from '../hooks/useChatStore';
import { useModels } from '../hooks/useModels';

export function ModelSelector() {
  const { data: models, isLoading } = useModels();
  const { getActiveSession, updateSessionModel } = useChatStore();
  const activeSession = getActiveSession();

  const handleModelChange = (modelId: string | null) => {
    if (modelId && activeSession) {
      updateSessionModel(activeSession.id, modelId);
    }
  };

  const renderSelectOption = ({ option }: { option: { value: string; label: string } }) => {
    const model = models?.find(m => m.id === option.value);
    if (!model) return option.label;
    
    return (
      <Group justify="space-between" wrap="nowrap">
        <Stack gap={4}>
          <Text size="sm" fw={500}>{model.id}</Text>
          <Group gap={4}>
            <Badge size="xs" variant="light" color="blue">
              {model.providerName ?? model.providerId}
            </Badge>
            {model.maxContextTokens && (
              <Badge size="xs" variant="light" color="gray">
                {model.maxContextTokens.toLocaleString()} tokens
              </Badge>
            )}
          </Group>
        </Stack>
        <Group gap={4}>
          {model.supportsVision && (
            <IconEye size={16} color="var(--mantine-color-blue-6)" />
          )}
          {model.supportsFunctionCalling && (
            <IconTool size={16} color="var(--mantine-color-green-6)" />
          )}
          {model.supportsJsonMode && (
            <IconBraces size={16} color="var(--mantine-color-orange-6)" />
          )}
        </Group>
      </Group>
    );
  };


  return (
    <Select
      placeholder="Select a model"
      value={activeSession?.model ?? null}
      onChange={handleModelChange}
      data={models?.map(model => ({
        value: model.id,
        label: model.id
      })) ?? []}
      renderOption={renderSelectOption}
      searchable
      maxDropdownHeight={400}
      disabled={!activeSession || isLoading}
      styles={{
        input: { minHeight: 42 },
        dropdown: { maxWidth: 500 }
      }}
    />
  );
}