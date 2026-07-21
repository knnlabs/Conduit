'use client';

import { useEffect, useState, useRef, useMemo, useCallback } from 'react';
import {
  Modal,
  Stack,
  Group,
  Text,
  TextInput,
  NumberInput,
  Select,
  Switch,
  Textarea,
  Button,
  Alert,
  Divider,
  Loader,
  Center,
  Tabs,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import {
  IconInfoCircle,
  IconCurrencyDollar,
  IconSettings,
  IconAdjustments,
} from '@tabler/icons-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  PricingModel,
  ModelType,
  ModelTypeUtils,
  type ModelDto,
  type ModelCostDto,
  type CreateModelCostDto,
  type UpdateModelCostDto,
} from '@/lib/admin-api';
import { useAdminClient } from '@/lib/client/adminClient';
import { extractCapabilities } from '@/utils/typeGuards';
import { PricingModelSelector } from '@/app/model-costs/components/PricingModelSelector';
import { ModelMappingSelector } from '@/app/model-costs/components/ModelMappingSelector';

interface ModelCostEditorModalProps {
  isOpen: boolean;
  model: ModelDto;
  existingCost?: ModelCostDto | null;
  onClose: () => void;
  onSuccess?: () => void;
}

interface FormValues {
  costName: string;
  modelType: ModelType;
  priority: number;
  isActive: boolean;
  description: string;
  // Model provider mappings - which models this cost applies to
  modelProviderMappingIds: number[];
  // Token pricing
  inputCostPerMillionTokens: number;
  outputCostPerMillionTokens: number;
  cachedInputCostPerMillionTokens: number;
  cachedInputWriteCostPerMillionTokens: number;
  embeddingCostPerMillionTokens: number;
  costPerSearchUnit: number;
  // Pricing model
  pricingModel: PricingModel;
  pricingConfiguration: string;
  // Batch processing
  supportsBatchProcessing: boolean;
  batchProcessingMultiplier: number;
}

function getModelTypeFromCapabilities(model: ModelDto): ModelType {
  const capabilities = extractCapabilities(model);
  if (capabilities.supportsVideoGeneration) return ModelType.Video;
  if (capabilities.supportsImageGeneration) return ModelType.Image;
  if (capabilities.supportsEmbeddings) return ModelType.Embedding;
  return ModelType.Chat;
}

