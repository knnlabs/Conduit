'use client';

import React from 'react';
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
import { useModelCostsApi } from '../hooks/useModelCostsApi';
import { CreateModelCostDto } from '../types/modelCost';
import { PatternPreview } from './PatternPreview';

interface CreateModelCostModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

interface FormValues {
  modelIdPattern: string;
  providerName: string;
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

export function CreateModelCostModal({ isOpen, onClose, onSuccess }: CreateModelCostModalProps) {
  const queryClient = useQueryClient();
  const { createModelCost } = useModelCostsApi();

  const form = useForm<FormValues>({
    initialValues: {
      modelIdPattern: '',
      providerName: '',
      modelType: 'chat',
      inputCostPer1K: 0,
      outputCostPer1K: 0,
      embeddingCostPer1K: 0,
      imageCostPerImage: 0,
      audioCostPerMinute: 0,
      audioCostPerKCharacters: 0,
      audioInputCostPerMinute: 0,
      audioOutputCostPerMinute: 0,
      videoCostPerSecond: 0,
      videoResolutionMultipliers: '',
      priority: 0,
      description: '',
      isActive: true,
    },
    validate: {
      modelIdPattern: (value) => !value?.trim() ? 'Model pattern is required' : null,
      providerName: (value) => !value?.trim() ? 'Provider name is required' : null,
      priority: (value) => value < 0 ? 'Priority must be non-negative' : null,
      inputCostPer1K: (value) => value < 0 ? 'Cost must be non-negative' : null,
      outputCostPer1K: (value) => value < 0 ? 'Cost must be non-negative' : null,
      embeddingCostPer1K: (value) => value < 0 ? 'Cost must be non-negative' : null,
      imageCostPerImage: (value) => value < 0 ? 'Cost must be non-negative' : null,
      audioCostPerMinute: (value) => value < 0 ? 'Cost must be non-negative' : null,
      videoCostPerSecond: (value) => value < 0 ? 'Cost must be non-negative' : null,
    },
  });

  const createMutation = useMutation({
    mutationFn: async (data: CreateModelCostDto) => createModelCost(data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['model-costs'] });
      form.reset();
      onSuccess?.();
      onClose();
    },
  });

  const handleSubmit = (values: FormValues) => {
    const data: CreateModelCostDto = {
      modelIdPattern: values.modelIdPattern,
      providerName: values.providerName,
      modelType: values.modelType,
      // Convert from per 1K to per 1M tokens for backend
      inputTokenCost: values.inputCostPer1K * 1000000,
      outputTokenCost: values.outputCostPer1K * 1000000,
      embeddingTokenCost: values.embeddingCostPer1K > 0 ? values.embeddingCostPer1K * 1000000 : undefined,
      imageCostPerImage: values.imageCostPerImage > 0 ? values.imageCostPerImage : undefined,
      audioCostPerMinute: values.audioCostPerMinute > 0 ? values.audioCostPerMinute : undefined,
      audioCostPerKCharacters: values.audioCostPerKCharacters > 0 ? values.audioCostPerKCharacters : undefined,
      audioInputCostPerMinute: values.audioInputCostPerMinute > 0 ? values.audioInputCostPerMinute : undefined,
      audioOutputCostPerMinute: values.audioOutputCostPerMinute > 0 ? values.audioOutputCostPerMinute : undefined,
      videoCostPerSecond: values.videoCostPerSecond > 0 ? values.videoCostPerSecond : undefined,
      videoResolutionMultipliers: values.videoResolutionMultipliers || undefined,
      priority: values.priority,
      description: values.description || undefined,
      supportsBatchProcessing: false, // Default to false for new costs
    };

    void createMutation.mutate(data);
  };

  const modelType = form.values.modelType;

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Add Model Pricing"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <Alert icon={<IconInfoCircle size={16} />} color="blue">
            Configure pricing for AI models. Use patterns like &quot;openai/gpt-4*&quot; to match multiple models.
            Token costs are entered per 1,000 tokens for convenience.
          </Alert>

          <TextInput
            label="Model Pattern"
            placeholder="e.g., openai/gpt-4, anthropic/claude-3*, minimax/abab6.5g"
            required
            {...form.getInputProps('modelIdPattern')}
            description="Exact model ID or pattern with * wildcard"
          />

          <TextInput
            label="Provider Name"
            placeholder="e.g., OpenAI, Anthropic, MiniMax"
            required
            {...form.getInputProps('providerName')}
            description="Name of the LLM provider"
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
            <Button type="submit" loading={createMutation.isPending}>
              Create Pricing
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}