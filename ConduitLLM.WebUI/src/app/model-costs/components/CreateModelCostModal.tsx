'use client';

import React from 'react';
import {
  Modal,
  NumberInput,
  Select,
  Switch,
  Button,
  Group,
  Stack,
  Alert,
  Divider,
  Textarea,
  Fieldset,
  JsonInput,
  Accordion,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { 
  IconInfoCircle, 
  IconCurrencyDollar, 
  IconDatabase, 
  IconSparkles 
} from '@tabler/icons-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useModelCostsApi } from '../hooks/useModelCostsApi';
import { CreateModelCostDto } from '../types/modelCost';
import { PatternPreview } from './PatternPreview';
import { ModelPatternCombobox } from './ModelPatternCombobox';
import { formatters } from '@/lib/utils/formatters';
import { useProviders } from '@/hooks/useProviderApi';

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
  cachedInputCostPer1K: number;
  cachedInputWriteCostPer1K: number;
  embeddingCostPer1K: number;
  // Other cost types
  searchUnitCostPer1K: number;
  inferenceStepCost: number;
  defaultInferenceSteps: number;
  imageCostPerImage: number;
  audioCostPerMinute: number;
  audioCostPerKCharacters: number;
  audioInputCostPerMinute: number;
  audioOutputCostPerMinute: number;
  videoCostPerSecond: number;
  videoResolutionMultipliers: string;
  // Batch processing
  supportsBatchProcessing: boolean;
  batchProcessingMultiplier: number;
  // Image quality
  imageQualityMultipliers: string;
  // Metadata
  priority: number;
  description: string;
  isActive: boolean;
}

