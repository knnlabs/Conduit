'use client';

import { useMemo } from 'react';
import { 
  Card, 
  SimpleGrid, 
  Text, 
  Button, 
  Group, 
  Stack, 
  Center, 
  Badge, 
  Progress,
  Box,
  Title,
  Paper
} from '@mantine/core';
import { 
  IconDownload, 
  IconTrash,
  IconX,
  IconAlertCircle
} from '@tabler/icons-react';
import { useVideoStore } from '../hooks/useVideoStore';
import type { VideoTask, VideoData } from '../types';

export default function VideoGallery() {
  const { taskHistory, removeTask, clearHistory } = useVideoStore();

  const completedVideos = useMemo(() => {
    // First filter for completed videos with results
    const completed = taskHistory.filter(task => task.status === 'completed' && task.result);
    
    // Then deduplicate by task ID (keep the most recent one)
    const seen = new Set<string>();
    const deduplicated = completed.reverse().filter(task => {
      if (seen.has(task.id)) {
        return false;
      }
      seen.add(task.id);
      return true;
    }).reverse();
    
    return deduplicated;
  }, [taskHistory]);

  if (completedVideos.length === 0) {
    return (
      <Paper p="xl" withBorder>
        <Center>
          <Stack align="center" gap="md">
            <Title order={3} c="dimmed">No videos generated yet</Title>
            <Text c="dimmed">Your generated videos will appear here</Text>
          </Stack>
        </Center>
      </Paper>
    );
  }

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <Title order={3}>Generated Videos ({completedVideos.length})</Title>
        <Button
          onClick={clearHistory}
          variant="subtle"
          size="sm"
          leftSection={<IconTrash size={16} />}
        >
          Clear History
        </Button>
      </Group>
      
      <SimpleGrid cols={{ base: 1, md: 2, lg: 3 }} spacing="lg">
        {completedVideos.map((task) => (
          <VideoCard 
            key={task.id} 
            task={task} 
            onRemove={() => removeTask(task.id)}
          />
        ))}
      </SimpleGrid>
    </Stack>
  );
}

interface VideoCardProps {
  task: VideoTask;
  onRemove: () => void;
}

