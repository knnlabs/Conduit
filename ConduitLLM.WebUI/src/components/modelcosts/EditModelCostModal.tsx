'use client';

import {
  Modal,
  TextInput,
  NumberInput,
  Select,
  Switch,
  Button,
  Group,
  Stack,
  Alert,
  Divider,
  Textarea,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconInfoCircle } from '@tabler/icons-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ModelCost, UpdateModelCostDto } from '@/hooks/useModelCostsApi';
import { useModelCostsApi } from '@/hooks/useModelCostsApi';
import { PatternPreview } from './PatternPreview';

interface EditModelCostModalProps {
  isOpen: boolean;
  modelCost: ModelCost;
  onClose: () => void;
  onSuccess?: () => void;
}

interface FormValues {
  modelIdPattern: string;
  modelType: 'chat' | 'embedding' | 'image' | 'audio' | 'video';
  // Token-based costs (per 1K tokens for display)
  inputCostPer1K: number;
  outputCostPer1K: number;
  embeddingCostPer1K: number;
  // Other cost types
  imageCostPerImage: number;
  audioCostPerMinute: number;
  audioCostPerKCharacters: number;
  audioInputCostPerMinute: number;
  audioOutputCostPerMinute: number;
  videoCostPerSecond: number;
  videoResolutionMultipliers: string;
  // Metadata
  priority: number;
  description: string;
  isActive: boolean;
}

