# SignalR React Hooks

This directory contains React hooks for real-time SignalR communication with Conduit Core API.

## Available Hooks

### `useTaskHub`
Connect to the task hub for general task tracking.

```tsx
import { useTaskHub } from '@conduit/client/react-query';

function TaskTracker({ taskId }: { taskId: string }) {
  const { isConnected, error } = useTaskHub({
    taskId,
    onTaskStarted: (event) => {
      console.log('Task started:', event);
    },
    onTaskProgress: (event) => {
      console.log(`Progress: ${event.progress}%`);
    },
    onTaskCompleted: (event) => {
      console.log('Task completed:', event.result);
    },
    onTaskFailed: (event) => {
      console.error('Task failed:', event.error);
    },
  });

  if (error) return <div>Connection error: {error.message}</div>;
  if (!isConnected) return <div>Connecting...</div>;
  
  return <div>Tracking task: {taskId}</div>;
}
```

### `useVideoGenerationHub`
Track video generation progress in real-time.

```tsx
import { useVideoGenerationHub, useGenerateVideo } from '@conduit/client/react-query';

function VideoGenerator() {
  const generateVideo = useGenerateVideo();
  const [taskId, setTaskId] = useState<string>();
  const [progress, setProgress] = useState(0);

  const { isConnected } = useVideoGenerationHub({
    taskId,
    onVideoGenerationProgress: (event) => {
      setProgress(event.progress);
    },
    onVideoGenerationCompleted: (event) => {
      console.log('Video ready:', event.videoUrl);
    },
  });

  const handleGenerate = async () => {
    const result = await generateVideo.mutateAsync({
      prompt: 'A beautiful sunset over mountains',
      // ... other params
    });
    setTaskId(result.taskId);
  };

  return (
    <div>
      <button onClick={handleGenerate}>Generate Video</button>
      {taskId && <div>Progress: {progress}%</div>}
    </div>
  );
}
```

### `useImageGenerationHub`
Track image generation progress in real-time.

```tsx
import { useImageGenerationHub, useGenerateImages } from '@conduit/client/react-query';

function ImageGenerator() {
  const generateImages = useGenerateImages();
  const [taskId, setTaskId] = useState<string>();
  const [imageUrl, setImageUrl] = useState<string>();

  const { isConnected } = useImageGenerationHub({
    taskId,
    onImageGenerationCompleted: (event) => {
      setImageUrl(event.imageUrl);
    },
    onImageGenerationFailed: (event) => {
      alert(`Generation failed: ${event.error}`);
    },
  });

  const handleGenerate = async () => {
    const result = await generateImages.mutateAsync({
      prompt: 'A futuristic city at night',
      n: 4,
      // ... other params
    });
    setTaskId(result.taskId);
  };

  return (
    <div>
      <button onClick={handleGenerate}>Generate Images</button>
      {imageUrl && (
        <img src={imageUrl} alt="Generated image" />
      )}
    </div>
  );
}
```

## Hook Options

All SignalR hooks accept these common options:

```typescript
interface SignalRHookOptions {
  // Connection options
  enabled?: boolean;              // Auto-connect on mount (default: true)
  onConnected?: () => void;       // Called when connected
  onDisconnected?: (error?: Error) => void;
  onReconnecting?: (error?: Error) => void;
  onReconnected?: () => void;
  
  // Subscription options
  taskId?: string;                // Auto-subscribe to specific task
  taskType?: string;              // Auto-subscribe to task type (useTaskHub only)
  autoSubscribe?: boolean;        // Auto-subscribe when connected (default: true)
  
  // Event handlers (hook-specific)
  onTaskStarted?: (event) => void;
  onTaskProgress?: (event) => void;
  // ... etc
}
```

## Advanced Usage

### Manual Connection Management

```tsx
const { connect, disconnect, isConnected } = useTaskHub({
  enabled: false, // Don't auto-connect
});

// Connect manually
await connect();

// Disconnect when done
await disconnect();
```

### Multiple Subscriptions

```tsx
const { subscribeToTask, unsubscribeFromTask } = useTaskHub({
  onTaskProgress: (event) => {
    console.log(`Task ${event.taskId}: ${event.progress}%`);
  },
});

// Subscribe to multiple tasks
await subscribeToTask('task-1');
await subscribeToTask('task-2');

// Unsubscribe when done
await unsubscribeFromTask('task-1');
```

### Handling All Events

```tsx
const { } = useTaskHub({
  onAnyTaskEvent: (event) => {
    // Handle any task event
    switch (event.eventType) {
      case 'TaskStarted':
        // ...
        break;
      case 'TaskCompleted':
        // ...
        break;
    }
  },
});
```

## TypeScript Support

All hooks are fully typed. Event handlers receive properly typed events:

```tsx
useVideoGenerationHub({
  onVideoGenerationProgress: (event: VideoGenerationProgressEvent) => {
    // event is fully typed
    console.log(event.progress);        // number
    console.log(event.currentFrame);    // number | undefined
    console.log(event.totalFrames);     // number | undefined
  },
});
```

## Error Handling

```tsx
const { error, isReconnecting } = useTaskHub({
  onTaskProgress: (event) => {
    // ...
  },
});

if (error) {
  return <div>Connection error: {error.message}</div>;
}

if (isReconnecting) {
  return <div>Reconnecting...</div>;
}
```

## Security

- Uses virtual keys for authentication (passed via `ConduitProvider`)
- Connections are secured with WSS in production
- Virtual keys only have access to their own resources
- No sensitive data is exposed to the client