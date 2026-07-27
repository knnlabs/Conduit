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
import { ModelCostDto, UpdateModelCostDto, PricingModel, ModelTypeUtils } from '@/lib/admin-api';
const getModelTypeSelectOptions = ModelTypeUtils.getSelectOptions;
import { ModelMappingSelector } from './ModelMappingSelector';
import { PricingModelSelector } from './PricingModelSelector';
import {
  modelCostFormValidation,
  modelCostToFormValues,
  toUpdateModelCostDto,
  type ModelCostFormValues,
} from '../utils/modelCostForm';

// ExtendedModelProviderMappingDto type removed - not needed

interface EditModelCostModalV2Props {
  isOpen: boolean;
  modelCost: ModelCostDto;
  onClose: () => void;
  onSuccess?: () => void;
}

export function EditModelCostModalV2({ isOpen, modelCost, onClose, onSuccess }: EditModelCostModalV2Props) {
  const queryClient = useQueryClient();
  const { updateModelCost } = useModelCostsApi();

  // Convert backend data to form values
  const form = useForm<ModelCostFormValues>({
    initialValues: modelCostToFormValues(modelCost),
    validate: modelCostFormValidation,
  });

  const updateMutation = useMutation({
    mutationFn: async (data: UpdateModelCostDto) => updateModelCost(modelCost.id, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['model-costs'] });
      onSuccess?.();
      onClose();
    },
  });

  const handleSubmit = (values: ModelCostFormValues) => {
    updateMutation.mutate(toUpdateModelCostDto(values));
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
