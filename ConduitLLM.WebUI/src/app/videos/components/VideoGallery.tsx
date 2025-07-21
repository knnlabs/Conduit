'use client';

import { useMemo } from 'react';
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
    <div className="video-card">
      <div className="video-card-player">
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
          return <div className="video-placeholder">No video available</div>;
        })()}
      </div>
      
      <div className="video-card-content">
        <div className="video-card-prompt">
          {task.prompt}
        </div>
        
        {metadata && (
          <div className="video-card-metadata">
            {metadata.duration && <span>{metadata.duration}s</span>}
            {metadata.resolution && <span>{metadata.resolution}</span>}
            {metadata.fps && <span>{metadata.fps} FPS</span>}
            {metadata.fileSizeBytes && (
              <span>{formatFileSize(Number(metadata.fileSizeBytes))}</span>
            )}
          </div>
        )}
        
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