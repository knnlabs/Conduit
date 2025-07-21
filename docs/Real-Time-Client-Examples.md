# Conduit Real-Time Client Examples

Production-ready client implementations for Conduit's real-time APIs with complete error handling, reconnection logic, and best practices.

## JavaScript/TypeScript Client

### Complete Implementation

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

### React Hook Example

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

## Python Client

### Complete Implementation

```python
# conduit_realtime_client.py
import asyncio
import json
import logging
from typing import Optional, Dict, Any, Callable, Set
from datetime import datetime, timedelta
from dataclasses import dataclass
from enum import Enum
import aiohttp
import websockets
from websockets.exceptions import WebSocketException

logger = logging.getLogger(__name__)

class TaskStatus(Enum):
    PENDING = "pending"
    PROCESSING = "processing"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"

@dataclass
class TaskResult:
    task_id: str
    status: TaskStatus
    image_url: Optional[str] = None
    video_url: Optional[str] = None
    error: Optional[str] = None
    metadata: Optional[Dict[str, Any]] = None

@dataclass
class ProgressUpdate:
    task_id: str
    progress: int
    message: Optional[str] = None
    estimated_seconds_remaining: Optional[int] = None

class ConduitRealtimeClient:
    """Production-ready Python client for Conduit real-time APIs"""
    
    def __init__(
        self,
        virtual_key: str,
        base_url: str = "https://api.conduit.im",
        max_reconnect_attempts: int = 5,
        enable_logging: bool = False
    ):
        self.virtual_key = virtual_key
        self.base_url = base_url
        self.ws_base_url = base_url.replace("https://", "wss://").replace("http://", "ws://")
        self.max_reconnect_attempts = max_reconnect_attempts
        
        self._websocket: Optional[websockets.WebSocketClientProtocol] = None
        self._session: Optional[aiohttp.ClientSession] = None
        self._active_tasks: Set[str] = set()
        self._is_connected = False
        self._reconnect_attempts = 0
        self._tasks: Dict[str, asyncio.Task] = {}
        
        # Event handlers
        self._handlers: Dict[str, list[Callable]] = {
            'connected': [],
            'disconnected': [],
            'reconnecting': [],
            'reconnected': [],
            'progress': [],
            'completed': [],
            'failed': [],
            'error': []
        }
        
        if enable_logging:
            logging.basicConfig(level=logging.INFO)
    
    async def __aenter__(self):
        self._session = aiohttp.ClientSession()
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        await self.disconnect()
        if self._session:
            await self._session.close()
    
    def on(self, event: str, handler: Callable):
        """Register an event handler"""
        if event in self._handlers:
            self._handlers[event].append(handler)
    
    def off(self, event: str, handler: Callable):
        """Remove an event handler"""
        if event in self._handlers and handler in self._handlers[event]:
            self._handlers[event].remove(handler)
    
    async def _emit(self, event: str, *args, **kwargs):
        """Emit an event to all registered handlers"""
        for handler in self._handlers.get(event, []):
            try:
                if asyncio.iscoroutinefunction(handler):
                    await handler(*args, **kwargs)
                else:
                    handler(*args, **kwargs)
            except Exception as e:
                logger.error(f"Error in {event} handler: {e}")
    
    async def connect(self, hub_type: 'image' | 'video'):
        """Connect to SignalR hub using WebSocket"""
        hub_url = f"{self.ws_base_url}/hubs/{hub_type}-generation"
        
        # SignalR expects specific headers and query parameters
        headers = {
            "Authorization": f"Bearer {self.virtual_key}",
            "User-Agent": "ConduitPythonClient/1.0"
        }
        
        self._reconnect_attempts = 0
        await self._connect_with_retry(hub_url, headers)
    
    async def _connect_with_retry(self, url: str, headers: Dict[str, str]):
        """Connect with exponential backoff retry"""
        while self._reconnect_attempts < self.max_reconnect_attempts:
            try:
                logger.info(f"Connecting to {url} (attempt {self._reconnect_attempts + 1})")
                
                # SignalR handshake
                negotiate_url = url.replace("/hubs/", "/hubs/negotiate/")
                async with self._session.post(
                    negotiate_url,
                    headers=headers
                ) as response:
                    if response.status != 200:
                        raise Exception(f"Negotiate failed: {response.status}")
                    negotiate_data = await response.json()
                
                # Connect WebSocket
                ws_url = f"{url}?id={negotiate_data.get('connectionId', '')}"
                self._websocket = await websockets.connect(
                    ws_url,
                    extra_headers=headers,
                    ping_interval=30,
                    ping_timeout=10
                )
                
                self._is_connected = True
                self._reconnect_attempts = 0
                await self._emit('connected', hub_type)
                
                # Start message handler
                self._tasks['message_handler'] = asyncio.create_task(
                    self._handle_messages()
                )
                
                # Resubscribe to active tasks
                await self._resubscribe_to_active_tasks()
                
                break
                
            except Exception as e:
                logger.error(f"Connection failed: {e}")
                self._reconnect_attempts += 1
                
                if self._reconnect_attempts >= self.max_reconnect_attempts:
                    await self._emit('error', 'connection', e)
                    raise
                
                # Exponential backoff with jitter
                delay = min(2 ** self._reconnect_attempts, 30) + asyncio.get_event_loop().time() % 1
                await self._emit('reconnecting', e)
                await asyncio.sleep(delay)
    
    async def _handle_messages(self):
        """Handle incoming WebSocket messages"""
        try:
            async for message in self._websocket:
                await self._process_message(message)
        except WebSocketException as e:
            logger.error(f"WebSocket error: {e}")
            await self._handle_disconnect(e)
    
    async def _process_message(self, message: str):
        """Process a SignalR message"""
        try:
            data = json.loads(message)
            
            # SignalR message format
            if data.get('type') == 1:  # Invocation
                await self._handle_invocation(data)
            elif data.get('type') == 6:  # Ping
                await self._send_pong()
            
        except json.JSONDecodeError:
            logger.error(f"Invalid message format: {message}")
    
    async def _handle_invocation(self, data: Dict[str, Any]):
        """Handle SignalR method invocation"""
        target = data.get('target')
        arguments = data.get('arguments', [])
        
        if target == 'TaskProgress' or target == 'RequestProgress':
            task_id, progress = arguments[0], arguments[1]
            update = ProgressUpdate(task_id=task_id, progress=progress)
            await self._emit('progress', update)
            
        elif target == 'TaskCompleted' or target == 'RequestCompleted':
            task_id, result = arguments[0], arguments[1]
            task_result = TaskResult(
                task_id=task_id,
                status=TaskStatus.COMPLETED,
                image_url=result.get('image_url'),
                video_url=result.get('video_url'),
                metadata=result.get('metadata')
            )
            await self._emit('completed', task_result)
            self._active_tasks.discard(task_id)
            
        elif target == 'TaskFailed' or target == 'RequestFailed':
            task_id, error = arguments[0], arguments[1]
            task_result = TaskResult(
                task_id=task_id,
                status=TaskStatus.FAILED,
                error=error
            )
            await self._emit('failed', task_result)
            self._active_tasks.discard(task_id)
    
    async def _send_pong(self):
        """Send pong response"""
        pong = {"type": 6}
        await self._websocket.send(json.dumps(pong))
    
    async def _handle_disconnect(self, error: Optional[Exception] = None):
        """Handle disconnection and attempt reconnection"""
        self._is_connected = False
        await self._emit('disconnected', error)
        
        if self._reconnect_attempts < self.max_reconnect_attempts:
            await asyncio.sleep(5)
            await self._connect_with_retry(
                self._websocket.host,
                dict(self._websocket.request_headers)
            )
    
    async def subscribe_to_task(self, task_id: str, task_type: 'image' | 'video'):
        """Subscribe to task updates"""
        if not self._is_connected:
            raise Exception("Not connected to hub")
        
        method = 'SubscribeToTask' if task_type == 'image' else 'SubscribeToRequest'
        
        # Send SignalR invocation
        message = {
            "type": 1,  # Invocation
            "invocationId": str(asyncio.get_event_loop().time()),
            "target": method,
            "arguments": [task_id],
            "streamIds": []
        }
        
        await self._websocket.send(json.dumps(message))
        self._active_tasks.add(task_id)
        logger.info(f"Subscribed to {task_id}")
    
    async def unsubscribe_from_task(self, task_id: str):
        """Unsubscribe from task updates"""
        if not self._is_connected or task_id not in self._active_tasks:
            return
        
        # Determine method based on current subscriptions
        # In production, you'd track the task type
        method = 'UnsubscribeFromTask'
        
        message = {
            "type": 1,
            "invocationId": str(asyncio.get_event_loop().time()),
            "target": method,
            "arguments": [task_id],
            "streamIds": []
        }
        
        await self._websocket.send(json.dumps(message))
        self._active_tasks.discard(task_id)
    
    async def _resubscribe_to_active_tasks(self):
        """Resubscribe to all active tasks after reconnection"""
        for task_id in list(self._active_tasks):
            try:
                # In production, track task types
                await self.subscribe_to_task(task_id, 'image')
            except Exception as e:
                logger.error(f"Failed to resubscribe to {task_id}: {e}")
                self._active_tasks.discard(task_id)
    
    async def generate_image(
        self,
        model: str,
        prompt: str,
        size: Optional[str] = None,
        n: Optional[int] = None
    ) -> str:
        """Generate an image and return task ID"""
        if not self._session:
            raise Exception("Client not initialized. Use async context manager.")
        
        payload = {
            "model": model,
            "prompt": prompt
        }
        if size:
            payload["size"] = size
        if n:
            payload["n"] = n
        
        async with self._session.post(
            f"{self.base_url}/v1/images/generations/async",
            headers={
                "Authorization": f"Bearer {self.virtual_key}",
                "Content-Type": "application/json"
            },
            json=payload
        ) as response:
            if response.status != 200:
                error = await response.json()
                raise Exception(error.get('error', {}).get('message', 'Image generation failed'))
            
            result = await response.json()
            task_id = result['task_id']
            
            # Auto-subscribe if connected
            if self._is_connected:
                await self.subscribe_to_task(task_id, 'image')
            
            return task_id
    
    async def generate_video(
        self,
        model: str,
        prompt: str,
        duration: Optional[int] = None,
        size: Optional[str] = None
    ) -> str:
        """Generate a video and return request ID"""
        if not self._session:
            raise Exception("Client not initialized. Use async context manager.")
        
        payload = {
            "model": model,
            "prompt": prompt
        }
        if duration:
            payload["duration"] = duration
        if size:
            payload["size"] = size
        
        async with self._session.post(
            f"{self.base_url}/v1/videos/generations/async",
            headers={
                "Authorization": f"Bearer {self.virtual_key}",
                "Content-Type": "application/json"
            },
            json=payload
        ) as response:
            if response.status != 200:
                error = await response.json()
                raise Exception(error.get('error', {}).get('message', 'Video generation failed'))
            
            result = await response.json()
            request_id = result['request_id']
            
            # Auto-subscribe if connected
            if self._is_connected:
                await self.subscribe_to_task(request_id, 'video')
            
            return request_id
    
    async def disconnect(self):
        """Disconnect and clean up resources"""
        # Cancel all tasks
        for task in self._tasks.values():
            task.cancel()
        
        # Unsubscribe from all tasks
        for task_id in list(self._active_tasks):
            await self.unsubscribe_from_task(task_id)
        
        # Close WebSocket
        if self._websocket:
            await self._websocket.close()
            self._websocket = None
        
        self._is_connected = False

# Example usage
async def main():
    async with ConduitRealtimeClient(
        virtual_key="condt_your_virtual_key",
        enable_logging=True
    ) as client:
        
        # Set up event handlers
        def on_progress(update: ProgressUpdate):
            print(f"Progress: {update.task_id} - {update.progress}%")
        
        def on_completed(result: TaskResult):
            print(f"Completed: {result.task_id}")
            if result.image_url:
                print(f"Image URL: {result.image_url}")
        
        def on_failed(result: TaskResult):
            print(f"Failed: {result.task_id} - {result.error}")
        
        client.on('progress', on_progress)
        client.on('completed', on_completed)
        client.on('failed', on_failed)
        
        # Connect to image generation hub
        await client.connect('image')
        
        # Generate an image
        task_id = await client.generate_image(
            model='dall-e-3',
            prompt='A serene mountain landscape at sunset'
        )
        
        print(f"Started image generation: {task_id}")
        
        # Wait for completion
        completed = asyncio.Event()
        
        def on_task_completed(result: TaskResult):
            if result.task_id == task_id:
                completed.set()
        
        client.on('completed', on_task_completed)
        client.on('failed', lambda r: completed.set() if r.task_id == task_id else None)
        
        await completed.wait()

if __name__ == "__main__":
    asyncio.run(main())
```

