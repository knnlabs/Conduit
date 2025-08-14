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
  Divider,
  Textarea,
  JsonInput,
  Accordion,
  Paper,
  Grid,
  Box,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { 
  IconInfoCircle, 
  IconCurrencyDollar, 
  IconDatabase, 
  IconSparkles,
  IconArrowLeft
} from '@tabler/icons-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useModelCostsApi } from '../hooks/useModelCostsApi';
import { CreateModelCostDto, ModelType } from '@knn_labs/conduit-admin-client';
import { getModelTypeSelectOptions } from '@/lib/constants/modelTypes';
import { formatters } from '@/lib/utils/formatters';
import { ModelMappingSelector } from '../components/ModelMappingSelector';

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

export default function AddModelCostPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { createModelCost } = useModelCostsApi();
  const form = useForm<FormValues>({
    initialValues: {
      costName: '',
      modelProviderMappingIds: [],
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

              {/* Batch Processing */}
              <Accordion.Item value="batch">
                <Accordion.Control>
                  Batch Processing
                </Accordion.Control>
                <Accordion.Panel>
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
                </Accordion.Panel>
              </Accordion.Item>
            </Accordion>
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