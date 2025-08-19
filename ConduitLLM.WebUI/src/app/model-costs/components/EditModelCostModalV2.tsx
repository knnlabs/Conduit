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
  Divider,
  Textarea,
  JsonInput,
  Tabs,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { 
  IconCurrencyDollar, 
  IconSparkles,
  IconSettings,
} from '@tabler/icons-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useModelCostsApi } from '../hooks/useModelCostsApi';
import { ModelCostDto, UpdateModelCostDto, PricingModel, ModelType, ModelTypeUtils } from '@knn_labs/conduit-admin-client';
const getModelTypeSelectOptions = ModelTypeUtils.getSelectOptions;
import { ModelMappingSelector } from './ModelMappingSelector';
import { PricingModelSelector } from './PricingModelSelector';
import { useModelMappings } from '@/hooks/useModelMappingsApi';

// ExtendedModelProviderMappingDto type removed - not needed

interface EditModelCostModalV2Props {
  isOpen: boolean;
  modelCost: ModelCostDto;
  onClose: () => void;
  onSuccess?: () => void;
}

interface FormValues {
  costName: string;
  modelProviderMappingIds: number[];
  pricingModel: PricingModel;
  pricingConfiguration: string;
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
  imageResolutionMultipliers: string;
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

export function EditModelCostModalV2({ isOpen, modelCost, onClose, onSuccess }: EditModelCostModalV2Props) {
  const queryClient = useQueryClient();
  const { updateModelCost } = useModelCostsApi();
  const { mappings } = useModelMappings();

  // Find mapping IDs from associated model aliases
  const getMappingIds = (): number[] => {
    if (!modelCost.associatedModelAliases || modelCost.associatedModelAliases.length === 0) {
      return [];
    }
    const extendedMappings = mappings;
    return extendedMappings
      .filter(m => m?.modelAlias && modelCost.associatedModelAliases.includes(m.modelAlias))
      .map(m => m.id);
  };

  // Convert backend data to form values
  const initialValues: FormValues = {
    costName: modelCost.costName,
    modelProviderMappingIds: getMappingIds(),
    pricingModel: modelCost.pricingModel ?? PricingModel.Standard,
    pricingConfiguration: modelCost.pricingConfiguration ?? '',
    modelType: modelCost.modelType,
    // Token costs are already per million tokens
    inputCostPerMillion: modelCost.inputCostPerMillionTokens ?? 0,
    outputCostPerMillion: modelCost.outputCostPerMillionTokens ?? 0,
    cachedInputCostPerMillion: modelCost.cachedInputCostPerMillionTokens ?? 0,
    cachedInputWriteCostPerMillion: modelCost.cachedInputWriteCostPerMillionTokens ?? 0,
    embeddingCostPerMillion: modelCost.embeddingCostPerMillionTokens ?? 0,
    searchUnitCostPer1K: modelCost.costPerSearchUnit ?? 0,
    inferenceStepCost: modelCost.costPerInferenceStep ?? 0,
    defaultInferenceSteps: modelCost.defaultInferenceSteps ?? 0,
    imageCostPerImage: modelCost.imageCostPerImage ?? 0,
    audioCostPerMinute: modelCost.audioCostPerMinute ?? 0,
    audioCostPerKCharacters: modelCost.audioCostPerKCharacters ?? 0,
    audioInputCostPerMinute: modelCost.audioInputCostPerMinute ?? 0,
    audioOutputCostPerMinute: modelCost.audioOutputCostPerMinute ?? 0,
    videoCostPerSecond: modelCost.videoCostPerSecond ?? 0,
    videoResolutionMultipliers: modelCost.videoResolutionMultipliers ?? '',
    imageResolutionMultipliers: modelCost.imageResolutionMultipliers ?? '',
    supportsBatchProcessing: modelCost.supportsBatchProcessing ?? false,
    batchProcessingMultiplier: modelCost.batchProcessingMultiplier ?? 0.5,
    imageQualityMultipliers: modelCost.imageQualityMultipliers ?? '',
    priority: modelCost.priority,
    description: modelCost.description ?? '',
    isActive: modelCost.isActive,
  };

  const form = useForm<FormValues>({
    initialValues,
    validate: {
      costName: (value) => !value?.trim() ? 'Cost name is required' : null,
      modelProviderMappingIds: (value) => !value || value.length === 0 ? 'At least one model must be selected' : null,
      priority: (value) => value < 0 ? 'Priority must be non-negative' : null,
      pricingConfiguration: (value, values) => {
        if (values.pricingModel !== PricingModel.Standard && !value) {
          return 'Configuration is required for this pricing model';
        }
        if (value) {
          try {
            JSON.parse(value);
          } catch {
            return 'Invalid JSON format';
          }
        }
        return null;
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
    const updates: UpdateModelCostDto = {
      id: modelCost.id,
      costName: values.costName,
      modelProviderMappingIds: values.modelProviderMappingIds,
      pricingModel: values.pricingModel,
      pricingConfiguration: values.pricingConfiguration || undefined,
      modelType: values.modelType,
      priority: values.priority,
      description: values.description || undefined,
      isActive: values.isActive,
      inputCostPerMillionTokens: values.inputCostPerMillion,
      outputCostPerMillionTokens: values.outputCostPerMillion,
      cachedInputCostPerMillionTokens: values.cachedInputCostPerMillion || undefined,
      cachedInputWriteCostPerMillionTokens: values.cachedInputWriteCostPerMillion || undefined,
      embeddingCostPerMillionTokens: values.embeddingCostPerMillion || undefined,
      costPerSearchUnit: values.searchUnitCostPer1K || undefined,
      costPerInferenceStep: values.inferenceStepCost || undefined,
      defaultInferenceSteps: values.defaultInferenceSteps || undefined,
      imageCostPerImage: values.imageCostPerImage || undefined,
      audioCostPerMinute: values.audioCostPerMinute || undefined,
      audioCostPerKCharacters: values.audioCostPerKCharacters || undefined,
      audioInputCostPerMinute: values.audioInputCostPerMinute || undefined,
      audioOutputCostPerMinute: values.audioOutputCostPerMinute || undefined,
      videoCostPerSecond: values.videoCostPerSecond || undefined,
      videoResolutionMultipliers: values.videoResolutionMultipliers || undefined,
      imageResolutionMultipliers: values.imageResolutionMultipliers || undefined,
      supportsBatchProcessing: values.supportsBatchProcessing,
      batchProcessingMultiplier: values.supportsBatchProcessing ? values.batchProcessingMultiplier : undefined,
      imageQualityMultipliers: values.imageQualityMultipliers || undefined,
    };

    updateMutation.mutate(updates);
  };

  const showStandardFields = form.values.pricingModel === PricingModel.Standard;

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Edit Model Pricing"
      size="xl"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <TextInput
            label="Cost Name"
            placeholder="e.g., GPT-4 Turbo Standard Pricing"
            required
            {...form.getInputProps('costName')}
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
            {...form.getInputProps('modelType')}
          />

          <Divider label="Pricing Model Configuration" labelPosition="center" />

          <PricingModelSelector
            pricingModel={form.values.pricingModel}
            pricingConfiguration={form.values.pricingConfiguration}
            onPricingModelChange={(model) => form.setFieldValue('pricingModel', model)}
            onConfigurationChange={(config) => form.setFieldValue('pricingConfiguration', config)}
          />

          {showStandardFields && (
            <>
              <Divider label="Standard Pricing Fields" labelPosition="center" />
              
              <Tabs defaultValue="tokens">
                <Tabs.List>
                  <Tabs.Tab value="tokens" leftSection={<IconCurrencyDollar size={16} />}>
                    Token Costs
                  </Tabs.Tab>
                  <Tabs.Tab value="media" leftSection={<IconSparkles size={16} />}>
                    Media Costs
                  </Tabs.Tab>
                  <Tabs.Tab value="advanced" leftSection={<IconSettings size={16} />}>
                    Advanced
                  </Tabs.Tab>
                </Tabs.List>

                <Tabs.Panel value="tokens" pt="md">
                  <Stack gap="md">
                    <Group grow>
                      <NumberInput
                        label="Input Cost"
                        description="Per million tokens (USD)"
                        placeholder="0.00"
                        decimalScale={2}
                        min={0}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('inputCostPerMillion')}
                      />
                      <NumberInput
                        label="Output Cost"
                        description="Per million tokens (USD)"
                        placeholder="0.00"
                        decimalScale={2}
                        min={0}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('outputCostPerMillion')}
                      />
                    </Group>

                    <Group grow>
                      <NumberInput
                        label="Cached Input Cost"
                        description="Per million tokens (USD)"
                        placeholder="0.00"
                        decimalScale={2}
                        min={0}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('cachedInputCostPerMillion')}
                      />
                      <NumberInput
                        label="Embedding Cost"
                        description="Per million tokens (USD)"
                        placeholder="0.00"
                        decimalScale={2}
                        min={0}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('embeddingCostPerMillion')}
                      />
                    </Group>
                  </Stack>
                </Tabs.Panel>

                <Tabs.Panel value="media" pt="md">
                  <Stack gap="md">
                    <Group grow>
                      <NumberInput
                        label="Image Cost"
                        description="Per image (USD)"
                        placeholder="0.00"
                        decimalScale={4}
                        min={0}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('imageCostPerImage')}
                      />
                      <NumberInput
                        label="Video Cost"
                        description="Per second (USD)"
                        placeholder="0.00"
                        decimalScale={4}
                        min={0}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('videoCostPerSecond')}
                      />
                    </Group>

