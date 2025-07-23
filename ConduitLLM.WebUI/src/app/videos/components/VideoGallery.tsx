'use client';

import { useMemo } from 'react';
import { Stack, Title, Text, Group, Button, Card, SimpleGrid, Badge } from '@mantine/core';
import { IconDownload, IconTrash } from '@tabler/icons-react';
import { useVideoStore } from '../hooks/useVideoStore';
import VideoPlayer from './VideoPlayer';
import type { VideoTask } from '../types';

export default function VideoGallery() {
  const { taskHistory, removeTask, clearHistory } = useVideoStore();

  const completedVideos = useMemo(() => 
    taskHistory.filter(task => task.status === 'completed' && task.result),
    [taskHistory]
  );

  if (completedVideos.length === 0) {
    return (
      <Stack align="center" justify="center" h="100%" gap="sm">
        <Title order={3} c="dimmed">No videos generated yet</Title>
        <Text c="dimmed">Your generated videos will appear here</Text>
      </Stack>
    );
  }

  return (
    <Stack h="100%" gap="md">
      <Group justify="space-between">
        <Title order={3}>Generated Videos ({completedVideos.length})</Title>
        <Button
          variant="light"
          size="sm"
          onClick={clearHistory}
        >
          Clear History
        </Button>
      </Group>
      
      <SimpleGrid cols={{ base: 1, sm: 2, md: 3 }} spacing="lg" style={{ flex: 1, overflow: 'auto' }}>
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
  const video = task.result?.data[0];
  
  if (!video) {
    return null;
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

  return (
    <Card p="sm" withBorder>
      <Card.Section>
        {(() => {
          if (video.url) {
            return (
              <VideoPlayer
                src={video.url}
                poster={undefined}
                title={task.prompt}
              />
            );
          }
          if (video.b64Json) {
            return (
              <VideoPlayer
                src={`data:video/mp4;base64,${video.b64Json}`}
                poster={undefined}
                title={task.prompt}
              />
            );
          }
          return (
            <div style={{ width: '100%', aspectRatio: '16/9', background: '#000', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Text c="dimmed">No video available</Text>
            </div>
          );
        })()}
      </Card.Section>
      
      <Card.Section p="sm">
        <Text size="sm" lineClamp={2} mb="xs">
          {task.prompt}
        </Text>
        
        {metadata && (
          <Group gap="xs" mb="sm">
            {metadata.duration && <Badge size="sm" variant="light">{metadata.duration}s</Badge>}
            {metadata.resolution && <Badge size="sm" variant="light">{metadata.resolution}</Badge>}
            {metadata.fps && <Badge size="sm" variant="light">{metadata.fps} FPS</Badge>}
            {metadata.fileSizeBytes && (
              <Badge size="sm" variant="light">{formatFileSize(Number(metadata.fileSizeBytes))}</Badge>
            )}
          </Group>
        )}
        
        <Group gap="xs">
          <Button
            size="xs"
            variant="light"
            leftSection={<IconDownload size={14} />}
            onClick={() => void handleDownload()}
            disabled={!downloadUrl}
          >
            Download
          </Button>
          <Button
            size="xs"
            variant="light"
            color="red"
            leftSection={<IconTrash size={14} />}
            onClick={onRemove}
          >
            Remove
          </Button>
        </Group>
      </Card.Section>
    </Card>
  );
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}