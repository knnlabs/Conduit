import * as signalR from '@microsoft/signalr';
import type { IHubProtocol } from '@microsoft/signalr';
import { ephemeralKeyClient } from '@/lib/client/ephemeralKeyClient';

// Type for MessagePackHubProtocol constructor
type MessagePackHubProtocolConstructor = new () => IHubProtocol;

// Key for SignalR MessagePack config in window object
const SIGNALR_MESSAGEPACK_KEY = '__SIGNALR_USE_MESSAGEPACK__';

// Lazy import for MessagePack protocol
let MessagePackHubProtocol: MessagePackHubProtocolConstructor | null = null;

/**
 * Lazy loads the MessagePack protocol module
 */
async function loadMessagePackProtocol(): Promise<MessagePackHubProtocolConstructor | null> {
  if (!MessagePackHubProtocol) {
    try {
      const msgpack = await import('@microsoft/signalr-protocol-msgpack');
      MessagePackHubProtocol = msgpack.MessagePackHubProtocol;
      return msgpack.MessagePackHubProtocol;
    } catch (error) {
      console.warn('MessagePack protocol not available, using JSON:', error);
      return null;
    }
  }
  return MessagePackHubProtocol;
}

/**
 * Checks if MessagePack protocol should be used based on environment variable
 */
function shouldUseMessagePack(): boolean {
  if (typeof window === 'undefined') {
    return process.env.NEXT_PUBLIC_SIGNALR_USE_MESSAGEPACK === 'true';
  }
  return (window as unknown as Record<string, unknown>)[SIGNALR_MESSAGEPACK_KEY] === true;
}

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
   * Connect to the public video generation SignalR hub using an ephemeral key
   */
  async connect(
    taskId: string, 
    ephemeralKey?: string, // Made optional, will get one if not provided
    callbacks?: {
      onProgress?: (update: VideoProgressUpdate) => void;
      onCompleted?: (videoUrl: string) => void;
      onFailed?: (error: string) => void;
    }
  ): Promise<void> {
    // If already connected, disconnect first
    if (this.connection) {
      await this.disconnect();
    }

    // Get ephemeral key if not provided
    let keyToUse = ephemeralKey;
    let gatewayApiUrl = 'http://localhost:5000'; // default

    if (!keyToUse) {
      const keyData = await ephemeralKeyClient.getKey('video-generation');
      keyToUse = keyData.key;
      gatewayApiUrl = keyData.gatewayApiUrl;
    }

    // Create a new connection to the public hub
    // Connect directly to the Gateway API - CORS is properly configured
    const hubUrl = `${gatewayApiUrl}/hubs/public/video-generation`;
    
    const builder = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        // Don't send auth headers, we'll use the token in the subscribe call
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents | signalR.HttpTransportType.LongPolling
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Retry with exponential backoff
      .configureLogging(signalR.LogLevel.Information);

    // Configure protocol (MessagePack if enabled, otherwise JSON)
    if (shouldUseMessagePack()) {
      const MessagePackProtocol = await loadMessagePackProtocol();
      if (MessagePackProtocol) {
        builder.withHubProtocol(new MessagePackProtocol());
        console.warn('Using MessagePack protocol for video generation hub');
      }
    }

    this.connection = builder.build();

    // Set up event handlers (only if callbacks provided)
    if (callbacks) {
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
    }

    this.connection.on('taskSubscribed', (data: { taskId: string; message: string }) => {
      console.warn(`Successfully subscribed to task ${data.taskId}: ${data.message}`);
    });

    // Handle connection events
    this.connection.onreconnecting((error) => {
      console.warn('SignalR connection lost, reconnecting...', error);
    });

    this.connection.onreconnected((connectionId) => {
      console.warn('SignalR reconnected', connectionId);
      // Re-subscribe to the task after reconnection with ephemeral key
      if (this.connection) {
        void this.connection.invoke('SubscribeToTask', taskId, keyToUse);
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

      // Subscribe to the task with the ephemeral key
      await this.connection.invoke('SubscribeToTask', taskId, keyToUse);
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