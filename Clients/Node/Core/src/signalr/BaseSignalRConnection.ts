import * as signalR from '@microsoft/signalr';
import { 
  HubConnectionState, 
  HttpTransportType,
  DefaultTransports
} from '../models/signalr';

/**
 * Base class for SignalR hub connections with automatic reconnection and error handling.
 */
export abstract class BaseSignalRConnection {
  protected connection?: signalR.HubConnection;
  protected readonly virtualKey: string;
  protected readonly baseUrl: string;
  protected connectionReadyPromise: Promise<void>;
  private connectionReadyResolve?: () => void;
  private connectionReadyReject?: (error: Error) => void;
  private disposed = false;

  /**
   * Gets the hub path for this connection type.
   */
  protected abstract get hubPath(): string;

  constructor(baseUrl: string, virtualKey: string) {
    this.baseUrl = baseUrl.replace(/\/$/, '');
    this.virtualKey = virtualKey;
    
    // Initialize the connection ready promise
    this.connectionReadyPromise = new Promise((resolve, reject) => {
      this.connectionReadyResolve = resolve;
      this.connectionReadyReject = reject;
    });
  }

  /**
   * Gets whether the connection is established and ready for use.
   */
  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  /**
   * Gets the current connection state.
   */
  get state(): HubConnectionState {
    if (!this.connection) {
      return HubConnectionState.Disconnected;
    }

    switch (this.connection.state) {
      case signalR.HubConnectionState.Connected:
        return HubConnectionState.Connected;
      case signalR.HubConnectionState.Connecting:
        return HubConnectionState.Connecting;
      case signalR.HubConnectionState.Disconnected:
        return HubConnectionState.Disconnected;
      case signalR.HubConnectionState.Disconnecting:
        return HubConnectionState.Disconnecting;
      case signalR.HubConnectionState.Reconnecting:
        return HubConnectionState.Reconnecting;
      default:
        return HubConnectionState.Disconnected;
    }
  }

  /**
   * Event handlers
   */
  onConnected?: () => Promise<void>;
  onDisconnected?: (error?: Error) => Promise<void>;
  onReconnecting?: (error?: Error) => Promise<void>;
  onReconnected?: (connectionId?: string) => Promise<void>;

