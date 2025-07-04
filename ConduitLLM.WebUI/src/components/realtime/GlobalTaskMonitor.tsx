'use client';

import {
  Card,
  Stack,
  Group,
  Text,
  Badge,
  Button,
  Modal,
  // Removed unused ActionIcon, Tooltip imports
  Alert,
  Grid,
  Progress,
  Center,
} from '@mantine/core';
import {
  IconActivity,
  IconEye,
  IconAlertCircle,
  IconCheck,
  IconX,
} from '@tabler/icons-react';
// Removed unused useState import
import { useDisclosure } from '@mantine/hooks';
import { useTaskProgressHub } from '@/hooks/signalr/useTaskProgressHub';
import TaskProgressPanel from './TaskProgressPanel';

interface GlobalTaskMonitorProps {
  virtualKey?: string; // Made optional since we no longer use it for SignalR auth
}

export function GlobalTaskMonitor({ virtualKey }: GlobalTaskMonitorProps) {
  const [detailsOpened, { open: openDetails, close: closeDetails }] = useDisclosure(false);
  
  const { tasks, isConnected } = useTaskProgressHub();

  const activeTasks = tasks.filter(task => 
    task.status === 'pending' || task.status === 'processing'
  );

  const recentTasks = tasks
    .filter(task => task.status === 'completed' || task.status === 'failed')
    .slice(0, 3);

  const getTaskTypeStats = () => {
    const stats = {
      image: 0,
      video: 0,
      audio_transcription: 0,
      audio_speech: 0,
    };

    activeTasks.forEach(task => {
      if (task.type in stats) {
        (stats as Record<string, number>)[task.type]++;
      }
    });

    return stats;
  };

  const taskStats = getTaskTypeStats();
  const totalActiveTasks = activeTasks.length;

  // Always show the task monitor since authentication is now session-based

  return (
    <>
      <Card withBorder>
        <Stack gap="md">
          <Group justify="space-between">
            <Group gap="xs">
              <IconActivity size={20} />
              <Text fw={600}>Task Monitor</Text>
              <Badge 
                size="sm" 
                color={isConnected ? 'green' : 'red'}
                variant="light"
              >
                {isConnected ? 'Live' : 'Offline'}
              </Badge>
            </Group>
            
            {totalActiveTasks > 0 && (
              <Button 
                size="xs" 
                variant="light" 
                leftSection={<IconEye size={14} />}
                onClick={openDetails}
              >
                View All
              </Button>
            )}
          </Group>

          {totalActiveTasks === 0 ? (
            <Text size="sm" c="dimmed" ta="center" py="md">
              No active tasks
            </Text>
          ) : (
            <>
              <Grid>
                {taskStats.image > 0 && (
                  <Grid.Col span={6}>
                    <Center>
                      <Stack gap="xs" align="center">
                        <Badge size="lg" variant="light" color="blue">
                          {taskStats.image}
                        </Badge>
                        <Text size="xs" c="dimmed">Images</Text>
                      </Stack>
                    </Center>
                  </Grid.Col>
                )}
                
                {taskStats.video > 0 && (
                  <Grid.Col span={6}>
                    <Center>
                      <Stack gap="xs" align="center">
                        <Badge size="lg" variant="light" color="grape">
                          {taskStats.video}
                        </Badge>
                        <Text size="xs" c="dimmed">Videos</Text>
                      </Stack>
                    </Center>
                  </Grid.Col>
                )}
                
                {taskStats.audio_transcription > 0 && (
                  <Grid.Col span={6}>
                    <Center>
                      <Stack gap="xs" align="center">
                        <Badge size="lg" variant="light" color="orange">
                          {taskStats.audio_transcription}
                        </Badge>
                        <Text size="xs" c="dimmed">Transcriptions</Text>
                      </Stack>
                    </Center>
                  </Grid.Col>
                )}
                
                {taskStats.audio_speech > 0 && (
                  <Grid.Col span={6}>
                    <Center>
                      <Stack gap="xs" align="center">
                        <Badge size="lg" variant="light" color="teal">
                          {taskStats.audio_speech}
                        </Badge>
                        <Text size="xs" c="dimmed">Speech</Text>
                      </Stack>
                    </Center>
                  </Grid.Col>
                )}
              </Grid>
              
              {/* Show progress for the most recent active task */}
              {activeTasks.length > 0 && (
                <div>
                  <Text size="xs" c="dimmed" mb="xs">
                    Most recent: {activeTasks[0].type.replace('_', ' ')} generation
                  </Text>
                  <Progress 
                    value={activeTasks[0].progress} 
                    size="sm" 
                    animated={activeTasks[0].status === 'processing'}
                  />
                </div>
              )}
            </>
          )}

          {/* Recent completed tasks */}
          {recentTasks.length > 0 && (
            <>
              <Text size="xs" fw={500} c="dimmed">Recent completions</Text>
              <Stack gap="xs">
                {recentTasks.map((task) => (
                  <Group key={task.taskId} justify="space-between">
                    <Group gap="xs">
                      {task.status === 'completed' ? (
                        <IconCheck size={14} color="var(--mantine-color-green-6)" />
                      ) : (
                        <IconX size={14} color="var(--mantine-color-red-6)" />
                      )}
                      <Text size="xs">
                        {task.type.replace('_', ' ')} generation
                      </Text>
                    </Group>
                    <Text size="xs" c="dimmed">
                      {task.completedAt?.toLocaleTimeString()}
                    </Text>
                  </Group>
                ))}
              </Stack>
            </>
          )}

          {!isConnected && (
            <Alert icon={<IconAlertCircle size={16} />} color="orange">
              <Text size="sm">
                Real-time monitoring is currently unavailable. Some tasks may complete in the background.
              </Text>
            </Alert>
          )}
        </Stack>
      </Card>

      {/* Task Details Modal */}
      <Modal
        opened={detailsOpened}
        onClose={closeDetails}
        title="All Active Tasks"
        size="lg"
      >
        <TaskProgressPanel 
          virtualKey={virtualKey}
          showAll={true}
          maxHeight={500}
        />
      </Modal>
    </>
  );
}

export default GlobalTaskMonitor;