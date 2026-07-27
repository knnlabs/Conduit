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
import { CreateModelCostDto, PricingModel, ModelTypeUtils } from '@/lib/admin-api';
const getModelTypeSelectOptions = ModelTypeUtils.getSelectOptions;
import { ModelMappingSelector } from '../components/ModelMappingSelector';
import { PricingModelSelector } from '../components/PricingModelSelector';
import {
  createModelCostFormValues,
  modelCostFormValidation,
  toCreateModelCostDto,
  type ModelCostFormValues,
} from '../utils/modelCostForm';

export default function AddModelCostV2Page() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { createModelCost } = useModelCostsApi();
  
  const form = useForm<ModelCostFormValues>({
    initialValues: createModelCostFormValues(),
    validate: modelCostFormValidation,
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

  const handleSubmit = (values: ModelCostFormValues) => {
    createMutation.mutate(toCreateModelCostDto(values));
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
