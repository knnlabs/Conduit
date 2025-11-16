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
import { ModelCostDto, UpdateModelCostDto, ModelType, ModelTypeUtils } from '@knn_labs/conduit-admin-client';
const getModelTypeSelectOptions = ModelTypeUtils.getSelectOptions;
import { ModelMappingSelector } from './ModelMappingSelector';
import { useModelMappings } from '@/hooks/useModelMappingsApi';
import { FormValues, getFormValidation } from './ModelCostFormTypes';
import { ModelCostFormSections } from './ModelCostFormSections';

interface EditModelCostModalProps {
  isOpen: boolean;
  modelCost: ModelCostDto;
  onClose: () => void;
  onSuccess?: () => void;
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
    const extendedMappings = mappings;
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
    validate: getFormValidation(),
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
              <Accordion.Item value="basic">
                <Accordion.Control icon={<IconCurrencyDollar size={20} />}>
                  Basic Token Pricing
                </Accordion.Control>
                <Accordion.Panel>
                  <ModelCostFormSections form={form} modelType={modelType} />
                </Accordion.Panel>
              </Accordion.Item>
            )}

            {modelType === ModelType.Chat && (
              <Accordion.Item value="caching">
                <Accordion.Control icon={<IconDatabase size={20} />}>
                  Prompt Caching
                </Accordion.Control>
                <Accordion.Panel>
                  <ModelCostFormSections form={form} modelType={modelType} />
                </Accordion.Panel>
              </Accordion.Item>
            )}

            {modelType === ModelType.Image && (
              <Accordion.Item value="basic">
                <Accordion.Control icon={<IconCurrencyDollar size={20} />}>
                  Image Generation Pricing
                </Accordion.Control>
                <Accordion.Panel>
                  <ModelCostFormSections form={form} modelType={modelType} />
                </Accordion.Panel>
              </Accordion.Item>
            )}


            {modelType === ModelType.Video && (
              <Accordion.Item value="basic">
                <Accordion.Control icon={<IconCurrencyDollar size={20} />}>
                  Video Generation Pricing
                </Accordion.Control>
                <Accordion.Panel>
                  <ModelCostFormSections form={form} modelType={modelType} />
                </Accordion.Panel>
              </Accordion.Item>
            )}

            <Accordion.Item value="special">
              <Accordion.Control icon={<IconSparkles size={20} />}>
                Special Pricing Models
              </Accordion.Control>
              <Accordion.Panel>
                <ModelCostFormSections form={form} modelType={modelType} />
              </Accordion.Panel>
            </Accordion.Item>
          </Accordion>

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