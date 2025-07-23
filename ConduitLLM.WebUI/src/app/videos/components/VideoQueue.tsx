'use client';

import { useState } from 'react';
import { Paper, Title, Text, Group, Button, Progress, Alert, Collapse, Stack, Badge } from '@mantine/core';
import { IconRefresh, IconX, IconClock } from '@tabler/icons-react';
import { useVideoStore } from '../hooks/useVideoStore';
import { useVideoGeneration } from '../hooks/useVideoGeneration';
import { canRetry, type VideoTask } from '../types';

// Retry button component
function RetryButton({ task, onRetry }: { task: VideoTask; onRetry: (task: VideoTask) => Promise<void> }) {
  const [isRetrying, setIsRetrying] = useState(false);
  
  if (!canRetry(task)) return null;
  
  const handleRetry = () => {
    setIsRetrying(true);
    void onRetry(task).finally(() => {
      setIsRetrying(false);
    });
  };
  
  return (
    <Button
      size="xs"
      variant="light"
      leftSection={<IconRefresh size={14} />}
      onClick={handleRetry}
      loading={isRetrying}
      title="Retry generation"
    >
      {isRetrying ? 'Retrying...' : `Retry (${3 - task.retryCount} left)`}
    </Button>
  );
}

export default function VideoQueue() {
  const { currentTask } = useVideoStore();
  const { cancelGeneration, retryGeneration } = useVideoGeneration();

  if (!currentTask) {
    return null;
  }

  const isActive = currentTask.status === 'pending' || currentTask.status === 'running';

  const [retryHistoryOpened, setRetryHistoryOpened] = useState(false);

  return (
    <Paper p="md" withBorder>
      <Group align="center" mb="sm">
        <IconClock size={20} />
        <Title order={4}>Video Generation Queue</Title>
      </Group>
      
      <Stack gap="sm">
        <Group gap="xs" align="flex-start">
          <Text size="xl">{getStatusIcon(currentTask.status)}</Text>
          <div style={{ flex: 1 }}>
            <Text size="sm" fw={500} lineClamp={2}>
              {currentTask.prompt}
            </Text>
            
            <Group gap="xs" mt="xs">
              <Badge 
                size="sm" 
                variant="light"
                color={getStatusColor(currentTask.status)}
              >
                {getStatusText(currentTask.status)}
              </Badge>
              {currentTask.message && (
                <Text size="xs" c="dimmed">{currentTask.message}</Text>
              )}
              {currentTask.estimatedTimeToCompletion && (
                <Text size="xs" c="dimmed">
                  ETA: {formatTime(currentTask.estimatedTimeToCompletion)}
                </Text>
              )}
              {currentTask.retryCount > 0 && (
                <Badge size="xs" variant="dot">
                  Attempt {currentTask.retryCount + 1}/4
                </Badge>
              )}
            </Group>
            
            {currentTask.progress > 0 && (
              <Progress 
                value={currentTask.progress} 
                size="sm" 
                mt="xs"
                animated={isActive}
              />
            )}
          </div>
          
          <Group gap="xs">
            {isActive && (
              <Button
                size="xs"
                variant="light"
                color="red"
                leftSection={<IconX size={14} />}
                onClick={() => void cancelGeneration(currentTask.id)}
                title="Cancel generation"
              >
                Cancel
              </Button>
            )}
            
            {(currentTask.status === 'failed' || currentTask.status === 'timedout') && (
              <RetryButton task={currentTask} onRetry={retryGeneration} />
            )}
          </Group>
        </Group>
        
        {(currentTask.status === 'failed' || currentTask.status === 'timedout' || currentTask.status === 'cancelled') && currentTask.error && (
          <Alert 
            color="red" 
            variant="light"
            title={currentTask.status === 'timedout' ? 'Generation timed out' : 'Generation failed'}
          >
            {currentTask.error}
          </Alert>
        )}
        
        {currentTask.retryHistory.length > 0 && (
          <div>
            <Button
              size="xs"
              variant="subtle"
              onClick={() => setRetryHistoryOpened(!retryHistoryOpened)}
            >
              Retry History ({currentTask.retryHistory.length})
            </Button>
            <Collapse in={retryHistoryOpened}>
              <Stack gap="xs" mt="xs">
                {currentTask.retryHistory.map((retry) => (
                  <Text key={`${retry.attemptNumber}-${retry.timestamp}`} size="xs" c="dimmed">
                    Attempt {retry.attemptNumber}: {retry.error}
                    <Text span c="dimmed" ml="xs">
                      ({new Date(retry.timestamp).toLocaleTimeString()})
                    </Text>
                  </Text>
                ))}
              </Stack>
            </Collapse>
          </div>
        )}
      </Stack>
    </Paper>
  );
}

function getStatusIcon(status: string): string {
  switch (status) {
    case 'pending':
      return '⏱️';
    case 'running':
      return '⏳';
    case 'completed':
      return '✅';
    case 'failed':
      return '❌';
    case 'cancelled':
      return '🚫';
    case 'timedout':
      return '⏰';
    default:
      return '❓';
  }
}

function getStatusText(status: string): string {
  switch (status) {
    case 'pending':
      return 'Queued';
    case 'running':
      return 'Generating';
    case 'completed':
      return 'Completed';
    case 'failed':
      return 'Failed';
    case 'cancelled':
      return 'Cancelled';
    case 'timedout':
      return 'Timed Out';
    default:
      return status;
  }
}

function formatTime(seconds: number): string {
  if (seconds < 60) {
    return `${Math.round(seconds)}s`;
  } else {
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = Math.round(seconds % 60);
    return `${minutes}m ${remainingSeconds}s`;
  }
}

function getStatusColor(status: string): string {
  switch (status) {
    case 'pending':
      return 'gray';
    case 'running':
      return 'blue';
    case 'completed':
      return 'green';
    case 'failed':
      return 'red';
    case 'cancelled':
      return 'orange';
    case 'timedout':
      return 'yellow';
    default:
      return 'gray';
  }
}