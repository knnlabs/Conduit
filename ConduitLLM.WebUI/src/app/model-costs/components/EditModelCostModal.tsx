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
import { ModelCostDto, UpdateModelCostDto, type ModelProviderMappingDto, ModelType } from '@knn_labs/conduit-admin-client';
import { getModelTypeSelectOptions } from '@/lib/constants/modelTypes';
import { formatters } from '@/lib/utils/formatters';
import { ModelMappingSelector } from './ModelMappingSelector';
import { useModelMappings } from '@/hooks/useModelMappingsApi';

// Extended type to include additional fields from API response
interface ExtendedModelProviderMappingDto extends ModelProviderMappingDto {
  modelAlias?: string;
}

interface EditModelCostModalProps {
  isOpen: boolean;
  modelCost: ModelCostDto;
  onClose: () => void;
  onSuccess?: () => void;
}

interface FormValues {
  costName: string;
  modelProviderMappingIds: number[];
  modelType: ModelType;
  // Token-based costs (per million tokens)
  inputCostPerMillion: number;
  outputCostPerMillion: number;
  cachedInputCostPerMillion: number;
  cachedInputWriteCostPerMillion: number;
  embeddingCostPerMillion: number;
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

export function EditModelCostModal({ isOpen, modelCost, onClose, onSuccess }: EditModelCostModalProps) {
  const queryClient = useQueryClient();
  const { updateModelCost } = useModelCostsApi();
  const { mappings } = useModelMappings();

  // Find mapping IDs from associated model aliases
  const getMappingIds = (): number[] => {
    const aliases = modelCost.associatedModelAliases;
    if (!aliases || aliases.length === 0) {
      return [];
    }
    // Match aliases to mapping IDs
    const extendedMappings = mappings as ExtendedModelProviderMappingDto[];
    return extendedMappings
      .filter(m => m?.modelAlias && aliases.includes(m.modelAlias))
      .map(m => m.id);
  };

  // Convert backend data to form values
  const initialValues: FormValues = {
    costName: modelCost.costName,
    modelProviderMappingIds: getMappingIds(),
    modelType: modelCost.modelType,
    // Token costs are already per million tokens
    inputCostPerMillion: (modelCost.inputCostPerMillionTokens) ?? 0,
    outputCostPerMillion: (modelCost.outputCostPerMillionTokens) ?? 0,
    cachedInputCostPerMillion: (modelCost.cachedInputCostPerMillionTokens as number) ?? 0,
    cachedInputWriteCostPerMillion: (modelCost.cachedInputWriteCostPerMillionTokens as number) ?? 0,
    embeddingCostPerMillion: (modelCost.embeddingCostPerMillionTokens as number) ?? 0,
    searchUnitCostPer1K: (modelCost.costPerSearchUnit as number) ?? 0,
    inferenceStepCost: (modelCost.costPerInferenceStep as number) ?? 0,
    defaultInferenceSteps: (modelCost.defaultInferenceSteps as number) ?? 0,
    imageCostPerImage: (modelCost.imageCostPerImage as number) ?? 0,
    audioCostPerMinute: (modelCost.audioCostPerMinute as number) ?? 0,
    audioCostPerKCharacters: (modelCost.audioCostPerKCharacters as number) ?? 0,
    audioInputCostPerMinute: (modelCost.audioInputCostPerMinute as number) ?? 0,
    audioOutputCostPerMinute: (modelCost.audioOutputCostPerMinute as number) ?? 0,
    videoCostPerSecond: (modelCost.videoCostPerSecond as number) ?? 0,
    videoResolutionMultipliers: (modelCost.videoResolutionMultipliers as string) ?? '',
    supportsBatchProcessing: (modelCost.supportsBatchProcessing) ?? false,
    batchProcessingMultiplier: (modelCost.batchProcessingMultiplier as number) ?? 0.5,
    imageQualityMultipliers: (modelCost.imageQualityMultipliers as string) ?? '',
    priority: modelCost.priority,
    description: (modelCost.description as string) ?? '',
    isActive: modelCost.isActive,
  };

  const form = useForm<FormValues>({
    initialValues,
    validate: {
      costName: (value) => !value?.trim() ? 'Cost name is required' : null,
      modelProviderMappingIds: (value) => !value || value.length === 0 ? 'At least one model must be selected' : null,
      priority: (value) => value < 0 ? 'Priority must be non-negative' : null,
      inputCostPerMillion: (value) => value < 0 ? 'Cost must be non-negative' : null,
      outputCostPerMillion: (value) => value < 0 ? 'Cost must be non-negative' : null,
      cachedInputCostPerMillion: (value) => value < 0 ? 'Cost must be non-negative' : null,
      cachedInputWriteCostPerMillion: (value) => value < 0 ? 'Cost must be non-negative' : null,
      embeddingCostPerMillion: (value) => value < 0 ? 'Cost must be non-negative' : null,
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

  const updateMutation = useMutation({
    mutationFn: async (data: UpdateModelCostDto) => updateModelCost(modelCost.id, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['model-costs'] });
      onSuccess?.();
      onClose();
    },
  });

  const handleSubmit = (values: FormValues) => {
    // Only send changed fields
    const updates: UpdateModelCostDto = {
      id: modelCost.id,
      costName: values.costName,
      modelProviderMappingIds: values.modelProviderMappingIds
    };
    
    // Values are already per million tokens
    if (values.inputCostPerMillion !== modelCost.inputCostPerMillionTokens) {
      updates.inputCostPerMillionTokens = values.inputCostPerMillion;
    }
    if (values.outputCostPerMillion !== modelCost.outputCostPerMillionTokens) {
      updates.outputCostPerMillionTokens = values.outputCostPerMillion;
    }
    if (values.cachedInputCostPerMillion !== modelCost.cachedInputCostPerMillionTokens) {
      updates.cachedInputCostPerMillionTokens = values.cachedInputCostPerMillion || undefined;
    }
    if (values.cachedInputWriteCostPerMillion !== modelCost.cachedInputWriteCostPerMillionTokens) {
      updates.cachedInputWriteCostPerMillionTokens = values.cachedInputWriteCostPerMillion || undefined;
    }
    
    if (values.embeddingCostPerMillion > 0) {
      updates.embeddingCostPerMillionTokens = values.embeddingCostPerMillion;
    }
    
    if (values.searchUnitCostPer1K !== modelCost.costPerSearchUnit) {
      updates.costPerSearchUnit = values.searchUnitCostPer1K || undefined;
    }
    
    if (values.inferenceStepCost !== modelCost.costPerInferenceStep) {
      updates.costPerInferenceStep = values.inferenceStepCost || undefined;
    }
    
    if (values.defaultInferenceSteps !== modelCost.defaultInferenceSteps) {
      updates.defaultInferenceSteps = values.defaultInferenceSteps || undefined;
    }
    
    if (values.imageCostPerImage !== modelCost.imageCostPerImage) {
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
    
    if (values.videoCostPerSecond !== modelCost.videoCostPerSecond) {
      updates.videoCostPerSecond = values.videoCostPerSecond || undefined;
    }
    
    if (values.videoResolutionMultipliers) {
      updates.videoResolutionMultipliers = values.videoResolutionMultipliers;
    }
    
    // Batch processing fields
    if (values.supportsBatchProcessing !== modelCost.supportsBatchProcessing) {
      updates.supportsBatchProcessing = values.supportsBatchProcessing;
    }
    
    if (values.supportsBatchProcessing && values.batchProcessingMultiplier !== modelCost.batchProcessingMultiplier) {
      updates.batchProcessingMultiplier = values.batchProcessingMultiplier || undefined;
    }
    
    // Image quality multipliers
    if (values.imageQualityMultipliers !== modelCost.imageQualityMultipliers) {
      updates.imageQualityMultipliers = values.imageQualityMultipliers || undefined;
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
            Update pricing for {modelCost.costName}. Token costs are displayed per million tokens.
          </Alert>

          <TextInput
            label="Cost Name"
            placeholder="e.g., GPT-4 Standard Pricing, Claude 3 Opus Batch"
            required
            {...form.getInputProps('costName')}
            description="A descriptive name for this pricing configuration"
          />

          <ModelMappingSelector
            value={form.values.modelProviderMappingIds}
            onChange={(value) => form.setFieldValue('modelProviderMappingIds', value)}
            error={form.errors.modelProviderMappingIds as string}
            required
          />

          <Select
            label="Model Type"
            data={getModelTypeSelectOptions()}
            required
            disabled
            {...form.getInputProps('modelType')}
          />

          <Accordion variant="contained" defaultValue="basic">
            {(modelType === ModelType.Chat || modelType === ModelType.Embedding) && (
              <>
                <Accordion.Item value="basic">
                  <Accordion.Control icon={<IconCurrencyDollar size={20} />}>
                    Basic Token Pricing
                  </Accordion.Control>
                  <Accordion.Panel>
                    <Stack gap="sm">
                      {modelType === ModelType.Chat && (
                        <Group grow>
                          <NumberInput
                            label="Input Cost (per million tokens)"
                            placeholder="15.00"
                            decimalScale={2}
                            min={0}
                            step={0.50}
                            leftSection="$"
                            {...form.getInputProps('inputCostPerMillion')}
                          />
                          <NumberInput
                            label="Output Cost (per million tokens)"
                            placeholder="75.00"
                            decimalScale={2}
                            min={0}
                            step={0.50}
                            leftSection="$"
                            {...form.getInputProps('outputCostPerMillion')}
                          />
                        </Group>
                      )}
                      {modelType === ModelType.Embedding && (
                        <NumberInput
                          label="Embedding Cost (per million tokens)"
                          placeholder="1.00"
                          decimalScale={2}
                          min={0}
                          step={0.10}
                          leftSection="$"
                          {...form.getInputProps('embeddingCostPerMillion')}
                        />
                      )}
                    </Stack>
                  </Accordion.Panel>
                </Accordion.Item>

                {modelType === ModelType.Chat && (
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
                            label="Cached Read Cost (per million tokens)"
                            placeholder="0.50"
                            description="Cost for reading from cache"
                            decimalScale={2}
                            min={0}
                            step={0.10}
                            leftSection="$"
                            {...form.getInputProps('cachedInputCostPerMillion')}
                          />
                          <NumberInput
                            label="Cache Write Cost (per million tokens)"
                            placeholder="15.00"
                            description="Cost for writing to cache"
                            decimalScale={2}
                            min={0}
                            step={0.50}
                            leftSection="$"
                            {...form.getInputProps('cachedInputWriteCostPerMillion')}
                          />
                        </Group>
                      </Stack>
                    </Accordion.Panel>
                  </Accordion.Item>
                )}
              </>
            )}

            {modelType === ModelType.Image && (
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

            {modelType === ModelType.Audio && (
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

            {modelType === ModelType.Video && (
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
            <Button type="submit" loading={updateMutation.isPending}>
              Update Pricing
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}