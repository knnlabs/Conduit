'use client';

import { useVideoStore } from '../hooks/useVideoStore';
import { useVideoGeneration } from '../hooks/useVideoGeneration';

export default function VideoQueue() {
  const { currentTask } = useVideoStore();
  const { cancelGeneration } = useVideoGeneration();

  if (!currentTask) {
    return null;
  }

  const isActive = currentTask.status === 'pending' || currentTask.status === 'running';

  return (
    <div className="video-queue">
      <div className="video-queue-header">
        <span className="queue-icon">🎬</span>
        Video Generation Queue
      </div>
      
      <div className="video-queue-items">
        <div className="video-queue-item">
          {isActive && (
            <div className="video-queue-item-spinner">⏳</div>
          )}
          
          <div className="video-queue-item-info">
            <div className="video-queue-item-prompt">
              {currentTask.prompt}
            </div>
            
            <div className="video-queue-item-status">
              Status: {getStatusText(currentTask.status)}
              {currentTask.message && ` - ${currentTask.message}`}
              {currentTask.estimatedTimeToCompletion && (
                <span> (ETA: {formatTime(currentTask.estimatedTimeToCompletion)})</span>
              )}
            </div>
            
            {currentTask.progress > 0 && (
              <div className="video-queue-item-progress">
                <div 
                  className="video-queue-item-progress-bar"
                  style={{ width: `${currentTask.progress}%` }}
                />
              </div>
            )}
          </div>
          
          {isActive && (
            <button
              onClick={() => cancelGeneration(currentTask.id)}
              className="btn btn-secondary btn-sm"
              title="Cancel generation"
            >
              Cancel
            </button>
          )}
          
          {currentTask.status === 'failed' && currentTask.error && (
            <div className="error-message">
              {currentTask.error}
            </div>
          )}
        </div>
      </div>
    </div>
  );
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