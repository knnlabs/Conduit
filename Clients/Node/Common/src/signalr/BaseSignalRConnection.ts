import * as signalR from '@microsoft/signalr';
import { 
  HubConnectionState, 
  HttpTransportType,
  DefaultTransports,
  SignalRAuthConfig,
  SignalRConnectionOptions,
  SignalRLogLevel
} from './types';

/**
 * Base configuration for SignalR connections
 */
export interface BaseSignalRConfig {
  /**
   * Base URL for the SignalR hub
   */
  baseUrl: string;
  
  /**
   * Authentication configuration
   */
  auth: SignalRAuthConfig;
  
  /**
   * Connection options
   */
  options?: SignalRConnectionOptions;
  
  /**
   * User agent string
   */
  userAgent?: string;
}

/**
 * Base class for SignalR hub connections with automatic reconnection and error handling.
 * This abstract class provides common functionality for both Admin and Core SDKs.
 */
export abstract class BaseSignalRConnection {
  protected connection?: signalR.HubConnection;
  protected readonly config: BaseSignalRConfig;
  protected connectionReadyPromise: Promise<void>;
  private connectionReadyResolve?: () => void;
  private connectionReadyReject?: (error: Error) => void;
  private disposed = false;

  /**
   * Gets the hub path for this connection type.
   */
  protected abstract get hubPath(): string;

  constructor(config: BaseSignalRConfig) {
    this.config = {
      ...config,
      baseUrl: config.baseUrl.replace(/\/$/, '')
    };
    
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

    const hubUrl = `${this.config.baseUrl}${this.hubPath}`;
    
    // Build connection options
    const connectionOptions: signalR.IHttpConnectionOptions = {
      accessTokenFactory: this.config.options?.accessTokenFactory || (() => this.config.auth.authToken),
      transport: this.mapTransportType(this.config.options?.transport || DefaultTransports),
      headers: this.buildHeaders(),
      withCredentials: false
    };
    
    // Build the connection
    const builder = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, connectionOptions)
      .withAutomaticReconnect(this.config.options?.reconnectionDelay || [0, 2000, 10000, 30000]);

    // Configure server timeout and keep-alive if specified
    if (this.config.options?.serverTimeout) {
      builder.withServerTimeout(this.config.options.serverTimeout);
    }
    
    if (this.config.options?.keepAliveInterval) {
      builder.withKeepAliveInterval(this.config.options.keepAliveInterval);
    }

    // Configure logging
    const logLevel = this.mapLogLevel(this.config.options?.logLevel || SignalRLogLevel.Information);
    builder.configureLogging(logLevel);

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
  protected mapTransportType(transport: HttpTransportType): signalR.HttpTransportType {
    let result = signalR.HttpTransportType.None;
    
    if (transport & HttpTransportType.WebSockets) {
      result |= signalR.HttpTransportType.WebSockets;
    }
    if (transport & HttpTransportType.ServerSentEvents) {
      result |= signalR.HttpTransportType.ServerSentEvents;
    }
    if (transport & HttpTransportType.LongPolling) {
      result |= signalR.HttpTransportType.LongPolling;
    }
    
    return result;
  }

  /**
   * Maps log level enum to SignalR log level.
   */
  protected mapLogLevel(level: SignalRLogLevel): signalR.LogLevel {
    switch (level) {
      case SignalRLogLevel.Trace:
        return signalR.LogLevel.Trace;
      case SignalRLogLevel.Debug:
        return signalR.LogLevel.Debug;
      case SignalRLogLevel.Information:
        return signalR.LogLevel.Information;
      case SignalRLogLevel.Warning:
        return signalR.LogLevel.Warning;
      case SignalRLogLevel.Error:
        return signalR.LogLevel.Error;
      case SignalRLogLevel.Critical:
        return signalR.LogLevel.Critical;
      case SignalRLogLevel.None:
        return signalR.LogLevel.None;
      default:
        return signalR.LogLevel.Information;
    }
  }

  /**
   * Builds headers for the connection based on configuration.
   */
  private buildHeaders(): Record<string, string> {
    const headers: Record<string, string> = {
      'User-Agent': this.config.userAgent || 'Conduit-Node-Client/1.0.0',
      ...this.config.options?.headers
    };

    // Add authentication-specific headers
    if (this.config.auth.authType === 'master' && this.config.auth.additionalHeaders) {
      Object.assign(headers, this.config.auth.additionalHeaders);
    }

    return headers;
  }

  /**
   * Waits for the connection to be ready.
   */
  public async waitForReady(): Promise<void> {
    return this.connectionReadyPromise;
  }

  /**
   * Invokes a method on the hub with proper error handling.
   */
  protected async invoke<T = void>(methodName: string, ...args: unknown[]): Promise<T> {
    if (this.disposed) {
      throw new Error('Connection has been disposed');
    }

    const connection = await this.getConnection();
    
    try {
      return await connection.invoke<T>(methodName, ...args);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      throw new Error(`SignalR invoke error for ${methodName}: ${errorMessage}`);
    }
  }

  /**
   * Sends a message to the hub without expecting a response.
   */
  protected async send(methodName: string, ...args: unknown[]): Promise<void> {
    if (this.disposed) {
      throw new Error('Connection has been disposed');
    }

    const connection = await this.getConnection();
    
    try {
      await connection.send(methodName, ...args);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      throw new Error(`SignalR send error for ${methodName}: ${errorMessage}`);
    }
  }

  /**
   * Disconnects the SignalR connection.
   */
  public async disconnect(): Promise<void> {
    if (this.connection && this.connection.state !== signalR.HubConnectionState.Disconnected) {
      await this.connection.stop();
      this.connection = undefined;
      
      // Reset the connection ready promise
      this.connectionReadyPromise = new Promise((resolve, reject) => {
        this.connectionReadyResolve = resolve;
        this.connectionReadyReject = reject;
      });
    }
  }

  /**
   * Disposes of the connection and cleans up resources.
   */
  public async dispose(): Promise<void> {
    this.disposed = true;
    await this.disconnect();
    this.connectionReadyResolve = undefined;
    this.connectionReadyReject = undefined;
  }
}