// SignalR connection for real-time video generation updates
window.videoGenerationSignalR = {
    connection: null,
    isConnected: false,
    reconnectAttempts: 0,
    maxReconnectAttempts: 10,
    reconnectDelay: 5000,
    dotNetReference: null,
    activeTaskIds: new Set(),

    // Initialize SignalR connection
    async initialize(dotNetReference, virtualKey) {
        // console.log('Initializing video generation SignalR connection...');
        this.dotNetReference = dotNetReference;
        this.virtualKey = virtualKey;
        
        try {
            // Use configured API base URL from window.conduitConfig
            const apiBaseUrl = window.conduitConfig?.apiBaseUrl || 'http://localhost:5000';
            const hubUrl = `${apiBaseUrl}/hubs/video-generation`;
            
            // console.log('Connecting to video generation hub at:', hubUrl);
            
            // Create SignalR connection with authentication
            const connectionOptions = {};
            if (this.virtualKey) {
                connectionOptions.accessTokenFactory = () => this.virtualKey;
                connectionOptions.headers = {
                    'Authorization': `Bearer ${this.virtualKey}`
                };
            }
            
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl, connectionOptions)
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
            console.error('Failed to initialize video generation SignalR:', error);
            return false;
        }
    },

    // Set up SignalR event handlers
    setupEventHandlers() {
        // Connection state change handlers
        this.connection.onreconnecting(error => {
            // console.log('Video generation SignalR reconnecting...', error);
            this.isConnected = false;
            this.invokeMethod('OnReconnecting');
        });

        this.connection.onreconnected(connectionId => {
            // console.log('Video generation SignalR reconnected:', connectionId);
            this.isConnected = true;
            this.reconnectAttempts = 0;
            this.invokeMethod('OnReconnected');
            // Re-subscribe to active tasks
            this.resubscribeToActiveTasks();
        });

        this.connection.onclose(error => {
            // console.log('Video generation SignalR connection closed:', error);
            this.isConnected = false;
            this.invokeMethod('OnDisconnected');
            // Attempt manual reconnect if needed
            if (this.reconnectAttempts < this.maxReconnectAttempts) {
                setTimeout(() => this.start(), this.reconnectDelay);
            }
        });

        // Video generation event handlers
        this.connection.on('VideoGenerationStarted', (data) => {
            // console.log('Video generation started:', data);
            this.invokeMethod('OnVideoGenerationStarted', data);
        });

        this.connection.on('VideoGenerationProgress', (data) => {
            // console.log('Video generation progress:', data);
            this.invokeMethod('OnVideoGenerationProgress', data);
        });

        this.connection.on('VideoGenerationCompleted', (data) => {
            // console.log('Video generation completed:', data);
            this.activeTaskIds.delete(data.taskId);
            this.invokeMethod('OnVideoGenerationCompleted', data);
        });

        this.connection.on('VideoGenerationFailed', (data) => {
            // console.log('Video generation failed:', data);
            this.activeTaskIds.delete(data.taskId);
            this.invokeMethod('OnVideoGenerationFailed', data);
        });

        this.connection.on('VideoGenerationCancelled', (data) => {
            // console.log('Video generation cancelled:', data);
            this.activeTaskIds.delete(data.taskId);
            this.invokeMethod('OnVideoGenerationCancelled', data);
        });
    },

    // Start SignalR connection
    async start() {
        if (this.connection.state === signalR.HubConnectionState.Disconnected) {
            try {
                await this.connection.start();
                // console.log('Video generation SignalR connected');
                this.isConnected = true;
                this.reconnectAttempts = 0;
                this.invokeMethod('OnConnected');
                return true;
            } catch (error) {
                console.error('Failed to start video generation SignalR:', error);
                this.reconnectAttempts++;
                if (this.reconnectAttempts < this.maxReconnectAttempts) {
                    const delay = Math.min(this.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1), 30000);
                    // console.log(`Retrying connection in ${delay}ms...`);
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
                // console.log('Video generation SignalR disconnected');
                this.isConnected = false;
                this.activeTaskIds.clear();
            } catch (error) {
                console.error('Error stopping video generation SignalR:', error);
            }
        }
    },

    // Subscribe to updates for a specific task
    async subscribeToTask(taskId) {
        if (!this.isConnected) {
            // console.warn('Cannot subscribe to task - SignalR not connected');
            return false;
        }

        try {
            await this.connection.invoke('SubscribeToRequest', taskId);
            this.activeTaskIds.add(taskId);
            // console.log(`Subscribed to video generation updates for task: ${taskId}`);
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
            await this.connection.invoke('UnsubscribeFromRequest', taskId);
            this.activeTaskIds.delete(taskId);
            // console.log(`Unsubscribed from video generation updates for task: ${taskId}`);
            return true;
        } catch (error) {
            console.error(`Failed to unsubscribe from task ${taskId}:`, error);
            return false;
        }
    },

    // Re-subscribe to all active tasks after reconnection
    async resubscribeToActiveTasks() {
        if (this.activeTaskIds.size > 0) {
            // console.log(`Re-subscribing to ${this.activeTaskIds.size} active tasks...`);
            for (const taskId of this.activeTaskIds) {
                try {
                    await this.connection.invoke('SubscribeToRequest', taskId);
                    // console.log(`Re-subscribed to task: ${taskId}`);
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