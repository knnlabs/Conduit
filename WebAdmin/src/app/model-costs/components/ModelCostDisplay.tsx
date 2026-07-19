'use client';

import React from 'react';
import { Stack, Group, Text, Badge } from '@mantine/core';
import { ModelCost } from '../types/modelCost';
import { formatters } from '@/lib/utils/formatters';

interface ModelCostDisplayProps {
  modelCost: ModelCost;
  compact?: boolean;
}

export const ModelCostDisplay: React.FC<ModelCostDisplayProps> = ({ 
  modelCost, 
  compact = false 
}) => {
  const formatCost = (value: number, precision: number = 4) => {
    return formatters.currency(value, { currency: 'USD', precision });
  };

  return (
    <Stack gap={compact ? 'xs' : 'sm'}>
      {/* Token costs */}
      {(modelCost.inputCostPerMillionTokens ?? modelCost.outputCostPerMillionTokens) && (
        <>
          {modelCost.inputCostPerMillionTokens && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Input:</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.inputCostPerMillionTokens, 2)}/M tokens
              </Text>
              {modelCost.cachedInputCostPerMillionTokens && (
                <Text size="xs" c="teal">
                  (Cached: {formatCost(modelCost.cachedInputCostPerMillionTokens, 2)})
                </Text>
              )}
            </Group>
          )}
          
          {modelCost.outputCostPerMillionTokens && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Output:</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.outputCostPerMillionTokens, 2)}/M tokens
              </Text>
            </Group>
          )}

          {modelCost.cachedInputWriteCostPerMillionTokens && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Cache Write:</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.cachedInputWriteCostPerMillionTokens, 2)}/M tokens
              </Text>
              <Badge size="xs" variant="light" color="blue">Write</Badge>
            </Group>
          )}
        </>
      )}

      {/* Embedding cost */}
      {modelCost.embeddingCostPerMillionTokens && (
        <Group gap="xs">
          <Text size={compact ? 'xs' : 'sm'} c="dimmed">Embedding:</Text>
          <Text size={compact ? 'xs' : 'sm'} fw={500}>
            {formatCost(modelCost.embeddingCostPerMillionTokens, 2)}/M tokens
          </Text>
        </Group>
      )}

      {/* Search units */}
      {modelCost.costPerSearchUnit && (
        <Group gap="xs">
          <Text size={compact ? 'xs' : 'sm'} c="dimmed">Search:</Text>
          <Text size={compact ? 'xs' : 'sm'} fw={500}>
            {formatCost(modelCost.costPerSearchUnit)}/1K units
          </Text>
          <Badge size="xs" variant="light" color="violet">Rerank</Badge>
        </Group>
      )}

      {/* Inference-step, image, and video pricing (plus the per-input/output audio splits) were
          removed from ModelCostDto in #1038; they now live in pricingConfiguration, which this
          summary does not parse, so those rows are omitted here. */}

      {/* Audio costs */}
      {(modelCost.audioCostPerMinute ?? modelCost.audioCostPerThousandCharacters) && (
        <>
          {modelCost.audioCostPerMinute && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Audio:</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.audioCostPerMinute, 2)}/minute
              </Text>
            </Group>
          )}
          {modelCost.audioCostPerThousandCharacters && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Audio (TTS):</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.audioCostPerThousandCharacters, 2)}/1K chars
              </Text>
            </Group>
          )}
        </>
      )}

      {/* Batch processing */}
      {modelCost.supportsBatchProcessing && modelCost.batchProcessingMultiplier && (
        <Group gap="xs">
          <Text size={compact ? 'xs' : 'sm'} c="dimmed">Batch:</Text>
          <Badge size="xs" color="green">
            {(modelCost.batchProcessingMultiplier * 100).toFixed(0)}% of regular cost
          </Badge>
        </Group>
      )}
    </Stack>
  );
};