### Django Integration Example

```python
# conduit_django_client.py
import asyncio
from django.conf import settings
from django.core.cache import cache
from channels.generic.websocket import AsyncWebsocketConsumer
import json

class ConduitWebSocketBridge(AsyncWebsocketConsumer):
    """
    Django Channels consumer that bridges Conduit real-time updates to web clients
    """
    
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.conduit_client = None
        self.user_tasks = set()
    
    async def connect(self):
        # Accept WebSocket connection
        await self.accept()
        
        # Get user's virtual key (from session, database, etc.)
        virtual_key = await self.get_user_virtual_key()
        if not virtual_key:
            await self.close(code=4001)
            return
        
        # Initialize Conduit client
        self.conduit_client = ConduitRealtimeClient(
            virtual_key=virtual_key,
            base_url=settings.CONDUIT_API_URL
        )
        
        # Set up event forwarding
        self.conduit_client.on('progress', self.forward_progress)
        self.conduit_client.on('completed', self.forward_completed)
        self.conduit_client.on('failed', self.forward_failed)
        
        # Connect to Conduit
        try:
            await self.conduit_client.connect('image')
            await self.send(json.dumps({
                'type': 'connection_established',
                'status': 'connected'
            }))
        except Exception as e:
            await self.send(json.dumps({
                'type': 'connection_error',
                'error': str(e)
            }))
            await self.close(code=4002)
    
    async def disconnect(self, close_code):
        if self.conduit_client:
            await self.conduit_client.disconnect()
    
    async def receive(self, text_data):
        """Handle messages from web client"""
        try:
            data = json.loads(text_data)
            message_type = data.get('type')
            
            if message_type == 'generate_image':
                await self.handle_generate_image(data)
            elif message_type == 'subscribe':
                await self.handle_subscribe(data)
            elif message_type == 'unsubscribe':
                await self.handle_unsubscribe(data)
        
        except json.JSONDecodeError:
            await self.send_error('Invalid message format')
        except Exception as e:
            await self.send_error(str(e))
    
    async def handle_generate_image(self, data):
        """Handle image generation request"""
        try:
            task_id = await self.conduit_client.generate_image(
                model=data.get('model', 'dall-e-3'),
                prompt=data['prompt'],
                size=data.get('size')
            )
            
            self.user_tasks.add(task_id)
            
            # Cache task ownership for authorization
            cache.set(
                f"task_owner:{task_id}",
                self.scope['user'].id,
                timeout=3600  # 1 hour
            )
            
            await self.send(json.dumps({
                'type': 'generation_started',
                'task_id': task_id
            }))
            
        except Exception as e:
            await self.send_error(f"Generation failed: {str(e)}")
    
    async def handle_subscribe(self, data):
        """Subscribe to existing task"""
        task_id = data.get('task_id')
        if not task_id:
            await self.send_error('Task ID required')
            return
        
        # Verify ownership
        owner_id = cache.get(f"task_owner:{task_id}")
        if owner_id != self.scope['user'].id:
            await self.send_error('Unauthorized')
            return
        
        await self.conduit_client.subscribe_to_task(task_id, 'image')
        self.user_tasks.add(task_id)
    
    async def handle_unsubscribe(self, data):
        """Unsubscribe from task"""
        task_id = data.get('task_id')
        if task_id in self.user_tasks:
            await self.conduit_client.unsubscribe_from_task(task_id)
            self.user_tasks.discard(task_id)
    
    async def forward_progress(self, update: ProgressUpdate):
        """Forward progress updates to web client"""
        if update.task_id in self.user_tasks:
            await self.send(json.dumps({
                'type': 'progress',
                'task_id': update.task_id,
                'progress': update.progress,
                'message': update.message
            }))
    
    async def forward_completed(self, result: TaskResult):
        """Forward completion to web client"""
        if result.task_id in self.user_tasks:
            await self.send(json.dumps({
                'type': 'completed',
                'task_id': result.task_id,
                'image_url': result.image_url,
                'metadata': result.metadata
            }))
            self.user_tasks.discard(result.task_id)
    
    async def forward_failed(self, result: TaskResult):
        """Forward failure to web client"""
        if result.task_id in self.user_tasks:
            await self.send(json.dumps({
                'type': 'failed',
                'task_id': result.task_id,
                'error': result.error
            }))
            self.user_tasks.discard(result.task_id)
    
    async def send_error(self, error: str):
        """Send error message to client"""
        await self.send(json.dumps({
            'type': 'error',
            'error': error
        }))
    
    async def get_user_virtual_key(self):
        """Get virtual key for authenticated user"""
        # Implementation depends on your authentication system
        user = self.scope.get('user')
        if user and user.is_authenticated:
            # Example: Get from user profile or settings
            return user.profile.conduit_virtual_key
        return None

# Django routing.py
from django.urls import re_path
from . import consumers

websocket_urlpatterns = [
    re_path(r'ws/conduit/$', consumers.ConduitWebSocketBridge.as_asgi()),
]
```

