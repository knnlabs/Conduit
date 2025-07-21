'use client';

import { useEffect, useState } from 'react';
import { Group, Text, Progress, Tooltip, Paper, Stack, Badge } from '@mantine/core';
import { IconCoin } from '@tabler/icons-react';
import type { ChatMessage } from '@/types/chat';

interface TokenCounterProps {
  messages: ChatMessage[];
  maxTokens?: number;
  model?: string;
  compact?: boolean;
}

interface TokenStats {
  prompt: number;
  completion: number;
  total: number;
  estimatedCost?: number;
}

// Rough token estimation (actual tokenization varies by model)
function estimateTokens(text: string): number {
  // Approximate: 1 token ≈ 4 characters for English text
  return Math.ceil(text.length / 4);
}

function getMessageText(message: ChatMessage): string {
  if (typeof message.content === 'string') {
    return message.content;
  }
  return message.content
    .map(c => c.type === 'text' ? c.text : '[image]')
    .join(' ');
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

export function TokenCounter({ messages, maxTokens = 4096, model, compact = false }: TokenCounterProps) {
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
      const tokens = estimateTokens(text);

      if (msg.role === 'assistant') {
        completionTokens += tokens;
      } else {
        promptTokens += tokens;
      }
    });

    const totalTokens = promptTokens + completionTokens;

    // Calculate estimated cost
    let estimatedCost;
    if (model && MODEL_PRICING[model]) {
      const pricing = MODEL_PRICING[model];
      estimatedCost = (promptTokens / 1000) * pricing.input + 
                     (completionTokens / 1000) * pricing.output;
    }

    setStats({
      prompt: promptTokens,
      completion: completionTokens,
      total: totalTokens,
      estimatedCost,
    });
  }, [messages, model]);

  const percentage = (stats.total / maxTokens) * 100;
  const isNearLimit = percentage > 80;
  const isOverLimit = percentage > 100;

  if (compact) {
    return (
      <Tooltip
        label={
          <Stack gap={4}>
            <Text size="xs">Prompt: {stats.prompt.toLocaleString()} tokens</Text>
            <Text size="xs">Completion: {stats.completion.toLocaleString()} tokens</Text>
            {stats.estimatedCost !== undefined && (
              <Text size="xs">Est. cost: ${stats.estimatedCost.toFixed(4)}</Text>
            )}
          </Stack>
        }
      >
        <Badge
          size="sm"
          variant={isOverLimit ? 'filled' : 'light'}
          color={(() => {
            if (isOverLimit) return 'red';
            if (isNearLimit) return 'yellow';
            return 'blue';
          })()}
          leftSection={<IconCoin size={14} />}
        >
          {stats.total.toLocaleString()} / {maxTokens.toLocaleString()}
        </Badge>
      </Tooltip>
    );
  }

  return (
    <Paper p="sm" withBorder>
      <Stack gap="xs">
        <Group justify="space-between">
          <Group gap="xs">
            <IconCoin size={18} />
            <Text size="sm" fw={500}>Token Usage</Text>
          </Group>
          <Text size="sm" c={(() => {
            if (isOverLimit) return 'red';
            if (isNearLimit) return 'yellow';
            return undefined;
          })()}>
            {stats.total.toLocaleString()} / {maxTokens.toLocaleString()}
          </Text>
        </Group>

        <Progress
          value={percentage}
          color={(() => {
            if (isOverLimit) return 'red';
            if (isNearLimit) return 'yellow';
            return 'blue';
          })()}
          size="sm"
          striped={isNearLimit}
          animated={isNearLimit}
        />

        <Group justify="space-between" gap="xs">
          <Stack gap={2}>
            <Text size="xs" c="dimmed">
              Prompt: {stats.prompt.toLocaleString()}
            </Text>
            <Text size="xs" c="dimmed">
              Completion: {stats.completion.toLocaleString()}
            </Text>
          </Stack>
          {stats.estimatedCost !== undefined && (
            <Text size="xs" c="dimmed">
              Est. cost: ${stats.estimatedCost.toFixed(4)}
            </Text>
          )}
        </Group>

        {isOverLimit && (
          <Text size="xs" c="red">
            Token limit exceeded. Some messages may be truncated.
          </Text>
        )}
      </Stack>
    </Paper>
  );
}