'use client';

import {
  Modal,
  TextInput,
  Switch,
  Stack,
  Group,
  NumberInput,
  Button,
  Select,
  Text,
  Alert,
  Paper,
  Badge,
  Flex,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconInfoCircle, IconRobot, IconEye, IconBrush, IconVideo, IconBrain } from '@tabler/icons-react';
import { useCreateModelMapping } from '@/hooks/useModelMappingsApi';
import { useProviders } from '@/hooks/useProviderApi';
import { useModels } from '@/hooks/useModelsApi';
import { ProviderModelSelect } from './ProviderModelSelect';
import type { CreateModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

interface CreateModelMappingModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

interface FormValues {
  modelAlias: string;
  modelId: number | null;
  providerId: string;
  providerModelId: string;
  priority: number;
  isEnabled: boolean;
  maxContextTokensOverride?: number;
  providerVariation?: string;
  qualityScore?: number;
  isDefault: boolean;
  defaultCapabilityType?: string;
  notes?: string;
}

export function CreateModelMappingModal({ 
  isOpen, 
  onClose, 
  onSuccess 
}: CreateModelMappingModalProps) {
  const createMapping = useCreateModelMapping();
  const { providers, isLoading: providersLoading } = useProviders();
  const { models, isLoading: modelsLoading } = useModels();

  const form = useForm<FormValues>({
    initialValues: {
      modelAlias: '',
      modelId: null,
      providerId: '',
      providerModelId: '',
      priority: 100,
      isEnabled: true,
      maxContextTokensOverride: undefined,
      providerVariation: undefined,
      qualityScore: undefined,
      isDefault: false,
      defaultCapabilityType: undefined,
      notes: undefined,
    },
    validate: {
      modelAlias: (value) => !value?.trim() ? 'Model alias is required' : null,
      modelId: (value) => !value ? 'Model selection is required' : null,
      providerId: (value) => !value?.trim() ? 'Provider is required' : null,
      providerModelId: (value) => !value?.trim() ? 'Provider model ID is required' : null,
      priority: (value) => value < 0 || value > 1000 ? 'Priority must be between 0 and 1000' : null,
      qualityScore: (value) => {
        if (value === undefined || value === null) return null;
        return value < 0 || value > 1 ? 'Quality score must be between 0 and 1' : null;
      }
    },
  });

  const handleSubmit = async (values: FormValues) => {
    if (!values.modelId) return;

    const createData: CreateModelProviderMappingDto = {
      modelAlias: values.modelAlias,
      modelId: values.modelId,
      providerId: parseInt(values.providerId, 10),
      providerModelId: values.providerModelId,
      priority: values.priority,
      isEnabled: values.isEnabled,
      maxContextTokensOverride: values.maxContextTokensOverride,
      providerVariation: values.providerVariation,
      qualityScore: values.qualityScore,
      isDefault: values.isDefault,
      defaultCapabilityType: values.defaultCapabilityType,
      notes: values.notes,
    };

    try {
      await createMapping.mutateAsync(createData);
      form.reset();
      onSuccess?.();
      onClose();
    } catch (error) {
      console.error('Failed to create model mapping:', error);
    }
  };

  const selectedModel = models.find(m => m.id === form.values.modelId);
  const modelCapabilities = selectedModel?.capabilities;

  const providerOptions = providers?.map(p => ({
    value: p.id.toString(),
    label: `${p.providerName} (${p.providerType})`,
  })) || [];

  const modelOptions = models
    .filter(m => m.id !== undefined)
    .map(m => ({
      value: String(m.id),
      label: m.name ?? 'Unknown Model'
    }));

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Create Model Provider Mapping"
      size="xl"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          {/* Model Selection Section */}
          <Paper p="md" withBorder>
            <Stack gap="sm">
              <Text fw={600} size="sm">Model Configuration</Text>
              
              <Select
                label="Model"
                placeholder="Select a model"
                data={modelOptions}
                searchable
                required
                {...form.getInputProps('modelId')}
                onChange={(value) => {
                  form.setFieldValue('modelId', value ? parseInt(value, 10) : null);
                  // Auto-fill model alias if empty
                  if (!form.values.modelAlias && value) {
                    const model = models.find(m => m.id === parseInt(value, 10));
                    if (model?.name) {
                      form.setFieldValue('modelAlias', model.name);
                    }
                  }
                }}
                value={form.values.modelId?.toString()}
              />

              <TextInput
                label="Model Alias"
                placeholder="e.g., gpt-4, claude-3-opus"
                description="The name clients will use to request this model"
                required
                {...form.getInputProps('modelAlias')}
              />

              {modelCapabilities && (
                <Paper p="xs" bg="gray.0">
                  <Text size="xs" fw={500} mb="xs">Model Capabilities:</Text>
                  <Flex gap="xs" wrap="wrap">
                    {modelCapabilities.supportsChat && <Badge size="sm" leftSection={<IconRobot size={12} />}>Chat</Badge>}
                    {modelCapabilities.supportsVision && <Badge size="sm" leftSection={<IconEye size={12} />}>Vision</Badge>}
                    {modelCapabilities.supportsImageGeneration && <Badge size="sm" leftSection={<IconBrush size={12} />}>Images</Badge>}
                    {modelCapabilities.supportsVideoGeneration && <Badge size="sm" leftSection={<IconVideo size={12} />}>Video</Badge>}
                    {modelCapabilities.supportsEmbeddings && <Badge size="sm" leftSection={<IconBrain size={12} />}>Embeddings</Badge>}
                  </Flex>
                  <Text size="xs" mt="xs">Max Tokens: {modelCapabilities.maxTokens?.toLocaleString()}</Text>
                </Paper>
              )}
            </Stack>
          </Paper>

          {/* Provider Configuration Section */}
          <Paper p="md" withBorder>
            <Stack gap="sm">
              <Text fw={600} size="sm">Provider Configuration</Text>
              
              <Select
                label="Provider"
                placeholder="Select a provider"
                data={providerOptions}
                searchable
                required
                disabled={providersLoading}
                {...form.getInputProps('providerId')}
              />

              {form.values.providerId && (
                <ProviderModelSelect
                  providerId={form.values.providerId}
                  value={form.values.providerModelId}
                  onChange={(value) => form.setFieldValue('providerModelId', value || '')}
                  label="Provider Model ID"
                  placeholder="Enter or select provider's model ID"
                  description="The model identifier used by the provider's API"
                  required
                />
              )}

              <Group grow>
                <NumberInput
                  label="Priority"
                  description="Lower values have higher priority"
                  min={0}
                  max={1000}
                  {...form.getInputProps('priority')}
                />

                <Switch
                  label="Enabled"
                  description="Whether this mapping is active"
                  checked={form.values.isEnabled}
                  {...form.getInputProps('isEnabled')}
                />
              </Group>
            </Stack>
          </Paper>

          {/* Provider Overrides Section */}
          <Paper p="md" withBorder>
            <Stack gap="sm">
              <Text fw={600} size="sm">Provider-Specific Overrides (Optional)</Text>
              
              <NumberInput
                label="Max Context Tokens Override"
                placeholder="Leave empty to use model default"
                description="Override the model's default max tokens for this provider"
                min={1}
                {...form.getInputProps('maxContextTokensOverride')}
              />

              <TextInput
                label="Provider Variation"
                placeholder="e.g., Q4_K_M, GGUF, instruct"
                description="Specific variation or quantization of the model"
                {...form.getInputProps('providerVariation')}
              />

              <NumberInput
                label="Quality Score"
                placeholder="1.0 = identical to original"
                description="Quality relative to the original model (0-1)"
                min={0}
                max={1}
                step={0.05}
                decimalScale={2}
                {...form.getInputProps('qualityScore')}
              />
            </Stack>
          </Paper>

          {/* Advanced Settings */}
          <Paper p="md" withBorder>
            <Stack gap="sm">
              <Text fw={600} size="sm">Advanced Settings</Text>
              
              <Group>
                <Switch
                  label="Set as Default"
                  checked={form.values.isDefault}
                  {...form.getInputProps('isDefault')}
                />
                
                {form.values.isDefault && (
                  <Select
                    label="Default for Capability"
                    placeholder="Select capability type"
                    data={[
                      { value: 'chat', label: 'Chat' },
                      { value: 'vision', label: 'Vision' },
                      { value: 'embedding', label: 'Embeddings' },
                      { value: 'image-generation', label: 'Image Generation' },
                      { value: 'audio-transcription', label: 'Audio Transcription' },
                      { value: 'text-to-speech', label: 'Text to Speech' },
                    ]}
                    {...form.getInputProps('defaultCapabilityType')}
                  />
                )}
              </Group>

              <TextInput
                label="Notes"
                placeholder="Optional notes about this mapping"
                {...form.getInputProps('notes')}
              />
            </Stack>
          </Paper>

          <Alert icon={<IconInfoCircle size={16} />} color="blue">
            Model capabilities are defined by the selected model. Provider-specific variations
            or limitations should be documented in the notes field.
          </Alert>

          <Group justify="flex-end">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button 
              type="submit" 
              loading={createMapping.isPending}
              disabled={modelsLoading || providersLoading}
            >
              Create Mapping
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}