## C#/.NET Client

### Complete Implementation

```csharp
// ConduitRealtimeClient.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace Conduit.Realtime
{
    public class ConduitRealtimeClient : IAsyncDisposable
    {
        private readonly string _virtualKey;
        private readonly string _baseUrl;
        private readonly ILogger<ConduitRealtimeClient> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, TaskInfo> _activeTasks;
        private HubConnection? _hubConnection;
        private readonly SemaphoreSlim _connectionLock;
        private CancellationTokenSource? _reconnectCts;
        
        public event Action<string>? Connected;
        public event Action<Exception?>? Disconnected;
        public event Action<Exception?>? Reconnecting;
        public event Action<string>? Reconnected;
        public event Action<ProgressUpdate>? Progress;
        public event Action<TaskResult>? Completed;
        public event Action<TaskResult>? Failed;
        public event Action<string, Exception>? Error;
        
        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        
        public ConduitRealtimeClient(
            string virtualKey,
            string baseUrl = "https://api.conduit.im",
            ILogger<ConduitRealtimeClient>? logger = null)
        {
            _virtualKey = virtualKey;
            _baseUrl = baseUrl;
            _logger = logger ?? new ConsoleLogger();
            _activeTasks = new ConcurrentDictionary<string, TaskInfo>();
            _connectionLock = new SemaphoreSlim(1, 1);
            
            // Configure HTTP client with retry policy
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} after {timespan}s");
                    });
            
            _httpClient = new HttpClient(new PolicyHttpMessageHandler(retryPolicy)
            {
                InnerHandler = new HttpClientHandler()
            })
            {
                BaseAddress = new Uri(_baseUrl)
            };
            
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _virtualKey);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ConduitDotNetClient/1.0");
        }
        
        public async Task ConnectAsync(
            HubType hubType,
            CancellationToken cancellationToken = default)
        {
            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (_hubConnection != null)
                {
                    await _hubConnection.DisposeAsync();
                }
                
                var hubUrl = $"{_baseUrl}/hubs/{hubType.ToString().ToLower()}-generation";
                
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl, options =>
                    {
                        options.Headers.Add("Authorization", $"Bearer {_virtualKey}");
                        options.AccessTokenProvider = () => Task.FromResult<string?>(_virtualKey);
                    })
                    .WithAutomaticReconnect(new ExponentialBackoffReconnectPolicy())
                    .ConfigureLogging(logging =>
                    {
                        if (_logger is ILoggerProvider provider)
                        {
                            logging.AddProvider(provider);
                        }
                    })
                    .Build();
                
                RegisterEventHandlers();
                
                await _hubConnection.StartAsync(cancellationToken);
                _logger.LogInformation($"Connected to {hubType} hub");
                Connected?.Invoke(hubType.ToString());
            }
            finally
            {
                _connectionLock.Release();
            }
        }
        
        private void RegisterEventHandlers()
        {
            if (_hubConnection == null) return;
            
            // Task events
            _hubConnection.On<string, int>("TaskProgress", (taskId, progress) =>
            {
                _logger.LogDebug($"Task {taskId}: {progress}%");
                Progress?.Invoke(new ProgressUpdate { TaskId = taskId, Progress = progress });
            });
            
            _hubConnection.On<string, object>("TaskCompleted", (taskId, result) =>
            {
                _logger.LogInformation($"Task {taskId} completed");
                var taskResult = ParseTaskResult(taskId, "completed", result);
                Completed?.Invoke(taskResult);
                _activeTasks.TryRemove(taskId, out _);
            });
            
            _hubConnection.On<string, string>("TaskFailed", (taskId, error) =>
            {
                _logger.LogError($"Task {taskId} failed: {error}");
                Failed?.Invoke(new TaskResult 
                { 
                    TaskId = taskId, 
                    Status = TaskStatus.Failed, 
                    Error = error 
                });
                _activeTasks.TryRemove(taskId, out _);
            });
            
            // Request events (for video)
            _hubConnection.On<string, int>("RequestProgress", (requestId, progress) =>
            {
                Progress?.Invoke(new ProgressUpdate { TaskId = requestId, Progress = progress });
            });
            
            _hubConnection.On<string, object>("RequestCompleted", (requestId, result) =>
            {
                var taskResult = ParseTaskResult(requestId, "completed", result);
                Completed?.Invoke(taskResult);
                _activeTasks.TryRemove(requestId, out _);
            });
            
            _hubConnection.On<string, string>("RequestFailed", (requestId, error) =>
            {
                Failed?.Invoke(new TaskResult 
                { 
                    TaskId = requestId, 
                    Status = TaskStatus.Failed, 
                    Error = error 
                });
                _activeTasks.TryRemove(requestId, out _);
            });
            
            // Connection events
            _hubConnection.Reconnecting += (error) =>
            {
                _logger.LogWarning($"Reconnecting: {error?.Message}");
                Reconnecting?.Invoke(error);
                return Task.CompletedTask;
            };
            
            _hubConnection.Reconnected += async (connectionId) =>
            {
                _logger.LogInformation($"Reconnected: {connectionId}");
                Reconnected?.Invoke(connectionId ?? "");
                await ResubscribeToActiveTasksAsync();
            };
            
            _hubConnection.Closed += async (error) =>
            {
                _logger.LogError($"Connection closed: {error?.Message}");
                Disconnected?.Invoke(error);
                
                if (error != null && !_reconnectCts?.IsCancellationRequested == true)
                {
                    // Manual reconnection attempt
                    await Task.Delay(5000);
                    try
                    {
                        await _hubConnection.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Manual reconnection failed");
                    }
                }
            };
        }
        
        public async Task<string> GenerateImageAsync(
            ImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/v1/images/generations/async",
                request,
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<GenerationResponse>(
                cancellationToken: cancellationToken);
            
            if (result?.TaskId == null)
            {
                throw new InvalidOperationException("Invalid response from server");
            }
            
            // Auto-subscribe if connected
            if (IsConnected)
            {
                await SubscribeToTaskAsync(result.TaskId, HubType.Image, cancellationToken);
            }
            
            return result.TaskId;
        }
        
        public async Task<string> GenerateVideoAsync(
            VideoGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/v1/videos/generations/async",
                request,
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<GenerationResponse>(
                cancellationToken: cancellationToken);
            
            if (result?.RequestId == null)
            {
                throw new InvalidOperationException("Invalid response from server");
            }
            
            // Auto-subscribe if connected
            if (IsConnected)
            {
                await SubscribeToTaskAsync(result.RequestId, HubType.Video, cancellationToken);
            }
            
            return result.RequestId;
        }
        
        public async Task SubscribeToTaskAsync(
            string taskId,
            HubType hubType,
            CancellationToken cancellationToken = default)
        {
            if (_hubConnection?.State != HubConnectionState.Connected)
            {
                throw new InvalidOperationException("Not connected to hub");
            }
            
            var methodName = hubType == HubType.Image ? "SubscribeToTask" : "SubscribeToRequest";
            
            try
            {
                await _hubConnection.InvokeAsync(methodName, taskId, cancellationToken);
                _activeTasks[taskId] = new TaskInfo { TaskId = taskId, HubType = hubType };
                _logger.LogInformation($"Subscribed to {taskId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to subscribe to {taskId}");
                Error?.Invoke(taskId, ex);
                throw;
            }
        }
        
        public async Task UnsubscribeFromTaskAsync(
            string taskId,
            CancellationToken cancellationToken = default)
        {
            if (_hubConnection?.State != HubConnectionState.Connected)
            {
                return;
            }
            
            if (!_activeTasks.TryGetValue(taskId, out var taskInfo))
            {
                return;
            }
            
            var methodName = taskInfo.HubType == HubType.Image 
                ? "UnsubscribeFromTask" 
                : "UnsubscribeFromRequest";
            
            try
            {
                await _hubConnection.InvokeAsync(methodName, taskId, cancellationToken);
                _activeTasks.TryRemove(taskId, out _);
                _logger.LogInformation($"Unsubscribed from {taskId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to unsubscribe from {taskId}");
            }
        }
        
        private async Task ResubscribeToActiveTasksAsync()
        {
            var tasks = _activeTasks.ToList();
            
            foreach (var (taskId, taskInfo) in tasks)
            {
                try
                {
                    await SubscribeToTaskAsync(taskId, taskInfo.HubType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to resubscribe to {taskId}");
                    _activeTasks.TryRemove(taskId, out _);
                }
            }
        }
        
        private TaskResult ParseTaskResult(string taskId, string status, object result)
        {
            var json = JsonSerializer.Serialize(result);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            
            return new TaskResult
            {
                TaskId = taskId,
                Status = Enum.Parse<TaskStatus>(status, true),
                ImageUrl = dict?.GetValueOrDefault("image_url")?.ToString(),
                VideoUrl = dict?.GetValueOrDefault("video_url")?.ToString(),
                Metadata = dict
            };
        }
        
        public async ValueTask DisposeAsync()
        {
            _reconnectCts?.Cancel();
            
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
            
            _httpClient.Dispose();
            _connectionLock.Dispose();
        }
        
        // Helper classes
        private class TaskInfo
        {
            public string TaskId { get; set; } = "";
            public HubType HubType { get; set; }
        }
        
        private class ExponentialBackoffReconnectPolicy : IRetryPolicy
        {
            private readonly Random _random = new Random();
            
            public TimeSpan? NextRetryDelay(RetryContext retryContext)
            {
                if (retryContext.PreviousRetryCount >= 5)
                {
                    return null; // Stop after 5 attempts
                }
                
                var baseDelay = Math.Min(Math.Pow(2, retryContext.PreviousRetryCount), 30);
                var jitter = _random.NextDouble();
                
                return TimeSpan.FromSeconds(baseDelay + jitter);
            }
        }
        
        private class ConsoleLogger : ILogger<ConduitRealtimeClient>
        {
            public IDisposable BeginScope<TState>(TState state) => null!;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            }
        }
    }
    
    // Supporting types
    public enum HubType
    {
        Image,
        Video
    }
    
    public enum TaskStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Cancelled
    }
    
    public class TaskResult
    {
        public string TaskId { get; set; } = "";
        public TaskStatus Status { get; set; }
        public string? ImageUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
    
    public class ProgressUpdate
    {
        public string TaskId { get; set; } = "";
        public int Progress { get; set; }
        public string? Message { get; set; }
        public int? EstimatedSecondsRemaining { get; set; }
    }
    
    public class ImageGenerationRequest
    {
        public string Model { get; set; } = "dall-e-3";
        public string Prompt { get; set; } = "";
        public string? Size { get; set; }
        public int? N { get; set; }
    }
    
    public class VideoGenerationRequest
    {
        public string Model { get; set; } = "minimax-video";
        public string Prompt { get; set; } = "";
        public int? Duration { get; set; }
        public string? Size { get; set; }
    }
    
    public class GenerationResponse
    {
        public string? TaskId { get; set; }
        public string? RequestId { get; set; }
        public string? Status { get; set; }
    }
}

// Usage example
public class Program
{
    static async Task Main(string[] args)
    {
        await using var client = new ConduitRealtimeClient(
            virtualKey: "condt_your_virtual_key",
            logger: LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<ConduitRealtimeClient>());
        
        // Set up event handlers
        client.Progress += (update) =>
        {
            Console.WriteLine($"Progress: {update.TaskId} - {update.Progress}%");
        };
        
        client.Completed += (result) =>
        {
            Console.WriteLine($"Completed: {result.TaskId}");
            if (result.ImageUrl != null)
                Console.WriteLine($"Image: {result.ImageUrl}");
        };
        
        client.Failed += (result) =>
        {
            Console.WriteLine($"Failed: {result.TaskId} - {result.Error}");
        };
        
        // Connect to image hub
        await client.ConnectAsync(HubType.Image);
        
        // Generate an image
        var taskId = await client.GenerateImageAsync(new ImageGenerationRequest
        {
            Model = "dall-e-3",
            Prompt = "A beautiful sunset over mountains",
            Size = "1024x1024"
        });
        
        Console.WriteLine($"Started generation: {taskId}");
        
        // Wait for completion
        var tcs = new TaskCompletionSource<bool>();
        
        void OnCompleted(TaskResult result)
        {
            if (result.TaskId == taskId)
            {
                tcs.SetResult(true);
            }
        }
        
        void OnFailed(TaskResult result)
        {
            if (result.TaskId == taskId)
            {
                tcs.SetResult(false);
            }
        }
        
        client.Completed += OnCompleted;
        client.Failed += OnFailed;
        
        var success = await tcs.Task;
        
        client.Completed -= OnCompleted;
        client.Failed -= OnFailed;
        
        Console.WriteLine(success ? "Generation succeeded!" : "Generation failed!");
    }
}
```

