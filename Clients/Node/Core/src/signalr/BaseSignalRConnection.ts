import { 
  BaseSignalRConnection as CommonBaseSignalRConnection,
  type BaseSignalRConfig,
  type SignalRAuthConfig
} from '@knn_labs/conduit-common';

/**
 * Base class for Core SDK SignalR hub connections.
 * Extends the common base class with Core SDK-specific authentication.
 */
export abstract class BaseSignalRConnection extends CommonBaseSignalRConnection {
  protected readonly virtualKey: string;

  constructor(baseUrl: string, virtualKey: string) {
    // Configure Core SDK-specific authentication
    const authConfig: SignalRAuthConfig = {
      authToken: virtualKey,
      authType: 'virtual'
    };

    const config: BaseSignalRConfig = {
      baseUrl,
      auth: authConfig,
      options: {
        reconnectionDelay: [0, 2000, 10000, 30000]
      },
      userAgent: 'Conduit-Core-Node-Client/0.2.0'
    };

    super(config);
    this.virtualKey = virtualKey;
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
    await this.disconnect();
  }

  /**
   * Waits for the connection to be established.
   */
  async waitForConnection(timeoutMs = 30000): Promise<boolean> {
    try {
      await Promise.race([
        this.waitForReady(),
        new Promise<void>((_, reject) => 
          setTimeout(() => reject(new Error('Connection timeout')), timeoutMs)
        )
      ]);
      return true;
    } catch {
      return false;
    }
  }
}