import type { FetchBasedClient } from '../client/FetchBasedClient';
import type { SignalRService } from './SignalRService';
import { createClientAdapter, type IFetchBasedClientAdapter } from '../client/ClientAdapter';

export class ConnectionService {
  private signalr?: SignalRService;
  private clientAdapter: IFetchBasedClientAdapter;

  constructor(private client: FetchBasedClient) {
    this.clientAdapter = createClientAdapter(client);
  }

  initializeSignalR(signalr: SignalRService): void {
    this.signalr = signalr;
  }

  async testConnection(): Promise<boolean> {
    try {
      await this.clientAdapter.get('/health');
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