### ASP.NET Core Integration

```csharp
// Startup.cs or Program.cs
builder.Services.AddSingleton<ConduitRealtimeClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<ConduitRealtimeClient>>();
    
    return new ConduitRealtimeClient(
        virtualKey: configuration["Conduit:VirtualKey"],
        baseUrl: configuration["Conduit:ApiUrl"] ?? "https://api.conduit.im",
        logger: logger);
});

builder.Services.AddHostedService<ConduitConnectionService>();

// ConduitConnectionService.cs
public class ConduitConnectionService : BackgroundService
{
    private readonly ConduitRealtimeClient _client;
    private readonly ILogger<ConduitConnectionService> _logger;
    
    public ConduitConnectionService(
        ConduitRealtimeClient client,
        ILogger<ConduitConnectionService> logger)
    {
        _client = client;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _client.ConnectAsync(HubType.Image, stoppingToken);
            
            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to maintain Conduit connection");
            throw;
        }
    }
}

// Controller example
[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly ConduitRealtimeClient _conduitClient;
    
    public ImageController(ConduitRealtimeClient conduitClient)
    {
        _conduitClient = conduitClient;
    }
    
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateImage([FromBody] GenerateImageDto request)
    {
        try
        {
            var taskId = await _conduitClient.GenerateImageAsync(
                new ImageGenerationRequest
                {
                    Model = request.Model ?? "dall-e-3",
                    Prompt = request.Prompt,
                    Size = request.Size
                });
            
            return Ok(new { taskId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

## Common Patterns

### Rate Limiting and Throttling

```javascript
// JavaScript rate limiter
class RateLimiter {
    constructor(maxRequests, windowMs) {
        this.maxRequests = maxRequests;
        this.windowMs = windowMs;
        this.requests = [];
    }
    
