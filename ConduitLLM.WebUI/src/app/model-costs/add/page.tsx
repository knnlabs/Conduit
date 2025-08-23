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
  Alert,
  Textarea,
  Paper,
  Grid,
  Box,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { 
  IconInfoCircle, 
  IconArrowLeft
} from '@tabler/icons-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useModelCostsApi } from '../hooks/useModelCostsApi';
import { CreateModelCostDto, ModelTypeUtils } from '@knn_labs/conduit-admin-client';
const getModelTypeSelectOptions = ModelTypeUtils.getSelectOptions;
import { ModelMappingSelector } from '../components/ModelMappingSelector';
import { FormValues } from './types';
import { getInitialValues, getFormValidation } from './validation';
import { PricingConfigSections } from './PricingConfigSections';

export default function AddModelCostPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { createModelCost } = useModelCostsApi();
  const form = useForm<FormValues>({
    initialValues: getInitialValues(),
    validate: getFormValidation(),
  });

  const createMutation = useMutation({
    mutationFn: async (data: CreateModelCostDto) => createModelCost(data),
    onSuccess: () => {
      // Invalidate all model-costs queries regardless of their parameters
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
      modelType: values.modelType,
      // Values are already per million tokens
      inputCostPerMillionTokens: values.inputCostPerMillion,
      outputCostPerMillionTokens: values.outputCostPerMillion,
      cachedInputCostPerMillionTokens: values.cachedInputCostPerMillion > 0 ? values.cachedInputCostPerMillion : undefined,
      cachedInputWriteCostPerMillionTokens: values.cachedInputWriteCostPerMillion > 0 ? values.cachedInputWriteCostPerMillion : undefined,
      embeddingCostPerMillionTokens: values.embeddingCostPerMillion > 0 ? values.embeddingCostPerMillion : undefined,
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

  const handleCancel = () => {
    router.push('/model-costs');
  };

  const modelType = form.values.modelType;

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
        
        <Title order={2}>Add Model Pricing</Title>
        <Text c="dimmed" size="sm" mt={4}>
          Configure pricing for AI models to enable accurate cost tracking
        </Text>
      </Box>

      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Grid gutter="lg">
          {/* Left Column - Basic Configuration */}
          <Grid.Col span={{ base: 12, lg: 6 }}>
            <Stack gap="md">
              <Paper p="md" shadow="xs">
                <Stack gap="md">
                  <Alert icon={<IconInfoCircle size={16} />} color="blue">
                    Configure pricing for AI models by assigning costs to specific model mappings.
                    Token costs are entered per 1,000 tokens for convenience.
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
                    {...form.getInputProps('modelType')}
                  />
                </Stack>
              </Paper>

              {/* Additional Settings */}
              <Paper p="md" shadow="xs">
                <Stack gap="md">
                  <Title order={5}>Additional Settings</Title>
                  
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
                    rows={3}
                  />
                </Stack>
              </Paper>
            </Stack>
          </Grid.Col>

          {/* Right Column - Pricing Configuration */}
          <Grid.Col span={{ base: 12, lg: 6 }}>
            <PricingConfigSections form={form} modelType={modelType} />
          </Grid.Col>
        </Grid>

        <Group justify="flex-end" mt="xl">
          <Button variant="subtle" onClick={handleCancel}>
            Cancel
          </Button>
          <Button type="submit" loading={createMutation.isPending}>
            Create Pricing
          </Button>
        </Group>
      </form>
    </Container>
  );
}