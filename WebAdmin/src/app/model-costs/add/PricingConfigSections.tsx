'use client';

import {
  Accordion,
  Stack,
  Group,
  NumberInput,
  Switch,
  Alert,
  JsonInput,
  Textarea,
  Divider,
} from '@mantine/core';
import {
  IconCurrencyDollar,
  IconDatabase,
  IconSparkles,
  IconInfoCircle,
} from '@tabler/icons-react';
import { UseFormReturnType } from '@mantine/form';
import { ModelType } from '@knn_labs/conduit-admin-client';
import { formatters } from '@/lib/utils/formatters';
import { FormValues } from './types';

interface PricingConfigSectionsProps {
  form: UseFormReturnType<FormValues>;
  modelType: ModelType;
}

export function PricingConfigSections({ form, modelType }: PricingConfigSectionsProps) {
  return (
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
  );
}