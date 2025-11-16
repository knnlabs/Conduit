'use client';

import React from 'react';
import {
  Container,
  Title,
  Text,
  TextInput,
  NumberInput,
  Select,
  Switch,
  Button,
  Group,
  Stack,
  Textarea,
  Paper,
  Box,
  Tabs,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { 
  IconCurrencyDollar, 
  IconSparkles,
  IconArrowLeft,
  IconSettings,
} from '@tabler/icons-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useModelCostsApi } from '../hooks/useModelCostsApi';
import { CreateModelCostDto, PricingModel, ModelType, ModelTypeUtils } from '@knn_labs/conduit-admin-client';
const getModelTypeSelectOptions = ModelTypeUtils.getSelectOptions;
import { ModelMappingSelector } from '../components/ModelMappingSelector';
import { PricingModelSelector } from '../components/PricingModelSelector';

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

export default function AddModelCostV2Page() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { createModelCost } = useModelCostsApi();
  
  const form = useForm<FormValues>({
    initialValues: {
      costName: '',
      modelProviderMappingIds: [],
      pricingModel: PricingModel.Standard,
      pricingConfiguration: '',
      modelType: ModelType.Chat,
      inputCostPerMillion: 0,
      outputCostPerMillion: 0,
      cachedInputCostPerMillion: 0,
      cachedInputWriteCostPerMillion: 0,
      embeddingCostPerMillion: 0,
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
      imageResolutionMultipliers: '',
      supportsBatchProcessing: false,
      batchProcessingMultiplier: 0.5,
      imageQualityMultipliers: '',
      priority: 0,
      description: '',
      isActive: true,
    },
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

  const createMutation = useMutation({
    mutationFn: async (data: CreateModelCostDto) => createModelCost(data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ 
        queryKey: ['model-costs'],
        exact: false 
      });
      router.push('/model-costs');
    },
  });

  const handleSubmit = (values: FormValues) => {
    const data: CreateModelCostDto = {
      costName: values.costName,
      modelProviderMappingIds: values.modelProviderMappingIds,
      pricingModel: values.pricingModel,
      pricingConfiguration: values.pricingConfiguration || undefined,
      modelType: values.modelType,
      priority: values.priority,
      description: values.description || undefined,
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

    createMutation.mutate(data);
  };

  const handleCancel = () => {
    router.push('/model-costs');
  };

  const showStandardFields = form.values.pricingModel === PricingModel.Standard;

  return (
    <Container size="xl">
      <Box mb="xl">
        <Button
          variant="subtle"
          leftSection={<IconArrowLeft size={16} />}
          onClick={handleCancel}
          mb="md"
        >
          Back to Model Costs
        </Button>
        
        <Title order={2}>Add Model Pricing (Polymorphic)</Title>
        <Text c="dimmed" size="sm" mt={4}>
          Configure advanced polymorphic pricing models for AI services
        </Text>
      </Box>

      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="lg">
          <Paper p="md" shadow="xs">
            <Stack gap="md">
              <TextInput
                label="Cost Name"
                placeholder="e.g., GPT-4 Turbo Standard Pricing"
                required
                {...form.getInputProps('costName')}
              />

              <ModelMappingSelector
                value={form.values.modelProviderMappingIds}
                onChange={(ids) => form.setFieldValue('modelProviderMappingIds', ids)}
                error={form.errors.modelProviderMappingIds as string}
                required
              />

              <Select
                label="Model Type"
                data={getModelTypeSelectOptions()}
                {...form.getInputProps('modelType')}
              />
            </Stack>
          </Paper>

          <Paper p="md" shadow="xs">
            <Stack gap="md">
              <Title order={5}>Pricing Model Configuration</Title>
              
              <PricingModelSelector
                pricingModel={form.values.pricingModel}
                pricingConfiguration={form.values.pricingConfiguration}
                onPricingModelChange={(model) => form.setFieldValue('pricingModel', model)}
                onConfigurationChange={(config) => form.setFieldValue('pricingConfiguration', config)}
              />
            </Stack>
          </Paper>

          {showStandardFields && (
            <Paper p="md" shadow="xs">
              <Stack gap="md">
                <Title order={5}>Standard Pricing Fields</Title>
                
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

                      <Textarea
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
              </Stack>
            </Paper>
          )}

          <Group justify="flex-end" mt="xl">
            <Button variant="subtle" onClick={handleCancel}>
              Cancel
            </Button>
            <Button type="submit" loading={createMutation.isPending}>
              Create Pricing
            </Button>
          </Group>
        </Stack>
      </form>
    </Container>
  );
}