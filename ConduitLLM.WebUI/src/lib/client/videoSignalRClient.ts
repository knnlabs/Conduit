import * as signalR from '@microsoft/signalr';

interface VideoProgressUpdate {
  taskId: string;
  status: string;
  progress?: number;
  message?: string;
  videoUrl?: string;
  error?: string;
}

export class VideoSignalRClient {
  private connection: signalR.HubConnection | null = null;
  private connectionPromise: Promise<void> | null = null;
  
  /**
   * Connect to the public video generation SignalR hub using a task token
   */
  async connect(
    taskId: string, 
    token: string,
    callbacks: {
      onProgress?: (update: VideoProgressUpdate) => void;
      onCompleted?: (videoUrl: string) => void;
      onFailed?: (error: string) => void;
    }
  ): Promise<void> {
    // If already connected, disconnect first
    if (this.connection) {
      await this.disconnect();
    }

    // Create a new connection to the public hub
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/hubs/public/video-generation', {
        // Don't send auth headers, we'll use the token in the subscribe call
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents | signalR.HttpTransportType.LongPolling
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Retry with exponential backoff
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Set up event handlers
    this.connection.on('taskProgress', (update: VideoProgressUpdate) => {
      if (update.taskId === taskId && callbacks.onProgress) {
        callbacks.onProgress(update);
      }
    });

    this.connection.on('taskCompleted', (data: { taskId: string; videoUrl: string }) => {
      if (data.taskId === taskId && callbacks.onCompleted) {
        callbacks.onCompleted(data.videoUrl);
        // Auto-disconnect after completion
        void this.disconnect();
      }
    });

    this.connection.on('taskFailed', (data: { taskId: string; error: string }) => {
      if (data.taskId === taskId && callbacks.onFailed) {
        callbacks.onFailed(data.error);
        // Auto-disconnect after failure
        void this.disconnect();
      }
    });

    this.connection.on('taskSubscribed', (data: { taskId: string; message: string }) => {
      console.warn(`Successfully subscribed to task ${data.taskId}: ${data.message}`);
    });

    // Handle connection events
    this.connection.onreconnecting((error) => {
      console.warn('SignalR connection lost, reconnecting...', error);
    });

    this.connection.onreconnected((connectionId) => {
      console.warn('SignalR reconnected', connectionId);
      // Re-subscribe to the task after reconnection
      if (this.connection) {
        void this.connection.invoke('SubscribeToTask', taskId, token);
      }
    });

    this.connection.onclose((error) => {
      if (error) {
        console.error('SignalR connection closed with error:', error);
      } else {
        console.warn('SignalR connection closed');
      }
      this.connection = null;
      this.connectionPromise = null;
    });

    // Start the connection
    try {
      this.connectionPromise = this.connection.start();
      await this.connectionPromise;
      console.warn('SignalR connected successfully');

      // Subscribe to the task with the token
      await this.connection.invoke('SubscribeToTask', taskId, token);
      console.warn(`Subscribed to task ${taskId}`);
    } catch (error) {
      console.error('Failed to connect to SignalR:', error);
      this.connection = null;
      this.connectionPromise = null;
      throw error;
    }
  }

  /**
   * Disconnect from SignalR
   */
  async disconnect(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch (error) {
        console.error('Error stopping SignalR connection:', error);
      } finally {
        this.connection = null;
        this.connectionPromise = null;
      }
    }
  }

  /**
   * Check if connected
   */
  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

// Export a singleton instance
export const videoSignalRClient = new VideoSignalRClient();