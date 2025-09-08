'use client';

import { useMemo } from 'react';
import { 
  Button, 
  Group, 
  Badge,
  Box,
  Text
} from '@mantine/core';
import { 
  IconDownload,
  IconX
} from '@tabler/icons-react';
import { useVideoStore } from '../hooks/useVideoStore';
import type { VideoTask, VideoData } from '../types';
import { 
  MediaGallery, 
  MediaCard, 
  MediaContent,
  MediaPlaceholder,
  downloadMedia,
  formatFileSize
} from '@/app/components/media';

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

  const renderVideoCard = (task: VideoTask) => {
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
    
    // Handle various states
    if (!video?.url) {
      return (
        <MediaCard
          key={task.id}
          prompt={task.prompt}
          status={task.status as 'pending' | 'running' | 'completed' | 'failed'}
          progress={task.progress}
          error={task.error}
          actions={
            <Button 
              onClick={() => removeTask(task.id)} 
              color="red" 
              variant="light"
              size="sm"
              fullWidth
            >
              {task.status === 'pending' || task.status === 'running' ? 'Cancel' : 'Remove'}
            </Button>
          }
        />
      );
    }

    const metadata = video.metadata;
    const downloadUrl = video.url ?? '';
    const downloadFilename = `video-${task.id.slice(0, 8)}.mp4`;

    const handleDownload = async () => {
      await downloadMedia(video?.url, video?.b64_json, downloadFilename, 'video/mp4');
    };

    // Render completed video card
    return (
      <MediaCard key={task.id}>
        <MediaContent backgroundColor="#000">
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
            return <MediaPlaceholder message="No video available" />;
          })()}
        </MediaContent>
        
        <Box mt="md">
          <Text size="sm" fw={500} lineClamp={2}>
            {task.prompt}
          </Text>
          
          <Group gap="xs" wrap="wrap" mt="xs">
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
            {(!metadata || (!metadata.duration && !metadata.resolution && !metadata.fps && !metadata.file_size_bytes)) && (
              <Badge variant="light" color="green" size="sm">Completed</Badge>
            )}
          </Group>
          
          <Group gap="xs" grow mt="sm">
            <Button
              onClick={() => void handleDownload()}
              size="sm"
              leftSection={<IconDownload size={16} />}
              disabled={!downloadUrl}
            >
              Download
            </Button>
            <Button
              onClick={() => removeTask(task.id)}
              variant="light"
              size="sm"
              leftSection={<IconX size={16} />}
            >
              Remove
            </Button>
          </Group>
        </Box>
      </MediaCard>
    );
  };

  return (
    <MediaGallery
      items={completedVideos}
      renderCard={renderVideoCard}
      onClearAll={clearHistory}
      emptyTitle="No videos generated yet"
      emptyMessage="Your generated videos will appear here"
      title={(count) => `Generated Videos (${count})`}
      cols={{ base: 1, md: 2, lg: 3 }}
      clearButtonText="Clear History"
    />
  );
}