function VideoCard({ task, onRemove }: VideoCardProps) {
  // Handle both backend response structures
  let video: VideoData | undefined = task.result?.data?.[0];
  
  // Parse result if it's a string
  let parsedResult: unknown = task.result;
  if (typeof task.result === 'string') {
    try {
      parsedResult = JSON.parse(task.result) as unknown;
    } catch (e) {
      console.error('Failed to parse task result:', e);
    }
  }
  
  // Type guard for backend result structure
  interface BackendVideoResult {
    VideoUrl?: string;
    Duration?: number;
    Resolution?: string;
    FileSize?: number;
  }
  
  const isBackendResult = (obj: unknown): obj is BackendVideoResult => {
    return typeof obj === 'object' && obj !== null && 'VideoUrl' in obj;
  };
  
  // Check if we have the direct backend structure (VideoUrl instead of data array)
  if (!video && isBackendResult(parsedResult)) {
    // Convert backend structure to expected frontend structure
    video = {
      url: parsedResult.VideoUrl ?? '',
      metadata: {
        duration: parsedResult.Duration,
        resolution: parsedResult.Resolution,
        file_size_bytes: parsedResult.FileSize,
      }
    };
  } else if (!video && typeof parsedResult === 'object' && parsedResult !== null && 'data' in parsedResult) {
    // Try to get video from standard structure
    const standardResult = parsedResult as { data?: VideoData[] };
    video = standardResult.data?.[0];
  }
  
  // Fallback: Show pending state if no video data
  if (!video?.url) {
    // If task is running or pending, show progress
    if (task.status === 'pending' || task.status === 'running') {
      return (
        <Card shadow="sm" padding="lg" radius="md" withBorder>
          <Stack gap="sm">
            <Text size="sm" lineClamp={2}>{task.prompt}</Text>
            <Badge color="blue" variant="light">
              {task.status === 'pending' ? 'Queued' : 'Generating'}
            </Badge>
            <Progress value={task.progress} size="sm" animated />
            <Text size="xs" c="dimmed">ID: {task.id.slice(0, 8)}...</Text>
            <Button 
              onClick={onRemove} 
              color="red" 
              variant="light"
              size="sm"
              fullWidth
            >
              Cancel
            </Button>
          </Stack>
        </Card>
      );
    }
    
    // If failed, show error  
    if (task.status === 'failed' || task.error) {
      return (
        <Card shadow="sm" padding="lg" radius="md" withBorder>
          <Stack gap="sm">
            <Text size="sm" lineClamp={2}>{task.prompt}</Text>
            <Badge color="red" variant="light">Failed</Badge>
            <Text size="xs" c="red">
              <Group gap="xs">
                <IconAlertCircle size={14} />
                {task.error ?? 'Generation failed'}
              </Group>
            </Text>
            <Button 
              onClick={onRemove} 
              color="red" 
              variant="light"
              size="sm"
              fullWidth
            >
              Remove
            </Button>
          </Stack>
        </Card>
      );
    }
    
    // Shouldn't reach here, but show basic info
    return (
      <Card shadow="sm" padding="lg" radius="md" withBorder>
        <Stack gap="sm">
          <Text size="sm" lineClamp={2}>{task.prompt}</Text>
          <Badge variant="light">{task.status}</Badge>
          <Button 
            onClick={onRemove} 
            color="red" 
            variant="light"
            size="sm"
            fullWidth
          >
            Remove
          </Button>
        </Stack>
      </Card>
    );
  }

  const metadata = video.metadata;
  const downloadUrl = video.url ?? '';
  const downloadFilename = `video-${task.id.slice(0, 8)}.mp4`;

  const handleDownload = async () => {
    if (!downloadUrl) return;
    
    try {
      const response = await fetch(downloadUrl);
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = downloadFilename;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (error) {
      console.error('Error downloading video:', error);
    }
  };

  // Use simple HTML5 video element for better compatibility
  return (
    <Card shadow="sm" padding="lg" radius="md" withBorder>
      <Card.Section>
        <Box style={{ position: 'relative', backgroundColor: '#000' }}>
          {(() => {
            if (video.url) {
              return (
                <video
                  controls
                  preload="metadata"
                  style={{ width: '100%', height: 'auto', display: 'block' }}
                  onError={(e) => {
                    console.error('Video playback error:', e);
                    console.warn('Failed to load video from:', video.url);
                  }}
                >
                  <source src={video.url} type="video/mp4" />
                  Your browser does not support the video tag.
                </video>
              );
            }
            if (video.b64_json) {
              return (
                <video
                  controls
                  preload="metadata"
                  style={{ width: '100%', height: 'auto', display: 'block' }}
                >
                  <source src={`data:video/mp4;base64,${video.b64_json}`} type="video/mp4" />
                  Your browser does not support the video tag.
                </video>
              );
            }
            return (
              <Center h={200} bg="gray.1">
                <Text c="dimmed">No video available</Text>
              </Center>
            );
          })()}
        </Box>
      </Card.Section>
      
      <Stack gap="sm" mt="md">
        <Text size="sm" fw={500} lineClamp={2}>
          {task.prompt}
        </Text>
        
        <Group gap="xs" wrap="wrap">
          {metadata?.duration ? (
            <Badge variant="light" size="sm">{metadata.duration}s</Badge>
          ) : null}
          {metadata?.resolution && metadata.resolution !== '' ? (
            <Badge variant="light" size="sm">{metadata.resolution}</Badge>
          ) : null}
          {metadata?.fps ? (
            <Badge variant="light" size="sm">{metadata.fps} FPS</Badge>
          ) : null}
          {metadata?.file_size_bytes && metadata.file_size_bytes > 0 ? (
            <Badge variant="light" size="sm">{formatFileSize(Number(metadata.file_size_bytes))}</Badge>
          ) : null}
          {/* Show completion time if no other metadata */}
          {(!metadata || (!metadata.duration && !metadata.resolution && !metadata.fps && !metadata.file_size_bytes)) && (
            <Badge variant="light" color="green" size="sm">Completed</Badge>
          )}
        </Group>
        
        <Group gap="xs" grow>
          <Button
            onClick={() => void handleDownload()}
            size="sm"
            leftSection={<IconDownload size={16} />}
            disabled={!downloadUrl}
          >
            Download
          </Button>
          <Button
            onClick={onRemove}
            variant="light"
            size="sm"
            leftSection={<IconX size={16} />}
          >
            Remove
          </Button>
        </Group>
      </Stack>
    </Card>
  );
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}