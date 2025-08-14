'use client';

import React from 'react';
import { Card, Text, Stack, Group, Badge, Divider } from '@mantine/core';
import { ModelCost } from '../types/modelCost';
import { formatters } from '@/lib/utils/formatters';

interface CostPreviewProps {
  modelCost: Partial<ModelCost>;
}

interface CostExample {
  label: string;
  description?: string;
  cost: number;
  breakdown?: string;
}

export const CostPreview: React.FC<CostPreviewProps> = ({ modelCost }) => {
  const formatCost = (value: number, precision: number = 4) => {
    return formatters.currency(value, { currency: 'USD', precision });
  };

  const calculateExamples = (): CostExample[] => {
    const examples: CostExample[] = [];

    // Token-based examples
    if (modelCost.inputCostPerMillionTokens || modelCost.outputCostPerMillionTokens) {
      // Basic token example (costs are per million tokens)
      const inputCost = (modelCost.inputCostPerMillionTokens ?? 0) * 1000 / 1000000;
      const outputCost = (modelCost.outputCostPerMillionTokens ?? 0) * 500 / 1000000;
      examples.push({
        label: '1K input + 500 output tokens',
        cost: inputCost + outputCost,
        breakdown: `${formatCost(inputCost)} + ${formatCost(outputCost)}`
      });

      // Cached token example
      if (modelCost.cachedInputCostPerMillionTokens) {
        const cachedCost = (modelCost.cachedInputCostPerMillionTokens * 800 / 1000000);
        const regularCost = (modelCost.inputCostPerMillionTokens ?? 0) * 200 / 1000000;
        const outputCostCached = (modelCost.outputCostPerMillionTokens ?? 0) * 500 / 1000000;
        examples.push({
          label: '1K tokens (80% cached)',
          description: '800 cached + 200 new input + 500 output',
          cost: cachedCost + regularCost + outputCostCached,
          breakdown: `Cached: ${formatCost(cachedCost)}, New: ${formatCost(regularCost)}, Output: ${formatCost(outputCostCached)}`
        });
      }
    }

    // Embedding example
    if (modelCost.embeddingCostPerMillionTokens) {
      const embeddingCost = (modelCost.embeddingCostPerMillionTokens * 5000 / 1000000);
      examples.push({
        label: '5K embedding tokens',
        cost: embeddingCost
      });
    }

    // Search units example
    if (modelCost.costPerSearchUnit) {
      const searchCost = modelCost.costPerSearchUnit * 5;
      examples.push({
        label: '5 search operations',
        description: '5 queries × 100 documents each',
        cost: searchCost,
        breakdown: `5 × ${formatCost(modelCost.costPerSearchUnit)}`
      });
    }

    // Inference steps example
    if (modelCost.costPerInferenceStep && modelCost.defaultInferenceSteps) {
      const stepCost = modelCost.costPerInferenceStep * modelCost.defaultInferenceSteps;
      examples.push({
        label: `1 image (${modelCost.defaultInferenceSteps} steps)`,
        cost: stepCost,
        breakdown: `${modelCost.defaultInferenceSteps} × ${formatCost(modelCost.costPerInferenceStep, 6)}`
      });

      // Custom step count
      const customSteps = 50;
      const customStepCost = modelCost.costPerInferenceStep * customSteps;
      examples.push({
        label: `1 image (${customSteps} steps)`,
        description: 'High quality generation',
        cost: customStepCost,
        breakdown: `${customSteps} × ${formatCost(modelCost.costPerInferenceStep, 6)}`
      });
    }

    // Image generation example
    if (modelCost.imageCostPerImage) {
      examples.push({
        label: '1 standard image',
        cost: modelCost.imageCostPerImage
      });

      // With quality multiplier
      if (modelCost.imageQualityMultipliers) {
        try {
          const multipliers = JSON.parse(modelCost.imageQualityMultipliers) as Record<string, number>;
          if (multipliers.hd) {
            examples.push({
              label: '1 HD image',
              cost: modelCost.imageCostPerImage * multipliers.hd,
              breakdown: `${formatCost(modelCost.imageCostPerImage)} × ${multipliers.hd}x`
            });
          }
        } catch {
          // Invalid JSON, skip
        }
      }
    }

    // Audio examples
    if (modelCost.audioCostPerMinute) {
      examples.push({
        label: '5 minutes of audio',
        cost: modelCost.audioCostPerMinute * 5,
        breakdown: `5 × ${formatCost(modelCost.audioCostPerMinute, 2)}/min`
      });
    }

    if (modelCost.audioCostPerKCharacters) {
      examples.push({
        label: '10K characters TTS',
        cost: modelCost.audioCostPerKCharacters * 10,
        breakdown: `10 × ${formatCost(modelCost.audioCostPerKCharacters, 2)}/1K chars`
      });
    }

    // Video example
    if (modelCost.videoCostPerSecond) {
      examples.push({
        label: '10 second video',
        cost: modelCost.videoCostPerSecond * 10,
        breakdown: `10 × ${formatCost(modelCost.videoCostPerSecond, 2)}/sec`
      });
    }

    // Batch processing example
    if (modelCost.supportsBatchProcessing && modelCost.batchProcessingMultiplier && examples.length > 0) {
      const firstExample = examples[0];
      examples.push({
        label: `Batch: ${firstExample.label}`,
        description: 'With batch processing discount',
        cost: firstExample.cost * modelCost.batchProcessingMultiplier,
        breakdown: `${formatCost(firstExample.cost)} × ${(modelCost.batchProcessingMultiplier * 100).toFixed(0)}%`
      });
    }

    return examples;
  };

  const examples = calculateExamples();

  if (examples.length === 0) {
    return null;
  }

  return (
    <Card withBorder>
      <Stack gap="sm">
        <Group justify="space-between">
          <Text size="sm" fw={600}>Cost Examples</Text>
          <Badge size="sm" variant="light">Estimates</Badge>
        </Group>
        
        <Divider />
        
        {examples.map((example) => (
          <Stack key={`example-${example.label}-${example.cost}-${example.description ?? ''}`} gap={4}>
            <Group justify="space-between" align="flex-start">
              <Stack gap={2}>
                <Text size="sm">{example.label}</Text>
                {example.description && (
                  <Text size="xs" c="dimmed">{example.description}</Text>
                )}
              </Stack>
              <Text size="sm" fw={600} c="blue">
                {formatCost(example.cost)}
              </Text>
            </Group>
            {example.breakdown && (
              <Text size="xs" c="dimmed" style={{ fontFamily: 'monospace' }}>
                {example.breakdown}
              </Text>
            )}
          </Stack>
        ))}
      </Stack>
    </Card>
  );
};