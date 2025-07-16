import * as signalR from '@microsoft/signalr';
import { 
  BaseSignalRConnection as CommonBaseSignalRConnection,
  BaseSignalRConfig,
  SignalRAuthConfig
} from '@knn_labs/conduit-common';
import type { SignalRArgs, SignalRValue } from './types';

/**
 * Base class for Admin SDK SignalR hub connections.
 * Extends the common base class with Admin-specific authentication.
 */
export abstract class BaseSignalRConnection extends CommonBaseSignalRConnection {
  protected readonly masterKey: string;

  constructor(baseUrl: string, masterKey: string) {
    // Configure Admin-specific authentication
    const authConfig: SignalRAuthConfig = {
      authToken: masterKey,
      authType: 'master',
      additionalHeaders: {
        'X-Master-Key': masterKey
      }
    };

    const config: BaseSignalRConfig = {
      baseUrl,
      auth: authConfig,
      options: {
        serverTimeout: 120000, // 120 seconds
        keepAliveInterval: 15000, // 15 seconds
        reconnectionDelay: [0, 2000, 10000, 30000]
      },
      userAgent: 'Conduit-Admin-Node-Client/1.0.0'
    };

    super(config);
    this.masterKey = masterKey;
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

  /**
   * Invokes a hub method with retry logic and type-safe arguments.
   */
  protected async invokeTyped(methodName: string, ...args: SignalRArgs): Promise<void> {
    return super.invoke(methodName, ...args);
  }

  /**
   * Invokes a hub method with return value and retry logic.
   */
  protected async invokeWithResult<T extends SignalRValue>(methodName: string, ...args: SignalRArgs): Promise<T> {
    return super.invoke<T>(methodName, ...args);
  }
}