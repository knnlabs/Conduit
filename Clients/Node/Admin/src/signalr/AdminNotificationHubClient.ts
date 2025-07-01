import { HubConnection } from '@microsoft/signalr';
import { BaseSignalRConnection } from './BaseSignalRConnection';
import { 
  SignalREndpoints, 
  VirtualKeyEvent,
  ConfigurationChangeEvent,
  AdminNotificationEvent,
  IAdminNotificationHubClient
} from '../models/signalr';

/**
 * SignalR client for admin notifications
 */
export class AdminNotificationHubClient extends BaseSignalRConnection implements IAdminNotificationHubClient {
  protected get hubPath(): string {
    return SignalREndpoints.AdminNotifications;
  }

  private virtualKeyCallbacks: ((event: VirtualKeyEvent) => void)[] = [];
  private configChangeCallbacks: ((event: ConfigurationChangeEvent) => void)[] = [];
  private adminNotificationCallbacks: ((event: AdminNotificationEvent) => void)[] = [];

  /**
   * Configures event handlers for the admin notification hub
   */
  protected configureHubHandlers(connection: HubConnection): void {
    // Virtual key event handler
    connection.on('VirtualKeyEvent', (event: VirtualKeyEvent) => {
      this.virtualKeyCallbacks.forEach(callback => {
        try {
          callback(event);
        } catch (error) {
          console.error('Error in virtual key event callback:', error);
        }
      });
    });

    // Configuration change handler
    connection.on('ConfigurationChanged', (event: ConfigurationChangeEvent) => {
      this.configChangeCallbacks.forEach(callback => {
        try {
          callback(event);
        } catch (error) {
          console.error('Error in configuration change callback:', error);
        }
      });
    });

    // Admin notification handler
    connection.on('AdminNotification', (event: AdminNotificationEvent) => {
      this.adminNotificationCallbacks.forEach(callback => {
        try {
          callback(event);
        } catch (error) {
          console.error('Error in admin notification callback:', error);
        }
      });
    });
  }

  /**
   * Subscribe to virtual key events
   */
  onVirtualKeyEvent(callback: (event: VirtualKeyEvent) => void): void {
    this.virtualKeyCallbacks.push(callback);
  }

  /**
   * Subscribe to configuration changes
   */
  onConfigurationChange(callback: (event: ConfigurationChangeEvent) => void): void {
    this.configChangeCallbacks.push(callback);
  }

  /**
   * Subscribe to admin notifications
   */
  onAdminNotification(callback: (event: AdminNotificationEvent) => void): void {
    this.adminNotificationCallbacks.push(callback);
  }

  /**
   * Subscribe to all admin notifications
   */
  async subscribe(): Promise<void> {
    await this.invoke('Subscribe');
  }

  /**
   * Unsubscribe from admin notifications
   */
  async unsubscribe(): Promise<void> {
    await this.invoke('Unsubscribe');
  }

  /**
   * Acknowledge a notification as read
   */
  async acknowledgeNotification(notificationId: string): Promise<void> {
    await this.invoke('AcknowledgeNotification', notificationId);
  }

  /**
   * Clear all callbacks
   */
  clearCallbacks(): void {
    this.virtualKeyCallbacks = [];
    this.configChangeCallbacks = [];
    this.adminNotificationCallbacks = [];
  }

  /**
   * Remove a specific virtual key callback
   */
  removeVirtualKeyCallback(callback: (event: VirtualKeyEvent) => void): void {
    const index = this.virtualKeyCallbacks.indexOf(callback);
    if (index > -1) {
      this.virtualKeyCallbacks.splice(index, 1);
    }
  }

  /**
   * Remove a specific configuration change callback
   */
  removeConfigChangeCallback(callback: (event: ConfigurationChangeEvent) => void): void {
    const index = this.configChangeCallbacks.indexOf(callback);
    if (index > -1) {
      this.configChangeCallbacks.splice(index, 1);
    }
  }

  /**
   * Remove a specific admin notification callback
   */
  removeAdminNotificationCallback(callback: (event: AdminNotificationEvent) => void): void {
    const index = this.adminNotificationCallbacks.indexOf(callback);
    if (index > -1) {
      this.adminNotificationCallbacks.splice(index, 1);
    }
  }

  /**
   * Get the number of active callbacks
   */
  getActiveCallbackCount(): number {
    return this.virtualKeyCallbacks.length + 
           this.configChangeCallbacks.length + 
           this.adminNotificationCallbacks.length;
  }

  /**
   * Dispose the client
   */
  async dispose(): Promise<void> {
    this.clearCallbacks();
    await super.dispose();
  }
}