'use client';

import { useMemo, useEffect, useState } from 'react';
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
import { 
  normalizeBackendVideoResponse,
  VideoMetadataExtractor,
  MetadataCache,
  type BackendVideoResponse
} from '@/app/utils/metadataExtractor';
import { MediaGenerationStatus, type MediaMetadata } from '@/app/types/media';

export default function VideoGallery() {
  const { taskHistory, removeTask, clearHistory } = useVideoStore();
  const [metadataCache] = useState(() => new MetadataCache());
  const [videoMetadata, setVideoMetadata] = useState<Record<string, MediaMetadata>>({});

  const completedVideos = useMemo(() => {
    // First filter for completed videos with results
    const completed = taskHistory.filter(task => task.status === MediaGenerationStatus.Completed && task.result);
    
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

  // Extract metadata for completed videos
  useEffect(() => {
    const extractAllMetadata = async () => {
      const extractor = new VideoMetadataExtractor();
      const metadataMap: Record<string, MediaMetadata> = {};
      
      for (const task of completedVideos) {
        if (!task.result) continue;
        
        // Get the video data
        let video: VideoData | undefined = task.result.data?.[0];
        
        // Parse result if it's a string
        let parsedResult: unknown = task.result;
        if (typeof task.result === 'string') {
          try {
            parsedResult = JSON.parse(task.result) as unknown;
          } catch (e) {
            console.error('Failed to parse task result:', e);
            continue;
          }
        }
        
        // Check if we have the direct backend structure
        const isBackendResult = (obj: unknown): obj is BackendVideoResponse => {
          return typeof obj === 'object' && obj !== null && 'VideoUrl' in obj;
        };
        
        if (!video && isBackendResult(parsedResult)) {
          video = normalizeBackendVideoResponse(parsedResult);
        } else if (!video && typeof parsedResult === 'object' && parsedResult !== null && 'data' in parsedResult) {
          const standardResult = parsedResult as { data?: VideoData[] };
          video = standardResult.data?.[0];
        }
        
        if (video) {
          const cacheKey = video.url ?? video.b64_json ?? task.id;
          
          // Check if we already have metadata for this video
          if (!metadataCache.has(video)) {
            const metadata = await extractor.extract(video);
            metadataCache.set(video, metadata);
            metadataMap[cacheKey] = metadata;
          } else {
            const cached = metadataCache.get(video);
            if (cached) {
              metadataMap[cacheKey] = cached;
            }
          }
        }
      }
      
      if (Object.keys(metadataMap).length > 0) {
        setVideoMetadata(prev => ({ ...prev, ...metadataMap }));
      }
    };

    void extractAllMetadata();
  }, [completedVideos, metadataCache]);

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
    
    const isBackendResult = (obj: unknown): obj is BackendVideoResponse => {
      return typeof obj === 'object' && obj !== null && 'VideoUrl' in obj;
    };
    
    // Check if we have the direct backend structure (VideoUrl instead of data array)
    if (!video && isBackendResult(parsedResult)) {
      // Convert backend structure to expected frontend structure
      video = normalizeBackendVideoResponse(parsedResult);
    } else if (!video && typeof parsedResult === 'object' && parsedResult !== null && 'data' in parsedResult) {
      // Try to get video from standard structure
      const standardResult = parsedResult as { data?: VideoData[] };
      video = standardResult.data?.[0];
    }
    
    // Get cached metadata for this video
    const cacheKey = video?.url ?? video?.b64_json ?? task.id;
    const metadata = videoMetadata[cacheKey] ?? video?.metadata;
    
    // Handle various states
    if (!video?.url) {
      return (
        <MediaCard
          key={task.id}
          prompt={task.prompt}
          status={task.status}
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
              {task.status === MediaGenerationStatus.Pending || task.status === MediaGenerationStatus.Generating ? 'Cancel' : 'Remove'}
            </Button>
          }
        />
      );
    }

    const downloadUrl = video.url ?? '';
    const downloadFilename = `video-${task.id.slice(0, 8)}.mp4`;

    const handleDownload = async () => {
      await downloadMedia({
        url: video?.url,
        b64_json: video?.b64_json,
        filename: downloadFilename,
        mimeType: 'video/mp4'
      });
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
            {metadata?.duration !== undefined && metadata.duration > 0 ? (
              <Badge variant="light" size="sm">{metadata.duration}s</Badge>
            ) : null}
            {metadata?.resolution && metadata.resolution !== '' ? (
              <Badge variant="light" size="sm">{metadata.resolution}</Badge>
            ) : null}
            {metadata?.fps !== undefined && metadata.fps > 0 ? (
              <Badge variant="light" size="sm">{metadata.fps} FPS</Badge>
            ) : null}
            {metadata?.file_size_bytes !== undefined && metadata.file_size_bytes > 0 ? (
              <Badge variant="light" size="sm">{formatFileSize(metadata.file_size_bytes)}</Badge>
            ) : null}
            {metadata?.codec ? (
              <Badge variant="light" size="sm">{metadata.codec}</Badge>
            ) : null}
            {(!metadata || (metadata.duration === undefined && !metadata.resolution && metadata.fps === undefined && metadata.file_size_bytes === undefined)) && (
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