'use client';

import {
  Card,
  Stack,
  Group,
  Text,
  Badge,
  Progress,
  ActionIcon,
  Tooltip,
  Alert,
  ScrollArea,
  Button,
  Divider,
} from '@mantine/core';
import {
  IconClock,
  IconCheck,
  IconX,
  IconRefresh,
  IconTrash,
  IconPhoto,
  IconVideo,
  IconMicrophone,
  IconVolume,
  IconAlertCircle,
} from '@tabler/icons-react';
import { TaskProgress, useTaskProgressHub } from '@/hooks/signalr/useTaskProgressHub';
import { useEffect, useState } from 'react';

interface TaskProgressPanelProps {
  virtualKey?: string; // Made optional since we no longer use it for SignalR auth
  taskType?: TaskProgress['type'];
  onTaskCompleted?: (task: TaskProgress) => void;
  showAll?: boolean;
  maxHeight?: number;
}

export function TaskProgressPanel({
  virtualKey,
  taskType,
  onTaskCompleted,
  showAll = false,
  maxHeight = 300,
}: TaskProgressPanelProps) {
  const [expandedTasks, setExpandedTasks] = useState<Set<string>>(new Set());

  const { tasks, isConnected, getTasksByType, clearTask, clearCompletedTasks } = useTaskProgressHub(
    {
      onTaskCompleted: (task) => {
        onTaskCompleted?.(task);
      },
    }
  );

  const filteredTasks = showAll 
    ? tasks 
    : taskType 
      ? getTasksByType(taskType)
      : tasks;

  const activeTasks = filteredTasks.filter(task => 
    task.status === 'pending' || task.status === 'processing'
  );

  const completedTasks = filteredTasks.filter(task => 
    task.status === 'completed' || task.status === 'failed'
  );

  const getTaskIcon = (type: TaskProgress['type']) => {
    switch (type) {
      case 'image': return <IconPhoto size={16} />;
      case 'video': return <IconVideo size={16} />;
      case 'audio_transcription': return <IconMicrophone size={16} />;
      case 'audio_speech': return <IconVolume size={16} />;
      default: return <IconClock size={16} />;
    }
  };

  const getStatusColor = (status: TaskProgress['status']) => {
    switch (status) {
      case 'pending': return 'gray';
      case 'processing': return 'blue';
      case 'completed': return 'green';
      case 'failed': return 'red';
      default: return 'gray';
    }
  };

  const getStatusText = (status: TaskProgress['status']) => {
    switch (status) {
      case 'pending': return 'Queued';
      case 'processing': return 'Processing';
      case 'completed': return 'Completed';
      case 'failed': return 'Failed';
      default: return 'Unknown';
    }
  };

  const formatDuration = (startedAt: Date, completedAt?: Date) => {
    const end = completedAt || new Date();
    const duration = Math.round((end.getTime() - startedAt.getTime()) / 1000);
    
    if (duration < 60) {
      return `${duration}s`;
    } else if (duration < 3600) {
      return `${Math.floor(duration / 60)}m ${duration % 60}s`;
    } else {
      return `${Math.floor(duration / 3600)}h ${Math.floor((duration % 3600) / 60)}m`;
    }
  };

  const toggleTaskExpansion = (taskId: string) => {
    setExpandedTasks(prev => {
      const newSet = new Set(prev);
      if (newSet.has(taskId)) {
        newSet.delete(taskId);
      } else {
        newSet.add(taskId);
      }
      return newSet;
    });
  };

  if (filteredTasks.length === 0) {
    return null;
  }

  return (
    <Card withBorder>
      <Stack gap="md">
        <Group justify="space-between">
          <Group gap="xs">
            <Text fw={600}>
              {showAll ? 'All Tasks' : taskType ? `${taskType} Tasks` : 'Task Progress'}
            </Text>
            <Badge 
              size="xs" 
              color={isConnected ? 'green' : 'red'}
              variant="light"
            >
              {isConnected ? 'Connected' : 'Disconnected'}
            </Badge>
          </Group>
          
          <Group gap="xs">
            {completedTasks.length > 0 && (
              <Tooltip label="Clear completed tasks">
                <ActionIcon 
                  size="sm" 
                  variant="light" 
                  onClick={clearCompletedTasks}
                >
                  <IconTrash size={14} />
                </ActionIcon>
              </Tooltip>
            )}
          </Group>
        </Group>

        {!isConnected && (
          <Alert icon={<IconAlertCircle size={16} />} color="orange">
            <Text size="sm">
              Real-time updates are currently unavailable. Tasks may still complete in the background.
            </Text>
          </Alert>
        )}

        <ScrollArea.Autosize mah={maxHeight}>
          <Stack gap="md">
            {/* Active Tasks */}
            {activeTasks.length > 0 && (
              <>
                <Text size="sm" fw={500} c="dimmed">
                  Active ({activeTasks.length})
                </Text>
                {activeTasks.map((task) => (
                  <Card key={task.taskId} withBorder p="sm">
                    <Stack gap="xs">
                      <Group justify="space-between">
                        <Group gap="xs">
                          {getTaskIcon(task.type)}
                          <Text size="sm" fw={500}>
                            {task.type.replace('_', ' ')} generation
                          </Text>
                          <Badge 
                            size="xs" 
                            color={getStatusColor(task.status)}
                            variant="light"
                          >
                            {getStatusText(task.status)}
                          </Badge>
                        </Group>
                        
                        <Group gap="xs">
                          <Text size="xs" c="dimmed">
                            {formatDuration(task.startedAt)}
                          </Text>
                          <Tooltip label="Remove task">
                            <ActionIcon 
                              size="xs" 
                              variant="subtle" 
                              color="red"
                              onClick={() => clearTask(task.taskId)}
                            >
                              <IconX size={12} />
                            </ActionIcon>
                          </Tooltip>
                        </Group>
                      </Group>

                      {task.status === 'processing' && (
                        <div>
                          <Group justify="space-between" mb="xs">
                            <Text size="xs" c="dimmed">
                              {task.message || 'Processing...'}
                            </Text>
                            <Text size="xs" c="dimmed">
                              {Math.round(task.progress)}%
                            </Text>
                          </Group>
                          <Progress value={task.progress} size="sm" animated />
                        </div>
                      )}

                      {task.status === 'pending' && (
                        <Text size="xs" c="dimmed">
                          Waiting to start...
                        </Text>
                      )}
                    </Stack>
                  </Card>
                ))}
              </>
            )}

            {/* Completed Tasks */}
            {completedTasks.length > 0 && (
              <>
                {activeTasks.length > 0 && <Divider />}
                <Text size="sm" fw={500} c="dimmed">
                  Recent ({completedTasks.slice(0, 5).length})
                </Text>
                {completedTasks.slice(0, 5).map((task) => (
                  <Card key={task.taskId} withBorder p="sm" opacity={0.7}>
                    <Stack gap="xs">
                      <Group justify="space-between">
                        <Group gap="xs">
                          {getTaskIcon(task.type)}
                          <Text size="sm">
                            {task.type.replace('_', ' ')} generation
                          </Text>
                          <Badge 
                            size="xs" 
                            color={getStatusColor(task.status)}
                            variant="light"
                          >
                            {getStatusText(task.status)}
                          </Badge>
                        </Group>
                        
                        <Group gap="xs">
                          <Text size="xs" c="dimmed">
                            {formatDuration(task.startedAt, task.completedAt)}
                          </Text>
                          <Tooltip label="Remove task">
                            <ActionIcon 
                              size="xs" 
                              variant="subtle" 
                              color="red"
                              onClick={() => clearTask(task.taskId)}
                            >
                              <IconX size={12} />
                            </ActionIcon>
                          </Tooltip>
                        </Group>
                      </Group>

                      {task.status === 'failed' && task.error && (
                        <Text size="xs" c="red">
                          {task.error}
                        </Text>
                      )}

                      {task.status === 'completed' && (
                        <Group gap="xs">
                          <IconCheck size={12} color="var(--mantine-color-green-6)" />
                          <Text size="xs" c="green">
                            Completed successfully
                          </Text>
                        </Group>
                      )}
                    </Stack>
                  </Card>
                ))}
              </>
            )}
          </Stack>
        </ScrollArea.Autosize>
      </Stack>
    </Card>
  );
}

export default TaskProgressPanel;