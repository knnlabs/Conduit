'use client';

import { useEffect, useState } from 'react';
import { Group, Text, Progress, Tooltip, Paper, Stack, Badge } from '@mantine/core';
import { IconCoin, IconAlertTriangle } from '@tabler/icons-react';
import type { ChatMessage } from '../types';

interface TokenCounterProps {
  messages: ChatMessage[];
  maxTokens?: number;
  modelName?: string;
  compact?: boolean;
  showCost?: boolean;
}

interface TokenStats {
  prompt: number;
  completion: number;
  total: number;
  estimatedCost?: number;
}

// Improved token estimation based on common patterns
function estimateTokens(text: string): number {
  // More accurate estimation:
  // - Average English word is ~4-5 characters
  // - Average token is ~0.75 words
  // - So roughly 1 token ≈ 3-4 characters
  // - Add overhead for punctuation and special tokens
  
  // Base estimation: characters / 3.5
  let tokens = text.length / 3.5;
  
  // Add overhead for special characters and formatting
  const specialChars = (text.match(/[^\w\s]/g) ?? []).length;
  tokens += specialChars * 0.1;
  
  // Add overhead for newlines (often become special tokens)
  const newlines = (text.match(/\n/g) ?? []).length;
  tokens += newlines * 0.5;
  
  return Math.ceil(tokens);
}

function getMessageText(message: ChatMessage): string {
  // ChatMessage content is always a string
  // Images are handled separately in the images field
  return message.content ?? '';
}

// Model pricing per 1K tokens (example values)
const MODEL_PRICING: Record<string, { input: number; output: number }> = {
  ['gpt-4']: { input: 0.03, output: 0.06 },
  ['gpt-4-turbo']: { input: 0.01, output: 0.03 },
  ['gpt-3.5-turbo']: { input: 0.0005, output: 0.0015 },
  ['claude-3-opus']: { input: 0.015, output: 0.075 },
  ['claude-3-sonnet']: { input: 0.003, output: 0.015 },
  ['claude-3-haiku']: { input: 0.00025, output: 0.00125 },
};

export function TokenCounter({ 
  messages, 
  maxTokens = 128000, 
  modelName, 
  compact = false,
  showCost = false 
}: TokenCounterProps) {
  const [stats, setStats] = useState<TokenStats>({
    prompt: 0,
    completion: 0,
    total: 0,
  });

  useEffect(() => {
    // Calculate token counts
    let promptTokens = 0;
    let completionTokens = 0;

    messages.forEach((msg) => {
      const text = getMessageText(msg);
      let tokens = estimateTokens(text);
      
      // Add extra tokens for images if present
      if (msg.images && msg.images.length > 0) {
        // Each image typically uses ~85 tokens for low detail, ~765 for high detail
        // Using a conservative estimate
        tokens += msg.images.length * 100;
      }

      if (msg.role === 'assistant') {
        completionTokens += tokens;
      } else {
        promptTokens += tokens;
      }
    });

    const totalTokens = promptTokens + completionTokens;

    // Calculate estimated cost
    let estimatedCost;
    if (showCost && modelName && MODEL_PRICING[modelName]) {
      const pricing = MODEL_PRICING[modelName];
      estimatedCost = (promptTokens / 1000) * pricing.input + 
                     (completionTokens / 1000) * pricing.output;
    }

    setStats({
      prompt: promptTokens,
      completion: completionTokens,
      total: totalTokens,
      estimatedCost,
    });
  }, [messages, modelName, showCost]);

  const percentage = Math.min((stats.total / maxTokens) * 100, 100);
  const isWarning = percentage > 70 && percentage <= 85;
  const isNearLimit = percentage > 85 && percentage <= 95;
  const isCritical = percentage > 95;

  if (compact) {
    return (
      <Tooltip
        label={
          <Stack gap={4}>
            <Text size="xs">Prompt: {stats.prompt.toLocaleString()} tokens</Text>
            <Text size="xs">Completion: {stats.completion.toLocaleString()} tokens</Text>
            <Text size="xs">Remaining: {Math.max(0, maxTokens - stats.total).toLocaleString()} tokens</Text>
            {stats.estimatedCost !== undefined && (
              <Text size="xs">Est. cost: ${stats.estimatedCost.toFixed(4)}</Text>
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
          {stats.total.toLocaleString()} / {maxTokens.toLocaleString()} ({Math.round(percentage)}%)
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
            <Badge size="sm" variant="light" color={(() => {
              if (isCritical) return 'red';
              if (isNearLimit) return 'orange';
              if (isWarning) return 'yellow';
              return 'green';
            })()}>
              {Math.round(percentage)}%
            </Badge>
          </Group>
        </Group>

        <Progress
          value={percentage}
          color={(() => {
            if (isCritical) return 'red';
            if (isNearLimit) return 'orange';
            if (isWarning) return 'yellow';
            return 'blue';
          })()}
          size="sm"
          striped={isNearLimit || isCritical}
          animated={isNearLimit || isCritical}
        />

        <Group justify="space-between" gap="xs">
          <Stack gap={2}>
            <Text size="xs" c="dimmed">
              Prompt: {stats.prompt.toLocaleString()} tokens
            </Text>
            <Text size="xs" c="dimmed">
              Completion: {stats.completion.toLocaleString()} tokens
            </Text>
            <Text size="xs" c="dimmed" fw={500}>
              Remaining: {Math.max(0, maxTokens - stats.total).toLocaleString()} tokens
            </Text>
          </Stack>
          {stats.estimatedCost !== undefined && (
            <Text size="xs" c="dimmed">
              Est. cost: ${stats.estimatedCost.toFixed(4)}
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