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
  currentInputText?: string;
  currentInputImages?: number;
}

interface TokenCounterStats extends TokenStats {
  estimatedCost?: number;
  isEstimated?: boolean;
  currentInput?: number;
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
  showCost = false,
  currentInputText,
  currentInputImages = 0
}: TokenCounterProps) {
  const [stats, setStats] = useState<TokenCounterStats>({
    prompt: 0,
    completion: 0,
    total: 0,
    currentInput: 0,
  });

  useEffect(() => {
    // First, try to use actual token counts from metadata
    let promptTokens = 0;
    let completionTokens = 0;
    let hasActualTokenCounts = false;

    // Calculate actual tokens from metadata
    messages.forEach(message => {
      if (message.metadata?.promptTokens) {
        promptTokens += message.metadata.promptTokens;
        hasActualTokenCounts = true;
      }
      if (message.metadata?.completionTokens) {
        completionTokens += message.metadata.completionTokens;
        hasActualTokenCounts = true;
      }
      // Also check tokensUsed for backward compatibility
      if (message.metadata?.tokensUsed && message.role === 'assistant') {
        // tokensUsed typically represents completion tokens for assistant messages
        if (!message.metadata.completionTokens) {
          completionTokens += message.metadata.tokensUsed;
          hasActualTokenCounts = true;
        }
      }
    });

    let tokenStats: TokenStats;

    if (hasActualTokenCounts) {
      // Use actual token counts when available
      tokenStats = {
        prompt: promptTokens,
        completion: completionTokens,
        total: promptTokens + completionTokens,
      };
    } else {
      // Fall back to estimation only when no actual data is available
      const estimatorMessages: EstimatorMessage[] = messages.map(convertToEstimatorMessage);
      const modelFamily = modelName ? TokenEstimator.getModelFamily(modelName) : ModelFamily.Generic;
      tokenStats = TokenEstimator.estimateConversationTokens(estimatorMessages, modelFamily);
    }

    // Calculate current input token estimation
    let currentInputTokens = 0;
    if (currentInputText && currentInputText.trim().length > 0) {
      const modelFamily = modelName ? TokenEstimator.getModelFamily(modelName) : ModelFamily.Generic;
      const inputMessage: EstimatorMessage = {
        role: 'user',
        content: currentInputText,
        images: currentInputImages > 0 ? Array(currentInputImages).fill({
          width: undefined,
          height: undefined,
          detail: 'auto' as const
        }) : undefined
      };
      const inputStats = TokenEstimator.estimateConversationTokens([inputMessage], modelFamily);
      currentInputTokens = inputStats.prompt;
    }

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
      isEstimated: !hasActualTokenCounts,
      currentInput: currentInputTokens,
    });
  }, [messages, modelName, showCost, currentInputText, currentInputImages]);

  // Calculate total including current input for analysis
  const totalWithInput = stats.total + (stats.currentInput ?? 0);
  const analysis = TokenEstimator.analyzeTokenUsage({ ...stats, total: totalWithInput }, maxTokens);
  const percentage = analysis.percentage;
  const isWarning = analysis.isWarning;
  const isNearLimit = analysis.isNearLimit;
  const isCritical = analysis.isCritical;

  if (compact) {
    return (
      <Tooltip
        label={
          <Stack gap={4}>
            {stats.isEstimated && (
              <Text size="xs" c="yellow" fw={500}>
                ⚠️ Estimated values (actual counts unavailable)
              </Text>
            )}
            <Text size="xs">Prompt: {TokenUtils.formatTokenCount(stats.prompt)} tokens</Text>
            <Text size="xs">Completion: {TokenUtils.formatTokenCount(stats.completion)} tokens</Text>
            {(stats.currentInput ?? 0) > 0 && (
              <Text size="xs" c="blue" fw={500}>Current input: ~{TokenUtils.formatTokenCount(stats.currentInput ?? 0)} tokens</Text>
            )}
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
          {stats.isEstimated && '~'}{TokenUtils.formatTokenCount(totalWithInput)} / {TokenUtils.formatTokenCount(maxTokens)} ({Math.round(percentage)}%)
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
            <Text size="sm" fw={500}>Context Window {stats.isEstimated && '(Estimated)'}</Text>
          </Group>
          <Group gap="xs">
            <Text size="sm" c={(() => {
              if (isCritical) return 'red';
              if (isNearLimit) return 'orange';
              if (isWarning) return 'yellow';
              return undefined;
            })()}>
              {totalWithInput.toLocaleString()} / {maxTokens.toLocaleString()}
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
              {stats.isEstimated && '~'}Prompt: {TokenUtils.formatTokenCount(stats.prompt)} tokens
            </Text>
            <Text size="xs" c="dimmed">
              {stats.isEstimated && '~'}Completion: {TokenUtils.formatTokenCount(stats.completion)} tokens
            </Text>
            {(stats.currentInput ?? 0) > 0 && (
              <Text size="xs" c="blue" fw={500}>
                Current input: ~{TokenUtils.formatTokenCount(stats.currentInput ?? 0)} tokens
              </Text>
            )}
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