  /**
   * Establishes the SignalR connection.
   */
  protected async getConnection(): Promise<signalR.HubConnection> {
    if (this.connection) {
      return this.connection;
    }

    const hubUrl = `${this.baseUrl}${this.hubPath}`;
    
    // Build the connection
    const builder = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.virtualKey,
        transport: this.mapTransportType(DefaultTransports),
        headers: {
          'User-Agent': 'Conduit-Core-Node-Client/0.2.0'
        },
        withCredentials: false
      })
      .withAutomaticReconnect([0, 2000, 10000, 30000]); // Retry delays

    // Configure logging
    builder.configureLogging(signalR.LogLevel.Information);

    this.connection = builder.build();

    // Set up event handlers
    this.connection.onclose(async (error) => {
      if (this.onDisconnected) {
        await this.onDisconnected(error);
      }
    });

    this.connection.onreconnecting(async (error) => {
      if (this.onReconnecting) {
        await this.onReconnecting(error);
      }
    });

    this.connection.onreconnected(async (connectionId) => {
      if (this.onReconnected) {
        await this.onReconnected(connectionId);
      }
    });

    // Configure hub-specific handlers
    this.configureHubHandlers(this.connection);

    try {
      await this.connection.start();
      
      if (this.connectionReadyResolve) {
        this.connectionReadyResolve();
      }
      
      if (this.onConnected) {
        await this.onConnected();
      }
    } catch (error) {
      if (this.connectionReadyReject) {
        this.connectionReadyReject(error as Error);
      }
      throw error;
    }

    return this.connection;
  }

  /**
   * Configures hub-specific event handlers. Override in derived classes.
   */
  protected abstract configureHubHandlers(connection: signalR.HubConnection): void;

  /**
   * Maps transport type enum to SignalR transport.
   */
  private mapTransportType(transport: HttpTransportType): signalR.HttpTransportType {
    let result = 0;
    
    if (transport & HttpTransportType.WebSockets) {
      result |= signalR.HttpTransportType.WebSockets;
    }
    if (transport & HttpTransportType.ServerSentEvents) {
      result |= signalR.HttpTransportType.ServerSentEvents;
    }
    if (transport & HttpTransportType.LongPolling) {
      result |= signalR.HttpTransportType.LongPolling;
    }
    
    return result || signalR.HttpTransportType.None;
  }

  /**
   * Waits for the connection to be established.
   */
  async waitForConnection(timeoutMs = 30000): Promise<boolean> {
    try {
      await Promise.race([
        this.connectionReadyPromise,
        new Promise<void>((_, reject) => 
          setTimeout(() => reject(new Error('Connection timeout')), timeoutMs)
        )
      ]);
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Starts the SignalR connection.
   */
  async start(): Promise<void> {
    await this.getConnection();
  }

  /**
   * Stops the SignalR connection.
   */
  async stop(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch (error) {
        // Silently handle stop errors
      }
    }
  }

  /**
   * Invokes a hub method with retry logic.
   */
  protected async invoke(methodName: string, ...args: unknown[]): Promise<void> {
    const connection = await this.getConnection();
    
    const maxRetries = 3;
    const retryDelay = 1000;
    
    for (let attempt = 1; attempt <= maxRetries; attempt++) {
      try {
        await connection.invoke(methodName, ...args);
        return;
      } catch (error) {
        if (attempt < maxRetries && this.isRetryableError(error)) {
          console.warn(`Hub method ${methodName} failed, attempt ${attempt}/${maxRetries}:`, error);
          await new Promise(resolve => setTimeout(resolve, retryDelay * attempt));
        } else {
          throw error;
        }
      }
    }
  }

  /**
   * Invokes a hub method with return value and retry logic.
   */
  protected async invokeWithResult<T>(methodName: string, ...args: unknown[]): Promise<T> {
    const connection = await this.getConnection();
    
    const maxRetries = 3;
    const retryDelay = 1000;
    
    for (let attempt = 1; attempt <= maxRetries; attempt++) {
      try {
        return await connection.invoke<T>(methodName, ...args);
      } catch (error) {
        if (attempt < maxRetries && this.isRetryableError(error)) {
          console.warn(`Hub method ${methodName} failed, attempt ${attempt}/${maxRetries}:`, error);
          await new Promise(resolve => setTimeout(resolve, retryDelay * attempt));
        } else {
          throw error;
        }
      }
    }
    
    throw new Error(`Failed to invoke ${methodName} after ${maxRetries} attempts`);
  }

  /**
   * Determines if an error is retryable.
   */
  private isRetryableError(error: unknown): boolean {
    if (!error) return false;
    
    // Don't retry on cancellation
    if (error && typeof error === 'object' && 'name' in error && error.name === 'AbortError') return false;
    
    // Retry on network errors
    if (error && typeof error === 'object' && 'message' in error && typeof error.message === 'string' && 
        (error.message.includes('network') || error.message.includes('connection'))) {
      return true;
    }
    
    // Retry on server errors
    if (error && typeof error === 'object' && 'statusCode' in error && 
        typeof error.statusCode === 'number' && error.statusCode >= 500) {
      return true;
    }
    
    return true; // Default to retry
  }

  /**
   * Disposes the SignalR connection.
   */
  async dispose(): Promise<void> {
    if (!this.disposed) {
      if (this.connection) {
        try {
          await this.connection.stop();
        } catch (error) {
          console.warn(`Error disposing SignalR connection for ${this.hubPath}:`, error);
        }
      }
      this.disposed = true;
    }
  }
}