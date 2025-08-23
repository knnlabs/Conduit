# Conduit Real-Time JavaScript/TypeScript Client

A production-ready JavaScript/TypeScript client for Conduit's real-time APIs with complete error handling, reconnection logic, and React integration.

## Overview

This client provides comprehensive support for real-time image and video generation with SignalR WebSocket connections, automatic reconnection, and event-driven architecture.

## Related Documentation

- [Real-Time API Guide](../real-time-api-guide.md) - Complete real-time API documentation
- [Python Client](./python-client.md) - Python client implementation
- [C# Client](./csharp-client.md) - C#/.NET client implementation
- [Common Patterns](./common-patterns.md) - Shared patterns and best practices

## Installation

```bash
npm install @microsoft/signalr
npm install --save-dev @types/node  # For TypeScript
```

## Complete Client Implementation

```typescript
// conduit-realtime-client.ts
import * as signalR from '@microsoft/signalr';
import { EventEmitter } from 'events';

export interface ConduitClientOptions {
    virtualKey: string;
    baseUrl?: string;
    maxReconnectAttempts?: number;
    enableLogging?: boolean;
}

export interface TaskResult {
    taskId: string;
    status: 'completed' | 'failed' | 'cancelled';
    imageUrl?: string;
    videoUrl?: string;
    error?: string;
    metadata?: Record<string, any>;
}

export interface ProgressUpdate {
    taskId: string;
    progress: number;
    message?: string;
    estimatedSecondsRemaining?: number;
}

export class ConduitRealtimeClient extends EventEmitter {
    private connection: signalR.HubConnection | null = null;
    private readonly options: Required<ConduitClientOptions>;
    private readonly activeTasks = new Map<string, { hubType: 'image' | 'video' }>();
    private reconnectTimer: NodeJS.Timeout | null = null;
    private isDisposed = false;

    constructor(options: ConduitClientOptions) {
        super();
        this.options = {
            baseUrl: 'https://api.conduit.im',
            maxReconnectAttempts: 5,
            enableLogging: false,
            ...options
        };
    }

    /**
     * Connect to a specific hub (image or video generation)
     */
    async connect(hubType: 'image' | 'video'): Promise<void> {
        if (this.isDisposed) {
            throw new Error('Client has been disposed');
        }

        const hubUrl = `${this.options.baseUrl}/hubs/${hubType}-generation`;
        
        const connectionBuilder = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: () => this.options.virtualKey,
                transport: signalR.HttpTransportType.WebSockets,
                skipNegotiation: true
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: (retryContext) => {
                    if (retryContext.previousRetryCount >= this.options.maxReconnectAttempts) {
                        return null; // Stop reconnecting
                    }
                    
                    // Exponential backoff with jitter
                    const baseDelay = Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
                    const jitter = Math.random() * 1000;
                    return baseDelay + jitter;
                }
            });

        if (this.options.enableLogging) {
            connectionBuilder.configureLogging(signalR.LogLevel.Information);
        }

        this.connection = connectionBuilder.build();
        this.setupEventHandlers();
        
        try {
            await this.connection.start();
            this.emit('connected', { hubType });
        } catch (error) {
            this.emit('error', { type: 'connection', error });
            throw error;
        }
    }

    /**
     * Subscribe to updates for a specific task
     */
    async subscribeToTask(taskId: string, taskType: 'image' | 'video'): Promise<void> {
        if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
            throw new Error('Not connected to hub');
        }

        const methodName = taskType === 'image' ? 'SubscribeToTask' : 'SubscribeToRequest';
        
        try {
            await this.connection.invoke(methodName, taskId);
            this.activeTasks.set(taskId, { hubType: taskType });
            this.emit('subscribed', { taskId, taskType });
        } catch (error) {
            if ((error as any).message?.includes('already subscribed')) {
                // Already subscribed, not an error
                return;
            }
            this.emit('error', { type: 'subscription', taskId, error });
            throw error;
        }
    }

    /**
     * Unsubscribe from task updates
     */
    async unsubscribeFromTask(taskId: string): Promise<void> {
        if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        const task = this.activeTasks.get(taskId);
        if (!task) return;

        const methodName = task.hubType === 'image' ? 'UnsubscribeFromTask' : 'UnsubscribeFromRequest';
        
        try {
            await this.connection.invoke(methodName, taskId);
            this.activeTasks.delete(taskId);
            this.emit('unsubscribed', { taskId });
        } catch (error) {
            // Log but don't throw - task might already be completed
            console.warn(`Failed to unsubscribe from ${taskId}:`, error);
        }
    }

    /**
     * Create an image and subscribe to updates
     */
    async generateImage(params: {
        model: string;
        prompt: string;
        size?: string;
        n?: number;
    }): Promise<string> {
        const response = await fetch(`${this.options.baseUrl}/v1/images/generations/async`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${this.options.virtualKey}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(params)
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error?.message || 'Image generation failed');
        }

        const result = await response.json();
        const taskId = result.task_id;

        // Auto-subscribe if connected
        if (this.connection?.state === signalR.HubConnectionState.Connected) {
            await this.subscribeToTask(taskId, 'image');
        }

        return taskId;
    }

    /**
     * Create a video and subscribe to updates
     */
    async generateVideo(params: {
        model: string;
        prompt: string;
        duration?: number;
        size?: string;
    }): Promise<string> {
        const response = await fetch(`${this.options.baseUrl}/v1/videos/generations/async`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${this.options.virtualKey}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(params)
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error?.message || 'Video generation failed');
        }

        const result = await response.json();
        const taskId = result.request_id;

        // Auto-subscribe if connected
        if (this.connection?.state === signalR.HubConnectionState.Connected) {
            await this.subscribeToTask(taskId, 'video');
        }

        return taskId;
    }

    /**
     * Disconnect and clean up resources
     */
    async disconnect(): Promise<void> {
        this.isDisposed = true;
        
        if (this.reconnectTimer) {
            clearTimeout(this.reconnectTimer);
        }

        if (this.connection) {
            // Unsubscribe from all tasks
            for (const taskId of this.activeTasks.keys()) {
                await this.unsubscribeFromTask(taskId);
            }

            await this.connection.stop();
            this.connection = null;
        }

        this.removeAllListeners();
    }

    private setupEventHandlers(): void {
        if (!this.connection) return;

        // Task/Request progress
        this.connection.on('TaskProgress', (taskId: string, progress: number) => {
            this.handleProgress(taskId, progress);
        });

        this.connection.on('RequestProgress', (requestId: string, progress: number) => {
            this.handleProgress(requestId, progress);
        });

        // Task/Request completion
        this.connection.on('TaskCompleted', (taskId: string, result: any) => {
            this.handleCompletion(taskId, result);
        });

        this.connection.on('RequestCompleted', (requestId: string, result: any) => {
            this.handleCompletion(requestId, result);
        });

        // Task/Request failure
        this.connection.on('TaskFailed', (taskId: string, error: string) => {
            this.handleFailure(taskId, error);
        });

        this.connection.on('RequestFailed', (requestId: string, error: string) => {
            this.handleFailure(requestId, error);
        });

        // Connection lifecycle
        this.connection.onreconnecting((error) => {
            this.emit('reconnecting', { error });
        });

        this.connection.onreconnected(async (connectionId) => {
            this.emit('reconnected', { connectionId });
            await this.resubscribeToActiveTasks();
        });

        this.connection.onclose((error) => {
            this.emit('disconnected', { error });
            
            if (!this.isDisposed && error) {
                // Attempt manual reconnection after delay
                this.reconnectTimer = setTimeout(() => {
                    this.reconnect();
                }, 5000);
            }
        });
    }

    private handleProgress(taskId: string, progress: number): void {
        const update: ProgressUpdate = {
            taskId,
            progress
        };
        this.emit('progress', update);
    }

    private handleCompletion(taskId: string, result: any): void {
        const taskResult: TaskResult = {
            taskId,
            status: 'completed',
            imageUrl: result.image_url,
            videoUrl: result.video_url,
            metadata: result.metadata
        };
        
        this.emit('completed', taskResult);
        this.activeTasks.delete(taskId);
    }

    private handleFailure(taskId: string, error: string): void {
        const taskResult: TaskResult = {
            taskId,
            status: 'failed',
            error
        };
        
        this.emit('failed', taskResult);
        this.activeTasks.delete(taskId);
    }

    private async resubscribeToActiveTasks(): Promise<void> {
        const tasks = Array.from(this.activeTasks.entries());
        
        for (const [taskId, { hubType }] of tasks) {
            try {
                await this.subscribeToTask(taskId, hubType);
            } catch (error) {
                console.error(`Failed to resubscribe to ${taskId}:`, error);
                this.activeTasks.delete(taskId);
            }
        }
    }

    private async reconnect(): Promise<void> {
        if (this.isDisposed || !this.connection) return;

        try {
            await this.connection.start();
        } catch (error) {
            this.emit('error', { type: 'reconnection', error });
            
            // Try again
            if (!this.isDisposed) {
                this.reconnectTimer = setTimeout(() => {
                    this.reconnect();
                }, 10000);
            }
        }
    }
}
```

## Basic Usage Example

```typescript
// Usage example
async function main() {
    const client = new ConduitRealtimeClient({
        virtualKey: 'condt_your_virtual_key',
        enableLogging: true
    });

    // Set up event handlers
    client.on('connected', ({ hubType }) => {
        console.log(`Connected to ${hubType} hub`);
    });

    client.on('progress', ({ taskId, progress }) => {
        console.log(`Task ${taskId}: ${progress}%`);
    });

    client.on('completed', ({ taskId, imageUrl, videoUrl }) => {
        console.log(`Task ${taskId} completed!`);
        if (imageUrl) console.log(`Image: ${imageUrl}`);
        if (videoUrl) console.log(`Video: ${videoUrl}`);
    });

    client.on('failed', ({ taskId, error }) => {
        console.error(`Task ${taskId} failed: ${error}`);
    });

    client.on('error', ({ type, error }) => {
        console.error(`Error (${type}):`, error);
    });

    try {
        // Connect to image generation hub
        await client.connect('image');

        // Generate an image
        const taskId = await client.generateImage({
            model: 'dall-e-3',
            prompt: 'A beautiful sunset over mountains'
        });

        console.log(`Started image generation: ${taskId}`);

        // Wait for completion (in real app, this would be event-driven)
        await new Promise(resolve => {
            client.once('completed', resolve);
            client.once('failed', resolve);
        });

    } finally {
        await client.disconnect();
    }
}

// Run if this is the main module
if (require.main === module) {
    main().catch(console.error);
}
```

## React Hook Integration

```typescript
// useConduitRealtime.ts
import { useEffect, useRef, useState, useCallback } from 'react';
import { ConduitRealtimeClient, TaskResult, ProgressUpdate } from './conduit-realtime-client';

interface UseConduitRealtimeOptions {
    virtualKey: string;
    autoConnect?: boolean;
    hubType?: 'image' | 'video';
}

interface UseConduitRealtimeReturn {
    isConnected: boolean;
    isConnecting: boolean;
    error: Error | null;
    connect: (hubType: 'image' | 'video') => Promise<void>;
    disconnect: () => Promise<void>;
    generateImage: (params: any) => Promise<string>;
    generateVideo: (params: any) => Promise<string>;
    subscribeToTask: (taskId: string, taskType: 'image' | 'video') => Promise<void>;
    tasks: Map<string, TaskState>;
}

interface TaskState {
    id: string;
    progress: number;
    status: 'pending' | 'completed' | 'failed';
    result?: TaskResult;
    error?: string;
}

export function useConduitRealtime(options: UseConduitRealtimeOptions): UseConduitRealtimeReturn {
    const clientRef = useRef<ConduitRealtimeClient | null>(null);
    const [isConnected, setIsConnected] = useState(false);
    const [isConnecting, setIsConnecting] = useState(false);
    const [error, setError] = useState<Error | null>(null);
    const [tasks, setTasks] = useState<Map<string, TaskState>>(new Map());

    useEffect(() => {
        const client = new ConduitRealtimeClient({
            virtualKey: options.virtualKey,
            enableLogging: process.env.NODE_ENV === 'development'
        });

        clientRef.current = client;

        // Set up event handlers
        client.on('connected', () => {
            setIsConnected(true);
            setIsConnecting(false);
            setError(null);
        });

        client.on('disconnected', () => {
            setIsConnected(false);
        });

        client.on('reconnecting', () => {
            setIsConnecting(true);
        });

        client.on('reconnected', () => {
            setIsConnected(true);
            setIsConnecting(false);
        });

        client.on('error', ({ error }) => {
            setError(error);
            setIsConnecting(false);
        });

        client.on('progress', ({ taskId, progress }: ProgressUpdate) => {
            setTasks(prev => {
                const newTasks = new Map(prev);
                const task = newTasks.get(taskId);
                if (task) {
                    task.progress = progress;
                }
                return newTasks;
            });
        });

        client.on('completed', (result: TaskResult) => {
            setTasks(prev => {
                const newTasks = new Map(prev);
                const task = newTasks.get(result.taskId);
                if (task) {
                    task.status = 'completed';
                    task.result = result;
                    task.progress = 100;
                }
                return newTasks;
            });
        });

        client.on('failed', (result: TaskResult) => {
            setTasks(prev => {
                const newTasks = new Map(prev);
                const task = newTasks.get(result.taskId);
                if (task) {
                    task.status = 'failed';
                    task.error = result.error;
                }
                return newTasks;
            });
        });

        // Auto-connect if requested
        if (options.autoConnect && options.hubType) {
            client.connect(options.hubType).catch(setError);
        }

        // Cleanup
        return () => {
            client.disconnect();
        };
    }, [options.virtualKey]);

    const connect = useCallback(async (hubType: 'image' | 'video') => {
        if (!clientRef.current) return;
        setIsConnecting(true);
        try {
            await clientRef.current.connect(hubType);
        } catch (err) {
            setError(err as Error);
            throw err;
        }
    }, []);

    const disconnect = useCallback(async () => {
        if (!clientRef.current) return;
        await clientRef.current.disconnect();
    }, []);

    const generateImage = useCallback(async (params: any) => {
        if (!clientRef.current) throw new Error('Client not initialized');
        
        const taskId = await clientRef.current.generateImage(params);
        
        setTasks(prev => {
            const newTasks = new Map(prev);
            newTasks.set(taskId, {
                id: taskId,
                progress: 0,
                status: 'pending'
            });
            return newTasks;
        });
        
        return taskId;
    }, []);

    const generateVideo = useCallback(async (params: any) => {
        if (!clientRef.current) throw new Error('Client not initialized');
        
        const taskId = await clientRef.current.generateVideo(params);
        
        setTasks(prev => {
            const newTasks = new Map(prev);
            newTasks.set(taskId, {
                id: taskId,
                progress: 0,
                status: 'pending'
            });
            return newTasks;
        });
        
        return taskId;
    }, []);

    const subscribeToTask = useCallback(async (taskId: string, taskType: 'image' | 'video') => {
        if (!clientRef.current) throw new Error('Client not initialized');
        await clientRef.current.subscribeToTask(taskId, taskType);
    }, []);

    return {
        isConnected,
        isConnecting,
        error,
        connect,
        disconnect,
        generateImage,
        generateVideo,
        subscribeToTask,
        tasks
    };
}
```

## React Component Example

```typescript
// Usage in React component
function ImageGenerator() {
    const {
        isConnected,
        isConnecting,
        error,
        connect,
        generateImage,
        tasks
    } = useConduitRealtime({
        virtualKey: 'condt_your_key',
        autoConnect: true,
        hubType: 'image'
    });

    const [prompt, setPrompt] = useState('');
    const [isGenerating, setIsGenerating] = useState(false);

    const handleGenerate = async () => {
        try {
            setIsGenerating(true);
            const taskId = await generateImage({
                model: 'dall-e-3',
                prompt
            });
            console.log('Started task:', taskId);
        } catch (err) {
            console.error('Generation failed:', err);
        } finally {
            setIsGenerating(false);
        }
    };

    return (
        <div>
            {error && <div className="error">{error.message}</div>}
            
            <div className="status">
                {isConnecting ? 'Connecting...' : isConnected ? 'Connected' : 'Disconnected'}
            </div>

            <input
                type="text"
                value={prompt}
                onChange={(e) => setPrompt(e.target.value)}
                placeholder="Enter your prompt"
            />

            <button
                onClick={handleGenerate}
                disabled={!isConnected || isGenerating || !prompt}
            >
                Generate Image
            </button>

            <div className="tasks">
                {Array.from(tasks.values()).map(task => (
                    <div key={task.id} className="task">
                        <div>Task {task.id}</div>
                        <div>Status: {task.status}</div>
                        <div>Progress: {task.progress}%</div>
                        {task.result?.imageUrl && (
                            <img src={task.result.imageUrl} alt="Generated" />
                        )}
                        {task.error && <div className="error">{task.error}</div>}
                    </div>
                ))}
            </div>
        </div>
    );
}
```

## Advanced Features

### Connection Pool Management

```typescript
class ConduitConnectionPool {
    private pools = new Map<string, ConduitRealtimeClient[]>();
    private maxPoolSize = 5;

    async getClient(virtualKey: string, hubType: 'image' | 'video'): Promise<ConduitRealtimeClient> {
        const poolKey = `${virtualKey}:${hubType}`;
        let pool = this.pools.get(poolKey) || [];

        // Find available client
        for (const client of pool) {
            if (client.isAvailable()) {
                return client;
            }
        }

        // Create new client if pool not full
        if (pool.length < this.maxPoolSize) {
            const client = new ConduitRealtimeClient({ virtualKey });
            await client.connect(hubType);
            pool.push(client);
            this.pools.set(poolKey, pool);
            return client;
        }

        throw new Error('Connection pool exhausted');
    }

    async cleanup(): Promise<void> {
        for (const pool of this.pools.values()) {
            for (const client of pool) {
                await client.disconnect();
            }
        }
        this.pools.clear();
    }
}
```

### Error Recovery Strategy

```typescript
class ConduitRealtimeManager {
    private client: ConduitRealtimeClient;
    private retryQueue = new Set<string>();
    private maxRetries = 3;

    constructor(options: ConduitClientOptions) {
        this.client = new ConduitRealtimeClient(options);
        this.setupErrorRecovery();
    }

    private setupErrorRecovery(): void {
        this.client.on('failed', ({ taskId, error }) => {
            if (error.includes('network') || error.includes('timeout')) {
                this.addToRetryQueue(taskId);
            }
        });

        this.client.on('reconnected', () => {
            this.processRetryQueue();
        });
    }

    private addToRetryQueue(taskId: string): void {
        this.retryQueue.add(taskId);
    }

    private async processRetryQueue(): Promise<void> {
        for (const taskId of this.retryQueue) {
            try {
                await this.client.subscribeToTask(taskId, 'image'); // Retry subscription
                this.retryQueue.delete(taskId);
            } catch (error) {
                console.error(`Failed to retry ${taskId}:`, error);
            }
        }
    }
}
```

## Next Steps

- [Python Client](./python-client.md) - Python implementation
- [C# Client](./csharp-client.md) - C#/.NET implementation  
- [Common Patterns](./common-patterns.md) - Shared patterns and debugging
- [Real-Time API Guide](../real-time-api-guide.md) - Complete API documentation