    async acquire() {
        const now = Date.now();
        
        // Remove old requests outside window
        this.requests = this.requests.filter(
            time => now - time < this.windowMs
        );
        
        if (this.requests.length >= this.maxRequests) {
            // Calculate wait time
            const oldestRequest = this.requests[0];
            const waitTime = this.windowMs - (now - oldestRequest);
            
            await new Promise(resolve => setTimeout(resolve, waitTime));
            return this.acquire();
        }
        
        this.requests.push(now);
    }
}

// Usage
const limiter = new RateLimiter(10, 60000); // 10 requests per minute

async function rateLimitedGeneration() {
    await limiter.acquire();
    return client.generateImage({ /* ... */ });
}
```

### Connection Pool Management

```python
# Python connection pool
class ConduitConnectionPool:
    def __init__(self, virtual_key: str, pool_size: int = 5):
        self.virtual_key = virtual_key
        self.pool_size = pool_size
        self._connections: List[ConduitRealtimeClient] = []
        self._available = asyncio.Queue(maxsize=pool_size)
        self._lock = asyncio.Lock()
    
    async def initialize(self):
        """Initialize the connection pool"""
        async with self._lock:
            for i in range(self.pool_size):
                client = ConduitRealtimeClient(self.virtual_key)
                await client.connect('image')
                self._connections.append(client)
                await self._available.put(client)
    
    async def acquire(self) -> ConduitRealtimeClient:
        """Acquire a connection from the pool"""
        return await self._available.get()
    
    async def release(self, client: ConduitRealtimeClient):
        """Return a connection to the pool"""
        await self._available.put(client)
    
    @asynccontextmanager
    async def connection(self):
        """Context manager for connection usage"""
        client = await self.acquire()
        try:
            yield client
        finally:
            await self.release(client)
    
    async def close(self):
        """Close all connections"""
        async with self._lock:
            for client in self._connections:
                await client.disconnect()
            self._connections.clear()

