'use client';

import { useEffect, useState } from 'react';
import { Group, Text, Progress, Tooltip, Paper, Stack, Badge } from '@mantine/core';
import { IconCoin, IconAlertTriangle } from '@tabler/icons-react';
import { 
  TokenEstimator, 
  TokenUtils, 
  ModelFamily,
  type TokenStats,
  type EstimatorMessage 
} from '@knn_labs/conduit-core-client';
import type { ChatMessage } from '../types';

interface TokenCounterProps {
  messages: ChatMessage[];
  maxTokens?: number;
  modelName?: string;
  compact?: boolean;
  showCost?: boolean;
}

interface TokenCounterStats extends TokenStats {
  estimatedCost?: number;
}

function convertToEstimatorMessage(message: ChatMessage): EstimatorMessage {
  return {
    role: message.role as 'user' | 'assistant' | 'system',
    content: message.content ?? '',
    images: message.images?.map(() => ({
      // Width and height are optional and may not exist on ImageAttachment
      width: undefined,
      height: undefined,
      detail: 'auto' as const
    }))
  };
}

export function TokenCounter({ 
  messages, 
  maxTokens = 128000, 
  modelName, 
  compact = false,
  showCost = false 
}: TokenCounterProps) {
  const [stats, setStats] = useState<TokenCounterStats>({
    prompt: 0,
    completion: 0,
    total: 0,
  });

  useEffect(() => {
    // Convert ChatMessage to EstimatorMessage format
    const estimatorMessages: EstimatorMessage[] = messages.map(convertToEstimatorMessage);
    
    // Use TokenEstimator for accurate token calculation
    const modelFamily = modelName ? TokenEstimator.getModelFamily(modelName) : ModelFamily.Generic;
    const tokenStats = TokenEstimator.estimateConversationTokens(estimatorMessages, modelFamily);

    // Calculate estimated cost
    let estimatedCost;
    if (showCost && modelName) {
      const pricing = TokenEstimator.getModelPricing(modelName);
      if (pricing) {
        const cost = TokenEstimator.estimateCost(tokenStats, pricing, modelName);
        estimatedCost = cost.totalCost;
      }
    }

    setStats({
      ...tokenStats,
      estimatedCost,
    });
  }, [messages, modelName, showCost]);

  const analysis = TokenEstimator.analyzeTokenUsage(stats, maxTokens);
  const percentage = analysis.percentage;
  const isWarning = analysis.isWarning;
  const isNearLimit = analysis.isNearLimit;
  const isCritical = analysis.isCritical;

  if (compact) {
    return (
      <Tooltip
        label={
          <Stack gap={4}>
            <Text size="xs">Prompt: {TokenUtils.formatTokenCount(stats.prompt)} tokens</Text>
            <Text size="xs">Completion: {TokenUtils.formatTokenCount(stats.completion)} tokens</Text>
            <Text size="xs">Remaining: {TokenUtils.formatTokenCount(analysis.remaining)} tokens</Text>
            {stats.estimatedCost !== undefined && (
              <Text size="xs">Est. cost: {TokenUtils.formatCost(stats.estimatedCost)}</Text>
            )}
            {isCritical && (
              <Text size="xs" c="red" fw={500}>
                ⚠️ Context limit reached - messages may be truncated
              </Text>
            )}
          </Stack>
        }
      >
        <Badge
          size="sm"
          variant={isCritical ? 'filled' : 'light'}
          color={(() => {
            if (isCritical) return 'red';
            if (isNearLimit) return 'orange';
            if (isWarning) return 'yellow';
            return 'blue';
          })()}
          leftSection={isCritical ? <IconAlertTriangle size={14} /> : <IconCoin size={14} />}
        >
          {TokenUtils.formatTokenCount(stats.total)} / {TokenUtils.formatTokenCount(maxTokens)} ({Math.round(percentage)}%)
        </Badge>
      </Tooltip>
    );
  }

  return (
    <Paper p="sm" withBorder>
      <Stack gap="xs">
        <Group justify="space-between">
          <Group gap="xs">
            {isCritical ? <IconAlertTriangle size={18} color="var(--mantine-color-red-6)" /> : <IconCoin size={18} />}
            <Text size="sm" fw={500}>Context Window</Text>
          </Group>
          <Group gap="xs">
            <Text size="sm" c={(() => {
              if (isCritical) return 'red';
              if (isNearLimit) return 'orange';
              if (isWarning) return 'yellow';
              return undefined;
            })()}>
              {stats.total.toLocaleString()} / {maxTokens.toLocaleString()}
            </Text>
            <Badge size="sm" variant="light" color={TokenUtils.getUsageColor(percentage)}>
              {Math.round(percentage)}%
            </Badge>
          </Group>
        </Group>

        <Progress
          value={percentage}
          color={TokenUtils.getUsageColor(percentage) === 'green' ? 'blue' : TokenUtils.getUsageColor(percentage)}
          size="sm"
          striped={isNearLimit || isCritical}
          animated={isNearLimit || isCritical}
        />

        <Group justify="space-between" gap="xs">
          <Stack gap={2}>
            <Text size="xs" c="dimmed">
              Prompt: {TokenUtils.formatTokenCount(stats.prompt)} tokens
            </Text>
            <Text size="xs" c="dimmed">
              Completion: {TokenUtils.formatTokenCount(stats.completion)} tokens
            </Text>
            <Text size="xs" c="dimmed" fw={500}>
              Remaining: {TokenUtils.formatTokenCount(analysis.remaining)} tokens
            </Text>
          </Stack>
          {stats.estimatedCost !== undefined && (
            <Text size="xs" c="dimmed">
              Est. cost: {TokenUtils.formatCost(stats.estimatedCost)}
            </Text>
          )}
        </Group>

        {isCritical && (
          <Text size="xs" c="red" fw={500}>
            ⚠️ Context limit reached. Older messages will be automatically trimmed to stay within limits.
          </Text>
        )}
        {isNearLimit && !isCritical && (
          <Text size="xs" c="orange">
            Approaching context limit. Consider starting a new conversation soon.
          </Text>
        )}
      </Stack>
    </Paper>
  );
}