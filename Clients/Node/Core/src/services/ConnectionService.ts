import type { FetchBasedClient } from '../client/FetchBasedClient';
import type { SignalRService } from './SignalRService';

export class ConnectionService {
  private signalr?: SignalRService;

  constructor(private client: FetchBasedClient) {}

  initializeSignalR(signalr: SignalRService): void {
    this.signalr = signalr;
  }

  async testConnection(): Promise<boolean> {
    try {
      await this.client['get']('/health');
      return true;
    } catch {
      return false;
    }
  }

  async getConnectionStatus(): Promise<{
    api: boolean;
    signalr: boolean;
  }> {
    const apiConnected = await this.testConnection();
    const signalrConnected = this.signalr ? await this.signalr.isConnected() : false;

    return {
      api: apiConnected,
      signalr: signalrConnected,
    };
  }
}