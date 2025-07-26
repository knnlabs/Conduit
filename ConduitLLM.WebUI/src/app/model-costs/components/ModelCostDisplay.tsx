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
                {formatCost(modelCost.inputCostPerMillionTokens / 1000)}/1K tokens
              </Text>
              {modelCost.cachedInputCostPerMillionTokens && (
                <Text size="xs" c="teal">
                  (Cached: {formatCost(modelCost.cachedInputCostPerMillionTokens / 1000)})
                </Text>
              )}
            </Group>
          )}
          
          {modelCost.outputCostPerMillionTokens && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Output:</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.outputCostPerMillionTokens / 1000)}/1K tokens
              </Text>
            </Group>
          )}

          {modelCost.cachedInputWriteCostPerMillionTokens && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Cache Write:</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.cachedInputWriteCostPerMillionTokens / 1000)}/1K tokens
              </Text>
              <Badge size="xs" variant="light" color="blue">Write</Badge>
            </Group>
          )}
        </>
      )}

      {/* Embedding cost */}
      {modelCost.embeddingTokenCost && (
        <Group gap="xs">
          <Text size={compact ? 'xs' : 'sm'} c="dimmed">Embedding:</Text>
          <Text size={compact ? 'xs' : 'sm'} fw={500}>
            {formatCost(modelCost.embeddingTokenCost / 1000)}/1K tokens
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

      {/* Inference steps */}
      {modelCost.costPerInferenceStep && (
        <Group gap="xs">
          <Text size={compact ? 'xs' : 'sm'} c="dimmed">Per Step:</Text>
          <Text size={compact ? 'xs' : 'sm'} fw={500}>
            {formatCost(modelCost.costPerInferenceStep, 6)}
          </Text>
          {modelCost.defaultInferenceSteps && (
            <Text size="xs" c="dimmed">
              (Default: {modelCost.defaultInferenceSteps} steps = {formatCost(
                modelCost.costPerInferenceStep * modelCost.defaultInferenceSteps
              )})
            </Text>
          )}
        </Group>
      )}

      {/* Image cost */}
      {modelCost.imageCostPerImage && (
        <Group gap="xs">
          <Text size={compact ? 'xs' : 'sm'} c="dimmed">Per Image:</Text>
          <Text size={compact ? 'xs' : 'sm'} fw={500}>
            {formatCost(modelCost.imageCostPerImage, 2)}
          </Text>
          {modelCost.imageQualityMultipliers && modelCost.imageQualityMultipliers !== '{}' && (
            <Badge size="xs" variant="light" color="orange">Quality tiers</Badge>
          )}
        </Group>
      )}

      {/* Audio costs */}
      {(modelCost.audioCostPerMinute ?? modelCost.audioCostPerKCharacters ?? 
        modelCost.audioInputCostPerMinute ?? modelCost.audioOutputCostPerMinute) && (
        <>
          {modelCost.audioCostPerMinute && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Audio:</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.audioCostPerMinute, 2)}/minute
              </Text>
            </Group>
          )}
          {modelCost.audioCostPerKCharacters && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Audio (TTS):</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.audioCostPerKCharacters, 2)}/1K chars
              </Text>
            </Group>
          )}
          {modelCost.audioInputCostPerMinute && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Transcription:</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.audioInputCostPerMinute, 2)}/minute
              </Text>
            </Group>
          )}
          {modelCost.audioOutputCostPerMinute && (
            <Group gap="xs">
              <Text size={compact ? 'xs' : 'sm'} c="dimmed">Speech:</Text>
              <Text size={compact ? 'xs' : 'sm'} fw={500}>
                {formatCost(modelCost.audioOutputCostPerMinute, 2)}/minute
              </Text>
            </Group>
          )}
        </>
      )}

      {/* Video cost */}
      {modelCost.videoCostPerSecond && (
        <Group gap="xs">
          <Text size={compact ? 'xs' : 'sm'} c="dimmed">Video:</Text>
          <Text size={compact ? 'xs' : 'sm'} fw={500}>
            {formatCost(modelCost.videoCostPerSecond, 2)}/second
          </Text>
          {modelCost.videoResolutionMultipliers && modelCost.videoResolutionMultipliers !== '{}' && (
            <Badge size="xs" variant="light" color="pink">Resolution tiers</Badge>
          )}
        </Group>
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