export function EditModelCostModal({ isOpen, modelCost, onClose, onSuccess }: EditModelCostModalProps) {
  const queryClient = useQueryClient();
  const { updateModelCost } = useModelCostsApi();

  // Convert backend data to form values
  const initialValues: FormValues = {
    modelIdPattern: modelCost.modelIdPattern,
    modelType: modelCost.modelType,
    // Convert from per million to per 1K for display
    inputCostPer1K: (modelCost.inputCostPerMillionTokens || 0) / 1000,
    outputCostPer1K: (modelCost.outputCostPerMillionTokens || 0) / 1000,
    embeddingCostPer1K: 0, // Backend doesn't have this field yet
    imageCostPerImage: modelCost.costPerImage || 0,
    audioCostPerMinute: 0, // Backend missing audio fields
    audioCostPerKCharacters: 0,
    audioInputCostPerMinute: 0,
    audioOutputCostPerMinute: 0,
    videoCostPerSecond: modelCost.costPerSecond || 0,
    videoResolutionMultipliers: '', // Backend missing this field
    priority: modelCost.priority,
    description: '', // Backend missing description field
    isActive: modelCost.isActive,
  };

  const form = useForm<FormValues>({
    initialValues,
    validate: {
      modelIdPattern: (value) => !value?.trim() ? 'Model pattern is required' : null,
      priority: (value) => value < 0 ? 'Priority must be non-negative' : null,
      inputCostPer1K: (value) => value < 0 ? 'Cost must be non-negative' : null,
      outputCostPer1K: (value) => value < 0 ? 'Cost must be non-negative' : null,
      embeddingCostPer1K: (value) => value < 0 ? 'Cost must be non-negative' : null,
      imageCostPerImage: (value) => value < 0 ? 'Cost must be non-negative' : null,
      audioCostPerMinute: (value) => value < 0 ? 'Cost must be non-negative' : null,
      videoCostPerSecond: (value) => value < 0 ? 'Cost must be non-negative' : null,
    },
  });

  const updateMutation = useMutation({
    mutationFn: async (data: UpdateModelCostDto) => updateModelCost(modelCost.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['model-costs'] });
      onSuccess?.();
      onClose();
    },
  });

  const handleSubmit = (values: FormValues) => {
    // Only send changed fields
    const updates: UpdateModelCostDto = {};
    
    if (values.modelIdPattern !== modelCost.modelIdPattern) {
      updates.modelIdPattern = values.modelIdPattern;
    }
    
    // Convert token costs back to per million
    const inputTokenCost = values.inputCostPer1K * 1000;
    const outputTokenCost = values.outputCostPer1K * 1000;
    
    if (inputTokenCost !== modelCost.inputCostPerMillionTokens) {
      updates.inputTokenCost = inputTokenCost;
    }
    if (outputTokenCost !== modelCost.outputCostPerMillionTokens) {
      updates.outputTokenCost = outputTokenCost;
    }
    
    if (values.embeddingCostPer1K > 0) {
      updates.embeddingTokenCost = values.embeddingCostPer1K * 1000;
    }
    
    if (values.imageCostPerImage !== modelCost.costPerImage) {
      updates.imageCostPerImage = values.imageCostPerImage || undefined;
    }
    
    if (values.audioCostPerMinute > 0) {
      updates.audioCostPerMinute = values.audioCostPerMinute;
    }
    if (values.audioCostPerKCharacters > 0) {
      updates.audioCostPerKCharacters = values.audioCostPerKCharacters;
    }
    if (values.audioInputCostPerMinute > 0) {
      updates.audioInputCostPerMinute = values.audioInputCostPerMinute;
    }
    if (values.audioOutputCostPerMinute > 0) {
      updates.audioOutputCostPerMinute = values.audioOutputCostPerMinute;
    }
    
    if (values.videoCostPerSecond !== modelCost.costPerSecond) {
      updates.videoCostPerSecond = values.videoCostPerSecond || undefined;
    }
    
    if (values.videoResolutionMultipliers) {
      updates.videoResolutionMultipliers = values.videoResolutionMultipliers;
    }
    
    if (values.priority !== modelCost.priority) {
      updates.priority = values.priority;
    }
    
    if (values.description) {
      updates.description = values.description;
    }

    updateMutation.mutate(updates);
  };

  const modelType = form.values.modelType;

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Edit Model Pricing"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <Alert icon={<IconInfoCircle size={16} />} color="blue">
            Update pricing for {modelCost.modelIdPattern}. Token costs are displayed per 1,000 tokens.
          </Alert>

          <TextInput
            label="Model Pattern"
            placeholder="e.g., openai/gpt-4, anthropic/claude-3*, minimax/abab6.5g"
            required
            {...form.getInputProps('modelIdPattern')}
            description="Exact model ID or pattern with * wildcard"
          />

          <PatternPreview pattern={form.values.modelIdPattern} />

          <Select
            label="Model Type"
            data={[
              { value: 'chat', label: 'Chat / Completion' },
              { value: 'embedding', label: 'Embedding' },
              { value: 'image', label: 'Image Generation' },
              { value: 'audio', label: 'Audio (Speech/Transcription)' },
              { value: 'video', label: 'Video Generation' },
            ]}
            required
            disabled
            {...form.getInputProps('modelType')}
          />

          {(modelType === 'chat' || modelType === 'embedding') && (
            <>
              <Divider label="Token Pricing" labelPosition="center" />
              {modelType === 'chat' && (
                <Group grow>
                  <NumberInput
                    label="Input Cost (per 1K tokens)"
                    placeholder="0.0000"
                    decimalScale={4}
                    min={0}
                    step={0.0001}
                    leftSection="$"
                    {...form.getInputProps('inputCostPer1K')}
                  />
                  <NumberInput
                    label="Output Cost (per 1K tokens)"
                    placeholder="0.0000"
                    decimalScale={4}
                    min={0}
                    step={0.0001}
                    leftSection="$"
                    {...form.getInputProps('outputCostPer1K')}
                  />
                </Group>
              )}
              {modelType === 'embedding' && (
                <NumberInput
                  label="Embedding Cost (per 1K tokens)"
                  placeholder="0.0000"
                  decimalScale={4}
                  min={0}
                  step={0.0001}
                  leftSection="$"
                  {...form.getInputProps('embeddingCostPer1K')}
                />
              )}
            </>
          )}

          {modelType === 'image' && (
            <>
              <Divider label="Image Pricing" labelPosition="center" />
              <NumberInput
                label="Cost per Image"
                placeholder="0.00"
                decimalScale={2}
                min={0}
                step={0.01}
                leftSection="$"
                {...form.getInputProps('imageCostPerImage')}
              />
            </>
          )}

          {modelType === 'audio' && (
            <>
              <Divider label="Audio Pricing" labelPosition="center" />
              <Group grow>
                <NumberInput
                  label="Cost per Minute"
                  placeholder="0.00"
                  decimalScale={2}
                  min={0}
                  step={0.01}
                  leftSection="$"
                  {...form.getInputProps('audioCostPerMinute')}
                />
                <NumberInput
                  label="Cost per 1K Characters"
                  placeholder="0.00"
                  decimalScale={2}
                  min={0}
                  step={0.01}
                  leftSection="$"
                  {...form.getInputProps('audioCostPerKCharacters')}
                />
              </Group>
              <Group grow>
                <NumberInput
                  label="Input Cost per Minute"
                  placeholder="0.00"
                  decimalScale={2}
                  min={0}
                  step={0.01}
                  leftSection="$"
                  {...form.getInputProps('audioInputCostPerMinute')}
                  description="For transcription services"
                />
                <NumberInput
                  label="Output Cost per Minute"
                  placeholder="0.00"
                  decimalScale={2}
                  min={0}
                  step={0.01}
                  leftSection="$"
                  {...form.getInputProps('audioOutputCostPerMinute')}
                  description="For speech generation"
                />
              </Group>
            </>
          )}

          {modelType === 'video' && (
            <>
              <Divider label="Video Pricing" labelPosition="center" />
              <NumberInput
                label="Cost per Second"
                placeholder="0.00"
                decimalScale={2}
                min={0}
                step={0.01}
                leftSection="$"
                {...form.getInputProps('videoCostPerSecond')}
              />
              <Textarea
                label="Resolution Multipliers (JSON)"
                placeholder='{"720p": 1.0, "1080p": 1.5, "4k": 2.5}'
                {...form.getInputProps('videoResolutionMultipliers')}
                description="Optional: JSON object with resolution multipliers"
              />
            </>
          )}

          <Divider label="Additional Settings" labelPosition="center" />

          <Group grow>
            <NumberInput
              label="Priority"
              placeholder="0"
              min={0}
              {...form.getInputProps('priority')}
              description="Higher priority patterns match first"
            />
            <Switch
              label="Active"
              checked={form.values.isActive}
              {...form.getInputProps('isActive', { type: 'checkbox' })}
              description="Enable this pricing configuration"
            />
          </Group>

          <Textarea
            label="Description"
            placeholder="Optional notes about this pricing configuration"
            {...form.getInputProps('description')}
          />

          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={updateMutation.isPending}>
              Update Pricing
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}