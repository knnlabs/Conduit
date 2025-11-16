'use client';

import { useState, useEffect } from 'react';
import { Group, ActionIcon, Text, Progress, Paper, Tooltip } from '@mantine/core';
import {
  IconPlayerPause,
  IconPlayerPlay,
  IconPlayerStop,
  IconClock,
  IconGauge,
} from '@tabler/icons-react';

interface StreamingControlsProps {
  isStreaming: boolean;
  isPaused: boolean;
  tokenCount: number;
  tps: number;
  elapsedTime: number;
  onPause: () => void;
  onResume: () => void;
  onCancel: () => void;
}

export function StreamingControls({
  isStreaming,
  isPaused,
  tokenCount,
  tps,
  elapsedTime,
  onPause,
  onResume,
  onCancel,
}: StreamingControlsProps) {
  const [estimatedCompletion, setEstimatedCompletion] = useState<number | null>(null);

  useEffect(() => {
    // Estimate completion time based on current TPS
    if (tps > 0 && isStreaming) {
      // Assume average response is ~500 tokens (rough estimate)
      const remainingTokens = Math.max(500 - tokenCount, 0);
      const estimatedSeconds = remainingTokens / tps;
      setEstimatedCompletion(estimatedSeconds);
    } else {
      setEstimatedCompletion(null);
    }
  }, [tps, tokenCount, isStreaming]);

  const formatTime = (seconds: number): string => {
    if (seconds < 60) return `${Math.round(seconds)}s`;
    const minutes = Math.floor(seconds / 60);
    const secs = Math.round(seconds % 60);
    return `${minutes}:${secs.toString().padStart(2, '0')}`;
  };

  if (!isStreaming) return null;

  return (
    <Paper p="xs" withBorder>
      <Group justify="space-between" wrap="nowrap">
        <Group gap="xs" wrap="nowrap">
          {isPaused ? (
            <Tooltip label="Resume generation">
              <ActionIcon
                variant="filled"
                color="green"
                size="sm"
                onClick={onResume}
              >
                <IconPlayerPlay size={16} />
              </ActionIcon>
            </Tooltip>
          ) : (
            <Tooltip label="Pause generation">
              <ActionIcon
                variant="filled"
                color="yellow"
                size="sm"
                onClick={onPause}
              >
                <IconPlayerPause size={16} />
              </ActionIcon>
            </Tooltip>
          )}

          <Tooltip label="Cancel generation">
            <ActionIcon
              variant="filled"
              color="red"
              size="sm"
              onClick={onCancel}
            >
              <IconPlayerStop size={16} />
            </ActionIcon>
          </Tooltip>
        </Group>

        <Group gap="md" wrap="nowrap" style={{ flex: 1 }}>
          <Group gap={4}>
            <IconGauge size={16} style={{ color: 'var(--mantine-color-dimmed)' }} />
            <Text size="sm" c="dimmed">
              {tps.toFixed(1)} tokens/s
            </Text>
          </Group>

          <Group gap={4}>
            <Text size="sm" c="dimmed">
              {tokenCount} tokens
            </Text>
          </Group>

          <Group gap={4}>
            <IconClock size={16} style={{ color: 'var(--mantine-color-dimmed)' }} />
            <Text size="sm" c="dimmed">
              {formatTime(elapsedTime)}
            </Text>
          </Group>

          {estimatedCompletion !== null && (
            <Text size="sm" c="dimmed">
              ~{formatTime(estimatedCompletion)} remaining
            </Text>
          )}
        </Group>

        {isPaused && (
          <Text size="xs" c="yellow" fw={500}>
            PAUSED
          </Text>
        )}
      </Group>

      {/* Progress indicator */}
      {isPaused ? (
        <Progress
          value={50}
          color="yellow"
          size="xs"
          mt={4}
        />
      ) : (
        <Progress
          value={100}
          color="blue"
          size="xs"
          mt={4}
          striped
          animated
        />
      )}
    </Paper>
  );
}