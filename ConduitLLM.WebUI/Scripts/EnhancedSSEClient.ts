// Enhanced SSE Client for handling multiple event types in streaming responses

export interface StreamingMetrics {
    requestId: string;
    elapsedMs: number;
    tokensGenerated: number;
    currentTokensPerSecond: number;
    timeToFirstTokenMs?: number;
    avgInterTokenLatencyMs?: number;
}

export interface PerformanceMetrics {
    totalLatencyMs: number;
    timeToFirstTokenMs?: number;
    tokensPerSecond?: number;
    promptTokensPerSecond?: number;
    completionTokensPerSecond?: number;
    provider: string;
    model: string;
    streaming: boolean;
    avgInterTokenLatencyMs?: number;
}

export interface ChatCompletionChunk {
    id?: string;
    choices: Array<{
        delta?: {
            content?: string;
            role?: string;
        };
        finish_reason?: string;
        index: number;
    }>;
    created?: number;
    model?: string;
}

export interface EnhancedSSEHandlers {
    onContent?: (chunk: ChatCompletionChunk) => void;
    onMetrics?: (metrics: StreamingMetrics) => void;
    onFinalMetrics?: (metrics: PerformanceMetrics) => void;
    onError?: (error: string) => void;
    onDone?: () => void;
}

export class EnhancedSSEClient {
    private eventSource: EventSource | null = null;
    private handlers: EnhancedSSEHandlers = {};
    private requestId: string | null = null;

    constructor(private url: string, private options?: RequestInit) {}

    /**
     * Starts the SSE connection and begins receiving events.
     */
    async connect(handlers: EnhancedSSEHandlers): Promise<void> {
        this.handlers = handlers;

        // For POST requests, we need to use fetch + ReadableStream
        if (this.options?.method === 'POST') {
            return this.connectWithFetch();
        }

        // For GET requests, use EventSource
        this.eventSource = new EventSource(this.url);
        this.setupEventListeners();
    }

    /**
     * Connects using fetch API for POST requests with streaming.
     */
    private async connectWithFetch(): Promise<void> {
        try {
            const response = await fetch(this.url, {
                ...this.options,
                headers: {
                    ...this.options?.headers,
                    'Accept': 'text/event-stream',
                },
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            // Extract request ID from headers
            this.requestId = response.headers.get('X-Request-ID');

            const reader = response.body?.getReader();
            if (!reader) {
                throw new Error('Response body is not readable');
            }

            const decoder = new TextDecoder();
            let buffer = '';

            while (true) {
                const { done, value } = await reader.read();
                if (done) break;

                buffer += decoder.decode(value, { stream: true });
                const lines = buffer.split('\n');
                
                // Keep the last incomplete line in the buffer
                buffer = lines.pop() || '';

                for (const line of lines) {
                    this.processLine(line);
                }
            }

            // Process any remaining buffer
            if (buffer.trim()) {
                this.processLine(buffer);
            }

            this.handlers.onDone?.();
        } catch (error) {
            this.handlers.onError?.((error as Error).message);
        }
    }

    /**
     * Sets up event listeners for EventSource.
     */
    private setupEventListeners(): void {
        if (!this.eventSource) return;

        this.eventSource.addEventListener('content', (event) => {
            try {
                const data = JSON.parse(event.data);
                this.handlers.onContent?.(data);
            } catch (error) {
                console.error('Error parsing content event:', error);
            }
        });

        this.eventSource.addEventListener('metrics', (event) => {
            try {
                const data = JSON.parse(event.data);
                this.handlers.onMetrics?.(data);
            } catch (error) {
                console.error('Error parsing metrics event:', error);
            }
        });

        this.eventSource.addEventListener('metrics-final', (event) => {
            try {
                const data = JSON.parse(event.data);
                this.handlers.onFinalMetrics?.(data);
            } catch (error) {
                console.error('Error parsing final metrics event:', error);
            }
        });

        this.eventSource.addEventListener('error', (event) => {
            try {
                const data = JSON.parse(event.data);
                this.handlers.onError?.(data.error);
            } catch (error) {
                this.handlers.onError?.('Unknown error');
            }
        });

        this.eventSource.addEventListener('done', () => {
            this.handlers.onDone?.();
            this.disconnect();
        });

        this.eventSource.onerror = (error) => {
            this.handlers.onError?.('Connection error');
            this.disconnect();
        };
    }

    /**
     * Processes a single SSE line.
     */
    private currentEvent: { type?: string; data?: string } = {};

    private processLine(line: string): void {
        if (line.startsWith('event: ')) {
            this.currentEvent.type = line.slice(7).trim();
        } else if (line.startsWith('data: ')) {
            this.currentEvent.data = line.slice(6).trim();
        } else if (line.trim() === '' && this.currentEvent.data) {
            // End of event, process it
            this.handleEvent(this.currentEvent.type || 'message', this.currentEvent.data);
            this.currentEvent = {};
        }
    }

    /**
     * Handles a parsed SSE event.
     */
    private handleEvent(type: string, data: string): void {
        try {
            if (data === '[DONE]') {
                this.handlers.onDone?.();
                return;
            }

            const parsedData = JSON.parse(data);

            switch (type) {
                case 'content':
                    this.handlers.onContent?.(parsedData);
                    break;
                case 'metrics':
                    this.handlers.onMetrics?.(parsedData);
                    break;
                case 'metrics-final':
                    this.handlers.onFinalMetrics?.(parsedData);
                    break;
                case 'error':
                    this.handlers.onError?.(parsedData.error || 'Unknown error');
                    break;
                case 'done':
                    this.handlers.onDone?.();
                    break;
            }
        } catch (error) {
            console.error(`Error handling ${type} event:`, error);
        }
    }

    /**
     * Disconnects the SSE connection.
     */
    disconnect(): void {
        if (this.eventSource) {
            this.eventSource.close();
            this.eventSource = null;
        }
    }

    /**
     * Gets the request ID if available.
     */
    getRequestId(): string | null {
        return this.requestId;
    }
}

// Example usage:
/*
const client = new EnhancedSSEClient('/v1/chat/completions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ model: 'gpt-4', messages: [...] })
});

await client.connect({
    onContent: (chunk) => {
        console.log('Content:', chunk.choices[0]?.delta?.content);
    },
    onMetrics: (metrics) => {
        console.log('Metrics Update:', metrics);
    },
    onFinalMetrics: (metrics) => {
        console.log('Final Metrics:', metrics);
    },
    onError: (error) => {
        console.error('Error:', error);
    },
    onDone: () => {
        console.log('Stream completed');
    }
});
*/