export function ModelCostEditorModal({
  isOpen,
  model,
  existingCost,
  onClose,
  onSuccess,
}: ModelCostEditorModalProps) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { executeWithAdmin } = useAdminClient();
  const queryClient = useQueryClient();
  const formInitializedRef = useRef(false);

  const isEditing = !!existingCost;
  const inferredModelType = useMemo(() => getModelTypeFromCapabilities(model), [model]);

  const existingMappings = existingCost?.modelProviderTypeAssociationIds ?? [];
  const mappingsLoading = false;

  const form = useForm<FormValues>({
    initialValues: {
      costName: '',
      modelType: ModelType.Chat,
      priority: 0,
      isActive: true,
      description: '',
      modelProviderMappingIds: [],
      inputCostPerMillionTokens: 0,
      outputCostPerMillionTokens: 0,
      cachedInputCostPerMillionTokens: 0,
      cachedInputWriteCostPerMillionTokens: 0,
      embeddingCostPerMillionTokens: 0,
      costPerSearchUnit: 0,
      pricingModel: PricingModel.Standard,
      pricingConfiguration: '',
      supportsBatchProcessing: false,
      batchProcessingMultiplier: 0.5,
    },
    validate: {
      costName: (value) => (!value ? 'Cost name is required' : null),
    },
  });

  // Populate form when modal opens - use ref to prevent infinite loops
  useEffect(() => {
    if (!isOpen) {
      // Reset the ref when modal closes so it can initialize again next time
      formInitializedRef.current = false;
      return;
    }

    // Only initialize once per modal session
    if (formInitializedRef.current) {
      return;
    }

    // When editing, wait for mappings to load before initializing
    if (existingCost && mappingsLoading) {
      return;
    }

    formInitializedRef.current = true;

    if (existingCost) {
      form.setValues({
        costName: existingCost.costName ?? '',
        modelType: existingCost.modelType ?? inferredModelType,
        priority: existingCost.priority ?? 0,
        isActive: existingCost.isActive ?? true,
        description: existingCost.description ?? '',
        modelProviderMappingIds: existingMappings ?? [],
        inputCostPerMillionTokens: existingCost.inputCostPerMillionTokens ?? 0,
        outputCostPerMillionTokens: existingCost.outputCostPerMillionTokens ?? 0,
        cachedInputCostPerMillionTokens: existingCost.cachedInputCostPerMillionTokens ?? 0,
        cachedInputWriteCostPerMillionTokens: existingCost.cachedInputWriteCostPerMillionTokens ?? 0,
        embeddingCostPerMillionTokens: existingCost.embeddingCostPerMillionTokens ?? 0,
        costPerSearchUnit: existingCost.costPerSearchUnit ?? 0,
        pricingModel: existingCost.pricingModel ?? PricingModel.Standard,
        pricingConfiguration: existingCost.pricingConfiguration ?? '',
        supportsBatchProcessing: existingCost.supportsBatchProcessing ?? false,
        batchProcessingMultiplier: existingCost.batchProcessingMultiplier ?? 0.5,
      });
    } else {
      // Reset form for new cost
      form.reset();
      form.setFieldValue('costName', `${model.name ?? 'Model'} Pricing`);
      form.setFieldValue('modelType', inferredModelType);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, mappingsLoading, existingMappings]);

  const saveMutation = useMutation({
    mutationFn: async (values: FormValues) => {
      setLoading(true);
      setError(null);

      try {
        if (isEditing && existingCost?.id) {
          // Update existing cost
          const updateData: UpdateModelCostDto = {
            costName: values.costName,
            modelType: values.modelType,
            priority: values.priority,
            isActive: values.isActive,
            description: values.description || undefined,
            inputCostPerMillionTokens: values.inputCostPerMillionTokens,
            outputCostPerMillionTokens: values.outputCostPerMillionTokens,
            cachedInputCostPerMillionTokens: values.cachedInputCostPerMillionTokens > 0 ? values.cachedInputCostPerMillionTokens : undefined,
            cachedInputWriteCostPerMillionTokens: values.cachedInputWriteCostPerMillionTokens > 0 ? values.cachedInputWriteCostPerMillionTokens : undefined,
            embeddingCostPerMillionTokens: values.embeddingCostPerMillionTokens > 0 ? values.embeddingCostPerMillionTokens : undefined,
            costPerSearchUnit: values.costPerSearchUnit > 0 ? values.costPerSearchUnit : undefined,
            pricingModel: values.pricingModel,
            pricingConfiguration: values.pricingModel !== PricingModel.Standard ? values.pricingConfiguration : undefined,
            supportsBatchProcessing: values.supportsBatchProcessing,
            batchProcessingMultiplier: values.supportsBatchProcessing && values.batchProcessingMultiplier > 0 ? values.batchProcessingMultiplier : undefined,
            modelProviderTypeAssociationIds: values.modelProviderMappingIds,
          };

          await executeWithAdmin(client =>
            client.modelCosts.update(existingCost.id, updateData)
          );
        } else {
          // Create new cost with selected model mappings
          const createData: CreateModelCostDto = {
            costName: values.costName,
            modelType: values.modelType,
            priority: values.priority,
            description: values.description || undefined,
            inputCostPerMillionTokens: values.inputCostPerMillionTokens,
            outputCostPerMillionTokens: values.outputCostPerMillionTokens,
            cachedInputCostPerMillionTokens: values.cachedInputCostPerMillionTokens > 0 ? values.cachedInputCostPerMillionTokens : undefined,
            cachedInputWriteCostPerMillionTokens: values.cachedInputWriteCostPerMillionTokens > 0 ? values.cachedInputWriteCostPerMillionTokens : undefined,
            embeddingCostPerMillionTokens: values.embeddingCostPerMillionTokens > 0 ? values.embeddingCostPerMillionTokens : undefined,
            costPerSearchUnit: values.costPerSearchUnit > 0 ? values.costPerSearchUnit : undefined,
            pricingModel: values.pricingModel,
            pricingConfiguration: values.pricingModel !== PricingModel.Standard ? values.pricingConfiguration : undefined,
            supportsBatchProcessing: values.supportsBatchProcessing,
            batchProcessingMultiplier: values.supportsBatchProcessing && values.batchProcessingMultiplier > 0 ? values.batchProcessingMultiplier : undefined,
            modelProviderTypeAssociationIds: values.modelProviderMappingIds,
          };

          await executeWithAdmin(client =>
            client.modelCosts.create(createData)
          );
        }

        // Invalidate queries
        await queryClient.invalidateQueries({ queryKey: ['model-costs'] });
        await queryClient.invalidateQueries({ queryKey: ['models'] });

        onSuccess?.();
        onClose();
      } catch (err) {
        console.error('Failed to save model cost:', err);
        const errorMessage = err instanceof Error ? err.message : 'Unknown error';
        setError(`Failed to save pricing: ${errorMessage}`);
        throw err;
      } finally {
        setLoading(false);
      }
    },
  });

  const handleSubmit = form.onSubmit((values) => {
    void saveMutation.mutate(values);
  });

  const isRulesBased = form.values.pricingModel === PricingModel.RulesBased ||
                       form.values.pricingModel === PricingModel.PerVideo ||
                       form.values.pricingModel === PricingModel.PerSecondVideo ||
                       form.values.pricingModel === PricingModel.InferenceSteps ||
                       form.values.pricingModel === PricingModel.TieredTokens ||
                       form.values.pricingModel === PricingModel.PerImage;

  // Memoize callbacks to prevent infinite loops in child components
  const handlePricingModelChange = useCallback((model: PricingModel) => {
    form.setFieldValue('pricingModel', model);
  }, [form]);

  const handleConfigurationChange = useCallback((config: string) => {
    form.setFieldValue('pricingConfiguration', config);
  }, [form]);

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title={
        <Group gap="xs">
          <IconCurrencyDollar size={20} />
          <Text fw={600}>{isEditing ? 'Edit' : 'Add'} Pricing for {model.name ?? 'Model'}</Text>
        </Group>
      }
      size="xl"
    >
      {isEditing && mappingsLoading ? (
        <Center py="xl">
          <Stack align="center" gap="sm">
            <Loader size="md" />
            <Text size="sm" c="dimmed">Loading pricing configuration...</Text>
          </Stack>
        </Center>
      ) : (
      <form onSubmit={handleSubmit}>
        <Stack gap="md">
          {error && (
            <Alert color="red" variant="light" icon={<IconInfoCircle size={16} />}>
              {error}
            </Alert>
          )}

          <Tabs defaultValue="basic">
            <Tabs.List>
              <Tabs.Tab value="basic" leftSection={<IconSettings size={16} />}>
                Basic Info
              </Tabs.Tab>
              <Tabs.Tab value="pricing" leftSection={<IconCurrencyDollar size={16} />}>
                Pricing
              </Tabs.Tab>
              <Tabs.Tab value="advanced" leftSection={<IconAdjustments size={16} />}>
                Advanced
              </Tabs.Tab>
            </Tabs.List>

            <Tabs.Panel value="basic" pt="md">
              <Stack gap="md">
                <TextInput
                  label="Cost Name"
                  placeholder="e.g., GPT-4 Standard Pricing"
                  required
                  {...form.getInputProps('costName')}
                  description="A descriptive name for this pricing configuration"
                />

                <Group grow>
                  <Select
                    label="Model Type"
                    data={ModelTypeUtils.getSelectOptions()}
                    {...form.getInputProps('modelType')}
                  />
                  <NumberInput
                    label="Priority"
                    min={0}
                    {...form.getInputProps('priority')}
                    description="Higher priority matches first"
                  />
                </Group>

                <Group>
                  <Switch
                    label="Active"
                    checked={form.values.isActive}
                    {...form.getInputProps('isActive', { type: 'checkbox' })}
                  />
                </Group>

                <Textarea
                  label="Description"
                  placeholder="Optional notes about this pricing"
                  {...form.getInputProps('description')}
                  rows={2}
                />

                <Divider label="Model Mappings" labelPosition="center" />

                <ModelMappingSelector
                  value={form.values.modelProviderMappingIds}
                  onChange={(ids) => form.setFieldValue('modelProviderMappingIds', ids)}
                  description="Select model provider mappings that should use this pricing configuration"
                  placeholder="Select models to apply this cost to..."
                />
              </Stack>
            </Tabs.Panel>

            <Tabs.Panel value="pricing" pt="md">
              <Stack gap="md">
                <PricingModelSelector
                  pricingModel={form.values.pricingModel}
                  pricingConfiguration={form.values.pricingConfiguration}
                  onPricingModelChange={handlePricingModelChange}
                  onConfigurationChange={handleConfigurationChange}
                />

                {!isRulesBased && (
                  <>
                    <Divider label="Token Costs (per million)" labelPosition="center" />

                    <Group grow>
                      <NumberInput
                        label="Input Cost"
                        placeholder="0.00"
                        min={0}
                        step={0.01}
                        decimalScale={6}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('inputCostPerMillionTokens')}
                      />
                      <NumberInput
                        label="Output Cost"
                        placeholder="0.00"
                        min={0}
                        step={0.01}
                        decimalScale={6}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('outputCostPerMillionTokens')}
                      />
                    </Group>

                    {(inferredModelType === ModelType.Embedding || form.values.embeddingCostPerMillionTokens > 0) && (
                      <NumberInput
                        label="Embedding Cost"
                        placeholder="0.00"
                        min={0}
                        step={0.01}
                        decimalScale={6}
                        leftSection={<IconCurrencyDollar size={16} />}
                        {...form.getInputProps('embeddingCostPerMillionTokens')}
                        description="Per million tokens for embedding models"
                      />
                    )}
                  </>
                )}
              </Stack>
            </Tabs.Panel>

            <Tabs.Panel value="advanced" pt="md">
              <Stack gap="md">
                <Divider label="Prompt Caching" labelPosition="center" />
                <Group grow>
                  <NumberInput
                    label="Cache Read Cost"
                    placeholder="0.00"
                    min={0}
                    step={0.01}
                    decimalScale={6}
                    leftSection={<IconCurrencyDollar size={16} />}
                    {...form.getInputProps('cachedInputCostPerMillionTokens')}
                    description="Per million cached tokens"
                  />
                  <NumberInput
                    label="Cache Write Cost"
                    placeholder="0.00"
                    min={0}
                    step={0.01}
                    decimalScale={6}
                    leftSection={<IconCurrencyDollar size={16} />}
                    {...form.getInputProps('cachedInputWriteCostPerMillionTokens')}
                    description="Per million tokens written"
                  />
                </Group>

                <Divider label="Search Units" labelPosition="center" />
                <NumberInput
                  label="Cost per Search Unit"
                  placeholder="0.00"
                  min={0}
                  step={0.001}
                  decimalScale={6}
                  leftSection={<IconCurrencyDollar size={16} />}
                  {...form.getInputProps('costPerSearchUnit')}
                  description="Per 1000 search units (for reranking models)"
                />

                <Divider label="Batch Processing" labelPosition="center" />
                <Group grow align="flex-start">
                  <Switch
                    label="Supports Batch Processing"
                    checked={form.values.supportsBatchProcessing}
                    {...form.getInputProps('supportsBatchProcessing', { type: 'checkbox' })}
                    description="Enable batch API discounts"
                  />
                  {form.values.supportsBatchProcessing && (
                    <NumberInput
                      label="Batch Multiplier"
                      placeholder="0.5"
                      min={0}
                      max={1}
                      step={0.1}
                      decimalScale={2}
                      {...form.getInputProps('batchProcessingMultiplier')}
                      description="0.5 = 50% discount"
                    />
                  )}
                </Group>
              </Stack>
            </Tabs.Panel>
          </Tabs>

          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={onClose} disabled={loading}>
              Cancel
            </Button>
            <Button type="submit" loading={loading || saveMutation.isPending}>
              {isEditing ? 'Save Changes' : 'Create Pricing'}
            </Button>
          </Group>
        </Stack>
      </form>
      )}
    </Modal>
  );
}
