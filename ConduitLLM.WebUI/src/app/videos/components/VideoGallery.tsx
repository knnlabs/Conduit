'use client';

import { useMemo } from 'react';
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
      <div className="video-gallery">
        <div className="empty-state">
          <h3>No videos generated yet</h3>
          <p>Your generated videos will appear here</p>
        </div>
      </div>
    );
  }

  return (
    <div className="video-gallery">
      <div className="video-gallery-header">
        <h3>Generated Videos ({completedVideos.length})</h3>
        <button
          onClick={clearHistory}
          className="btn btn-secondary btn-sm"
        >
          Clear History
        </button>
      </div>
      
      <div className="video-gallery-content">
        {completedVideos.map((task) => (
          <VideoCard 
            key={task.id} 
            task={task} 
            onRemove={() => removeTask(task.id)}
          />
        ))}
      </div>
    </div>
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
  if (!video || !video.url) {
    // If task is running or pending, show progress
    if (task.status === 'pending' || task.status === 'running') {
      return (
        <div className="video-card video-card-pending">
          <div className="video-card-info">
            <p className="video-prompt">{task.prompt}</p>
            <p className="video-status">Status: {task.status}</p>
            <p className="video-progress">Progress: {task.progress}%</p>
            <p className="video-id">ID: {task.id.slice(0, 8)}...</p>
            <button onClick={onRemove} className="btn btn-sm btn-danger">
              Cancel
            </button>
          </div>
        </div>
      );
    }
    
    // If failed, show error  
    if (task.status === 'failed' || task.error) {
      return (
        <div className="video-card video-card-error">
          <div className="video-card-info">
            <p className="video-prompt">{task.prompt}</p>
            <p className="video-status">Status: {task.status}</p>
            <p className="video-error">Error: {task.error ?? 'Generation failed'}</p>
            <button onClick={onRemove} className="btn btn-sm btn-danger">
              Remove
            </button>
          </div>
        </div>
      );
    }
    
    // Shouldn't reach here, but show basic info
    return (
      <div className="video-card video-card-pending">
        <div className="video-card-info">
          <p className="video-prompt">{task.prompt}</p>
          <p className="video-status">Status: {task.status}</p>
          <button onClick={onRemove} className="btn btn-sm btn-danger">
            Remove
          </button>
        </div>
      </div>
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
    <div className="video-card">
      <div className="video-card-player">
        {(() => {
          if (video.url) {
            return (
              <video
                controls
                preload="metadata"
                style={{ width: '100%', height: 'auto', backgroundColor: '#000' }}
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
                style={{ width: '100%', height: 'auto', backgroundColor: '#000' }}
              >
                <source src={`data:video/mp4;base64,${video.b64_json}`} type="video/mp4" />
                Your browser does not support the video tag.
              </video>
            );
          }
          return <div className="video-placeholder">No video available</div>;
        })()}
      </div>
      
      <div className="video-card-content">
        <div className="video-card-prompt">
          {task.prompt}
        </div>
        
        <div className="video-card-metadata">
          {metadata?.duration ? (
            <span>{metadata.duration}s</span>
          ) : null}
          {metadata?.resolution && metadata.resolution !== '' ? (
            <span>{metadata.resolution}</span>
          ) : null}
          {metadata?.fps ? (
            <span>{metadata.fps} FPS</span>
          ) : null}
          {metadata?.file_size_bytes && metadata.file_size_bytes > 0 ? (
            <span>{formatFileSize(Number(metadata.file_size_bytes))}</span>
          ) : null}
          {/* Show completion time if no other metadata */}
          {(!metadata || (!metadata.duration && !metadata.resolution && !metadata.fps && !metadata.file_size_bytes)) && (
            <span>Completed</span>
          )}
        </div>
        
        <div className="video-card-actions">
          <button
            onClick={() => void handleDownload()}
            className="btn btn-primary btn-sm"
            disabled={!downloadUrl}
          >
            Download
          </button>
          <button
            onClick={onRemove}
            className="btn btn-secondary btn-sm"
          >
            Remove
          </button>
        </div>
      </div>
    </div>
  );
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}