# Usage
pool = ConduitConnectionPool("condt_key", pool_size=3)
await pool.initialize()

async with pool.connection() as client:
    task_id = await client.generate_image(...)
```

### Error Recovery and Resilience

```csharp
// C# resilient client wrapper
public class ResilientConduitClient
{
    private readonly ConduitRealtimeClient _client;
    private readonly IAsyncPolicy<string> _generationPolicy;
    
    public ResilientConduitClient(ConduitRealtimeClient client)
    {
        _client = client;
        
        // Configure Polly policies
        _generationPolicy = Policy<string>
            .HandleResult(string.IsNullOrEmpty)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<InvalidOperationException>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var taskId = context.Values.GetValueOrDefault("taskId", "unknown");
                    Console.WriteLine($"Retry {retryCount} for task {taskId} after {timespan}");
                });
    }
    
    public async Task<string> GenerateImageWithRetryAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _generationPolicy.ExecuteAsync(
            async (context, ct) =>
            {
                // Ensure connected
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(HubType.Image, ct);
                }
                
                var taskId = await _client.GenerateImageAsync(request, ct);
                context["taskId"] = taskId;
                return taskId;
            },
            new Context(),
            cancellationToken);
    }
}
```

## Testing and Debugging

### Mock Client for Testing

```typescript
// TypeScript mock client
export class MockConduitClient extends EventEmitter {
    private mockDelay = 1000;
    private shouldFail = false;
    
    async connect(): Promise<void> {
        await this.delay(100);
        this.emit('connected', 'image');
    }
    
    async generateImage(params: any): Promise<string> {
        const taskId = `mock-${Date.now()}`;
        
        // Simulate async generation
        setTimeout(async () => {
            if (this.shouldFail) {
                this.emit('failed', {
                    taskId,
                    status: 'failed',
                    error: 'Mock failure'
                });
            } else {
                // Progress updates
                for (let i = 0; i <= 100; i += 20) {
                    await this.delay(this.mockDelay / 5);
                    this.emit('progress', { taskId, progress: i });
                }
                
                this.emit('completed', {
                    taskId,
                    status: 'completed',
                    imageUrl: 'https://example.com/mock-image.jpg'
                });
            }
        }, 100);
        
        return taskId;
    }
    
    setMockDelay(ms: number) {
        this.mockDelay = ms;
    }
    
    setShouldFail(fail: boolean) {
        this.shouldFail = fail;
    }
    
    private delay(ms: number): Promise<void> {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
}
```