                    <Group grow>
                      <NumberInput
                        label="Audio Cost (Per Minute)"
                        description="USD per minute"
                        placeholder="0.00"
                        decimalScale={4}
                        min={0}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('audioCostPerMinute')}
                      />
                      <NumberInput
                        label="Audio Cost (Per 1K Chars)"
                        description="USD per 1000 characters"
                        placeholder="0.00"
                        decimalScale={4}
                        min={0}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('audioCostPerKCharacters')}
                      />
                    </Group>

                    <JsonInput
                      label="Resolution Multipliers"
                      description="JSON object with resolution multipliers"
                      placeholder='{"1080p": 1.5, "4k": 2.5}'
                      autosize
                      minRows={2}
                      {...form.getInputProps('videoResolutionMultipliers')}
                    />
                  </Stack>
                </Tabs.Panel>

                <Tabs.Panel value="advanced" pt="md">
                  <Stack gap="md">
                    <Switch
                      label="Supports Batch Processing"
                      description="Enable batch processing discounts"
                      {...form.getInputProps('supportsBatchProcessing', { type: 'checkbox' })}
                    />

                    {form.values.supportsBatchProcessing && (
                      <NumberInput
                        label="Batch Processing Multiplier"
                        description="Discount multiplier (0.5 = 50% discount)"
                        placeholder="0.5"
                        decimalScale={2}
                        min={0}
                        max={1}
                        step={0.1}
                        {...form.getInputProps('batchProcessingMultiplier')}
                      />
                    )}

                    <NumberInput
                      label="Priority"
                      description="Higher priority costs are preferred"
                      placeholder="0"
                      min={0}
                      {...form.getInputProps('priority')}
                    />

                    <Textarea
                      label="Description"
                      placeholder="Additional notes about this pricing configuration"
                      {...form.getInputProps('description')}
                    />

                    <Switch
                      label="Active"
                      description="Enable or disable this pricing configuration"
                      {...form.getInputProps('isActive', { type: 'checkbox' })}
                    />
                  </Stack>
                </Tabs.Panel>
              </Tabs>
            </>
          )}

          <Group justify="flex-end" mt="xl">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={updateMutation.isPending}>
              Save Changes
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}