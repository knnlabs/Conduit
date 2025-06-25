// SignalR connection for real-time image generation updates
window.imageGenerationSignalR = {
    connection: null,
    isConnected: false,
    reconnectAttempts: 0,
    maxReconnectAttempts: 10,
    reconnectDelay: 5000,
    dotNetReference: null,
    activeTaskIds: new Set(),

    // Initialize SignalR connection
    async initialize(dotNetReference) {
        console.log('Initializing image generation SignalR connection...');
        this.dotNetReference = dotNetReference;
        
        try {
            // Create SignalR connection
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/image-generation")
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: retryContext => {
                        if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
                            return null; // Stop reconnecting
                        }
                        return Math.min(this.reconnectDelay * Math.pow(2, retryContext.previousRetryCount), 30000);
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Set up event handlers
            this.setupEventHandlers();
            
            // Start connection
            await this.start();
            
            return true;
        } catch (error) {
            console.error('Failed to initialize image generation SignalR:', error);
            return false;
        }
    },

    // Set up SignalR event handlers
    setupEventHandlers() {
        // Connection state change handlers
        this.connection.onreconnecting(error => {
            console.log('Image generation SignalR reconnecting...', error);
            this.isConnected = false;
            this.invokeMethod('OnReconnecting');
        });

        this.connection.onreconnected(connectionId => {
            console.log('Image generation SignalR reconnected:', connectionId);
            this.isConnected = true;
            this.reconnectAttempts = 0;
            this.invokeMethod('OnReconnected');
            // Re-subscribe to active tasks
            this.resubscribeToActiveTasks();
        });

        this.connection.onclose(error => {
            console.log('Image generation SignalR connection closed:', error);
            this.isConnected = false;
            this.invokeMethod('OnDisconnected');
            // Attempt manual reconnect if needed
            if (this.reconnectAttempts < this.maxReconnectAttempts) {
                setTimeout(() => this.start(), this.reconnectDelay);
            }
        });

        // Image generation event handlers
        this.connection.on('ImageGenerationStarted', (data) => {
            console.log('Image generation started:', data);
            this.invokeMethod('OnImageGenerationStarted', data);
        });

        this.connection.on('ImageGenerationProgress', (data) => {
            console.log('Image generation progress:', data);
            this.invokeMethod('OnImageGenerationProgress', data);
        });

        this.connection.on('ImageGenerationCompleted', (data) => {
            console.log('Image generation completed:', data);
            this.activeTaskIds.delete(data.taskId || data.requestId);
            this.invokeMethod('OnImageGenerationCompleted', data);
        });

        this.connection.on('ImageGenerationFailed', (data) => {
            console.log('Image generation failed:', data);
            this.activeTaskIds.delete(data.taskId || data.requestId);
            this.invokeMethod('OnImageGenerationFailed', data);
        });

        this.connection.on('ImageGenerationCancelled', (data) => {
            console.log('Image generation cancelled:', data);
            this.activeTaskIds.delete(data.taskId || data.requestId);
            this.invokeMethod('OnImageGenerationCancelled', data);
        });
    },

    // Start SignalR connection
    async start() {
        if (this.connection.state === signalR.HubConnectionState.Disconnected) {
            try {
                await this.connection.start();
                console.log('Image generation SignalR connected');
                this.isConnected = true;
                this.reconnectAttempts = 0;
                this.invokeMethod('OnConnected');
                return true;
            } catch (error) {
                console.error('Failed to start image generation SignalR:', error);
                this.reconnectAttempts++;
                if (this.reconnectAttempts < this.maxReconnectAttempts) {
                    const delay = Math.min(this.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1), 30000);
                    console.log(`Retrying connection in ${delay}ms...`);
                    setTimeout(() => this.start(), delay);
                }
                return false;
            }
        }
        return this.isConnected;
    },

    // Stop SignalR connection
    async stop() {
        if (this.connection && this.connection.state !== signalR.HubConnectionState.Disconnected) {
            try {
                await this.connection.stop();
                console.log('Image generation SignalR disconnected');
                this.isConnected = false;
                this.activeTaskIds.clear();
            } catch (error) {
                console.error('Error stopping image generation SignalR:', error);
            }
        }
    },

    // Subscribe to updates for a specific task
    async subscribeToTask(taskId) {
        if (!this.isConnected) {
            console.warn('Cannot subscribe to task - SignalR not connected');
            return false;
        }

        try {
            await this.connection.invoke('SubscribeToTask', taskId);
            this.activeTaskIds.add(taskId);
            console.log(`Subscribed to image generation updates for task: ${taskId}`);
            return true;
        } catch (error) {
            console.error(`Failed to subscribe to task ${taskId}:`, error);
            return false;
        }
    },

    // Unsubscribe from updates for a specific task
    async unsubscribeFromTask(taskId) {
        if (!this.isConnected) {
            return false;
        }

        try {
            await this.connection.invoke('UnsubscribeFromTask', taskId);
            this.activeTaskIds.delete(taskId);
            console.log(`Unsubscribed from image generation updates for task: ${taskId}`);
            return true;
        } catch (error) {
            console.error(`Failed to unsubscribe from task ${taskId}:`, error);
            return false;
        }
    },

    // Re-subscribe to all active tasks after reconnection
    async resubscribeToActiveTasks() {
        if (this.activeTaskIds.size > 0) {
            console.log(`Re-subscribing to ${this.activeTaskIds.size} active tasks...`);
            for (const taskId of this.activeTaskIds) {
                try {
                    await this.connection.invoke('SubscribeToTask', taskId);
                    console.log(`Re-subscribed to task: ${taskId}`);
                } catch (error) {
                    console.error(`Failed to re-subscribe to task ${taskId}:`, error);
                }
            }
        }
    },

    // Invoke a method on the .NET reference
    invokeMethod(methodName, data) {
        if (this.dotNetReference) {
            try {
                if (data) {
                    this.dotNetReference.invokeMethodAsync(methodName, data);
                } else {
                    this.dotNetReference.invokeMethodAsync(methodName);
                }
            } catch (error) {
                console.error(`Error invoking method ${methodName}:`, error);
            }
        }
    },

    // Check connection status
    getConnectionState() {
        if (!this.connection) {
            return 'Not initialized';
        }
        switch (this.connection.state) {
            case signalR.HubConnectionState.Disconnected:
                return 'Disconnected';
            case signalR.HubConnectionState.Connecting:
                return 'Connecting';
            case signalR.HubConnectionState.Connected:
                return 'Connected';
            case signalR.HubConnectionState.Disconnecting:
                return 'Disconnecting';
            case signalR.HubConnectionState.Reconnecting:
                return 'Reconnecting';
            default:
                return 'Unknown';
        }
    }
};