export function CreateModelCostModal({ isOpen, onClose, onSuccess }: CreateModelCostModalProps) {
  const queryClient = useQueryClient();
  const { createModelCost } = useModelCostsApi();
  const { providers, isLoading: isLoadingProviders } = useProviders();

  const form = useForm<FormValues>({
    initialValues: {
      modelIdPattern: '',
      providerName: '',
      modelType: 'chat',
      inputCostPer1K: 0,
      outputCostPer1K: 0,
      cachedInputCostPer1K: 0,
      cachedInputWriteCostPer1K: 0,
      embeddingCostPer1K: 0,
      searchUnitCostPer1K: 0,
      inferenceStepCost: 0,
      defaultInferenceSteps: 0,
      imageCostPerImage: 0,
      audioCostPerMinute: 0,
      audioCostPerKCharacters: 0,
      audioInputCostPerMinute: 0,
      audioOutputCostPerMinute: 0,
      videoCostPerSecond: 0,
      videoResolutionMultipliers: '',
      supportsBatchProcessing: false,
      batchProcessingMultiplier: 0.5,
      imageQualityMultipliers: '',
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
      cachedInputCostPer1K: (value) => value < 0 ? 'Cost must be non-negative' : null,
      cachedInputWriteCostPer1K: (value) => value < 0 ? 'Cost must be non-negative' : null,
      embeddingCostPer1K: (value) => value < 0 ? 'Cost must be non-negative' : null,
      searchUnitCostPer1K: (value) => value < 0 ? 'Cost must be non-negative' : null,
      inferenceStepCost: (value) => value < 0 ? 'Cost must be non-negative' : null,
      defaultInferenceSteps: (value) => value < 0 ? 'Steps must be non-negative' : null,
      imageCostPerImage: (value) => value < 0 ? 'Cost must be non-negative' : null,
      audioCostPerMinute: (value) => value < 0 ? 'Cost must be non-negative' : null,
      videoCostPerSecond: (value) => value < 0 ? 'Cost must be non-negative' : null,
      batchProcessingMultiplier: (value, values) => {
        if (values.supportsBatchProcessing && value) {
          if (value <= 0 || value > 1) {
            return 'Multiplier must be between 0 and 1';
          }
        }
        return null;
      },
      imageQualityMultipliers: (value) => {
        if (!value || value === '{}') return null;
        try {
          const parsed = JSON.parse(value) as unknown;
          if (typeof parsed !== 'object' || Array.isArray(parsed)) {
            return 'Must be a JSON object';
          }
          const parsedObj = parsed as Record<string, unknown>;
          for (const [key, val] of Object.entries(parsedObj)) {
            if (typeof val !== 'number' || val <= 0) {
              return `Value for "${key}" must be a positive number`;
            }
          }
          return null;
        } catch {
          return 'Invalid JSON';
        }
      },
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
      cachedInputTokenCost: values.cachedInputCostPer1K > 0 ? values.cachedInputCostPer1K * 1000000 : undefined,
      cachedInputWriteTokenCost: values.cachedInputWriteCostPer1K > 0 ? values.cachedInputWriteCostPer1K * 1000000 : undefined,
      embeddingTokenCost: values.embeddingCostPer1K > 0 ? values.embeddingCostPer1K * 1000000 : undefined,
      costPerSearchUnit: values.searchUnitCostPer1K > 0 ? values.searchUnitCostPer1K : undefined,
      costPerInferenceStep: values.inferenceStepCost > 0 ? values.inferenceStepCost : undefined,
      defaultInferenceSteps: values.defaultInferenceSteps > 0 ? values.defaultInferenceSteps : undefined,
      imageCostPerImage: values.imageCostPerImage > 0 ? values.imageCostPerImage : undefined,
      audioCostPerMinute: values.audioCostPerMinute > 0 ? values.audioCostPerMinute : undefined,
      audioCostPerKCharacters: values.audioCostPerKCharacters > 0 ? values.audioCostPerKCharacters : undefined,
      audioInputCostPerMinute: values.audioInputCostPerMinute > 0 ? values.audioInputCostPerMinute : undefined,
      audioOutputCostPerMinute: values.audioOutputCostPerMinute > 0 ? values.audioOutputCostPerMinute : undefined,
      videoCostPerSecond: values.videoCostPerSecond > 0 ? values.videoCostPerSecond : undefined,
      videoResolutionMultipliers: values.videoResolutionMultipliers || undefined,
      supportsBatchProcessing: values.supportsBatchProcessing,
      batchProcessingMultiplier: values.supportsBatchProcessing && values.batchProcessingMultiplier > 0 ? values.batchProcessingMultiplier : undefined,
      imageQualityMultipliers: values.imageQualityMultipliers || undefined,
      priority: values.priority,
      description: values.description || undefined
    };

    void createMutation.mutate(data);
  };

  const modelType = form.values.modelType;

  // Create provider options from actual providers
  const providerOptions = providers
    .filter(provider => provider.providerName !== undefined)
    .filter((provider): provider is typeof provider & { providerName: string } => typeof provider.providerName === 'string')
    .map(provider => ({
      value: provider.providerName,
      label: provider.providerName,
      disabled: !provider.isEnabled
    }));

  // Get the selected provider object to pass ID and type
  const selectedProvider = providers.find(p => p.providerName === form.values.providerName);

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

          <ModelPatternCombobox
            value={form.values.modelIdPattern}
            onChange={(value) => form.setFieldValue('modelIdPattern', value)}
            selectedProvider={form.values.providerName}
            selectedProviderId={selectedProvider?.id}
            selectedProviderType={selectedProvider?.providerType}
            error={form.errors.modelIdPattern as string}
            required
          />

          <Select
            label="Provider Name"
            placeholder={isLoadingProviders ? "Loading providers..." : "Select a provider"}
            required
            {...form.getInputProps('providerName')}
            description="Name of the LLM provider"
            data={providerOptions}
            searchable
            disabled={isLoadingProviders || providerOptions.length === 0}
            nothingFoundMessage="No providers configured"
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

          <Accordion variant="contained" defaultValue="basic">
            {(modelType === 'chat' || modelType === 'embedding') && (
              <>
                <Accordion.Item value="basic">
                  <Accordion.Control icon={<IconCurrencyDollar size={20} />}>
                    Basic Token Pricing
                  </Accordion.Control>
                  <Accordion.Panel>
                    <Stack gap="sm">
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
                    </Stack>
                  </Accordion.Panel>
                </Accordion.Item>

                {modelType === 'chat' && (
                  <Accordion.Item value="caching">
                    <Accordion.Control icon={<IconDatabase size={20} />}>
                      Prompt Caching
                    </Accordion.Control>
                    <Accordion.Panel>
                      <Stack gap="sm">
                        <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
                          Prompt caching allows reusing context across requests at reduced rates
                        </Alert>
                        <Group grow>
                          <NumberInput
                            label="Cached Read Cost (per 1K tokens)"
                            placeholder="0.0000"
                            description="Cost for reading from cache"
                            decimalScale={4}
                            min={0}
                            step={0.0001}
                            leftSection="$"
                            {...form.getInputProps('cachedInputCostPer1K')}
                          />
                          <NumberInput
                            label="Cache Write Cost (per 1K tokens)"
                            placeholder="0.0000"
                            description="Cost for writing to cache"
                            decimalScale={4}
                            min={0}
                            step={0.0001}
                            leftSection="$"
                            {...form.getInputProps('cachedInputWriteCostPer1K')}
                          />
                        </Group>
                      </Stack>
                    </Accordion.Panel>
                  </Accordion.Item>
                )}
              </>
            )}

            {modelType === 'image' && (
              <Accordion.Item value="basic">
                <Accordion.Control icon={<IconCurrencyDollar size={20} />}>
                  Image Generation Pricing
                </Accordion.Control>
                <Accordion.Panel>
                  <Stack gap="sm">
                    <NumberInput
                      label="Cost per Image"
                      placeholder="0.00"
                      decimalScale={2}
                      min={0}
                      step={0.01}
                      leftSection="$"
                      {...form.getInputProps('imageCostPerImage')}
                    />
                    <JsonInput
                      label="Image Quality Multipliers"
                      description='JSON object like {"standard": 1.0, "hd": 2.0}'
                      placeholder='{"standard": 1.0, "hd": 2.0}'
                      validationError="Invalid JSON"
                      formatOnBlur
                      autosize
                      minRows={2}
                      {...form.getInputProps('imageQualityMultipliers')}
                    />
                  </Stack>
                </Accordion.Panel>
              </Accordion.Item>
            )}

            {modelType === 'audio' && (
              <Accordion.Item value="basic">
                <Accordion.Control icon={<IconCurrencyDollar size={20} />}>
                  Audio Pricing
                </Accordion.Control>
                <Accordion.Panel>
                  <Stack gap="sm">
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
                  </Stack>
                </Accordion.Panel>
              </Accordion.Item>
            )}

            {modelType === 'video' && (
              <Accordion.Item value="basic">
                <Accordion.Control icon={<IconCurrencyDollar size={20} />}>
                  Video Generation Pricing
                </Accordion.Control>
                <Accordion.Panel>
                  <Stack gap="sm">
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
                  </Stack>
                </Accordion.Panel>
              </Accordion.Item>
            )}

            {/* Special pricing models - available for all model types */}
            <Accordion.Item value="special">
              <Accordion.Control icon={<IconSparkles size={20} />}>
                Special Pricing Models
              </Accordion.Control>
              <Accordion.Panel>
                <Stack gap="sm">
                  <Alert icon={<IconInfoCircle size={16} />} color="violet" variant="light">
                    These pricing models are used by specialized providers
                  </Alert>
                  
                  <NumberInput
                    label="Cost per Search Unit (per 1K units)"
                    placeholder="0.0000"
                    description="For reranking models: 1 search unit = 1 query + up to 100 documents"
                    decimalScale={4}
                    min={0}
                    step={0.0001}
                    leftSection="$"
                    {...form.getInputProps('searchUnitCostPer1K')}
                  />
                  
                  <Divider variant="dashed" />
                  
                  <Group grow>
                    <NumberInput
                      label="Cost per Inference Step"
                      placeholder="0.000000"
                      description="For step-based image generation"
                      decimalScale={6}
                      min={0}
                      step={0.000001}
                      leftSection="$"
                      {...form.getInputProps('inferenceStepCost')}
                    />
                    <NumberInput
                      label="Default Inference Steps"
                      placeholder="30"
                      description="Default steps for this model"
                      min={0}
                      step={1}
                      {...form.getInputProps('defaultInferenceSteps')}
                    />
                  </Group>
                  
                  {form.values.inferenceStepCost > 0 && form.values.defaultInferenceSteps > 0 && (
                    <Alert color="blue" variant="light">
                      Default image cost: {formatters.currency(
                        form.values.inferenceStepCost * form.values.defaultInferenceSteps,
                        { currency: 'USD', precision: 4 }
                      )} ({form.values.defaultInferenceSteps} steps × {formatters.currency(
                        form.values.inferenceStepCost,
                        { currency: 'USD', precision: 6 }
                      )})
                    </Alert>
                  )}
                </Stack>
              </Accordion.Panel>
            </Accordion.Item>
          </Accordion>

          <Fieldset legend="Batch Processing">
            <Stack gap="sm">
              <Switch
                label="Supports Batch Processing"
                checked={form.values.supportsBatchProcessing}
                onChange={(event) => 
                  form.setFieldValue('supportsBatchProcessing', event.currentTarget.checked)}
                description="Enable batch API support for this model"
              />
              
              {form.values.supportsBatchProcessing && (
                <NumberInput
                  label="Batch Processing Multiplier"
                  description="Discount multiplier (e.g., 0.5 for 50% off)"
                  placeholder="0.5"
                  min={0.01}
                  max={1}
                  step={0.01}
                  decimalScale={2}
                  {...form.getInputProps('batchProcessingMultiplier')}
                />
              )}
            </Stack>
          </Fieldset>

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