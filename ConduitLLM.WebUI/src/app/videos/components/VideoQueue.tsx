'use client';

import { useState } from 'react';
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
    <button
      onClick={handleRetry}
      disabled={isRetrying}
      className="btn btn-secondary btn-sm"
      title="Retry generation"
    >
      {isRetrying ? 'Retrying...' : `Retry (${3 - task.retryCount} left)`}
    </button>
  );
}

export default function VideoQueue() {
  const { currentTask } = useVideoStore();
  const { cancelGeneration, retryGeneration } = useVideoGeneration();

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
          <div className="video-queue-item-icon">
            {getStatusIcon(currentTask.status)}
          </div>
          
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
              {currentTask.retryCount > 0 && (
                <span className="retry-info"> (Attempt {currentTask.retryCount + 1}/{3 + 1})</span>
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
              onClick={() => void cancelGeneration(currentTask.id)}
              className="btn btn-secondary btn-sm"
              title="Cancel generation"
            >
              Cancel
            </button>
          )}
          
          {(currentTask.status === 'failed' || currentTask.status === 'timedout') && (
            <RetryButton task={currentTask} onRetry={retryGeneration} />
          )}
          
          {(currentTask.status === 'failed' || currentTask.status === 'timedout' || currentTask.status === 'cancelled') && currentTask.error && (
            <div className={`error-message ${currentTask.status}-message`}>
              {currentTask.error}
            </div>
          )}
          
          {currentTask.retryHistory.length > 0 && (
            <div className="retry-history">
              <details>
                <summary>Retry History ({currentTask.retryHistory.length})</summary>
                <ul>
                  {currentTask.retryHistory.map((retry) => (
                    <li key={`${retry.attemptNumber}-${retry.timestamp}`}>
                      Attempt {retry.attemptNumber}: {retry.error} 
                      <span className="retry-timestamp"> ({new Date(retry.timestamp).toLocaleTimeString()})</span>
                    </li>
                  ))}
                </ul>
              </details>
            </div>
          )}
        </div>
      </div>
    </div>
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