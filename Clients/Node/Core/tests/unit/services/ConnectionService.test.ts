import { ConnectionService } from '../../../src/services/ConnectionService';
import { SignalRService } from '../../../src/services/SignalRService';
import { BaseClient } from '../../../src/client/BaseClient';
import { HubConnectionState } from '../../../src/models/signalr';
import type { ClientConfig } from '../../../src/client/types';

// Mock implementations
jest.mock('../../../src/services/SignalRService');
jest.mock('../../../src/client/BaseClient');

describe('ConnectionService', () => {
  let connectionService: ConnectionService;
  let mockClient: jest.Mocked<BaseClient>;
  let mockSignalRService: jest.Mocked<SignalRService>;

  beforeEach(() => {
    // Create mock client
    mockClient = {
      getConfig: jest.fn().mockReturnValue({
        apiKey: 'test-key',
        baseURL: 'http://localhost:5000',
        signalR: {}
      } as ClientConfig)
    } as any;

    // Create mock SignalR service
    mockSignalRService = {
      startAllConnections: jest.fn().mockResolvedValue(undefined),
      stopAllConnections: jest.fn().mockResolvedValue(undefined),
      getConnectionStatus: jest.fn().mockReturnValue({
        TaskHubClient: HubConnectionState.Connected,
        VideoGenerationHubClient: HubConnectionState.Connected,
        ImageGenerationHubClient: HubConnectionState.Connected
      }),
      areAllConnectionsEstablished: jest.fn().mockReturnValue(true),
      waitForAllConnections: jest.fn().mockResolvedValue(true)
    } as any;

    // Create connection service
    connectionService = new ConnectionService(mockClient);
    connectionService.initializeSignalR(mockSignalRService);
  });

  describe('connect', () => {
    it('should call startAllConnections on SignalR service', async () => {
      await connectionService.connect();
      expect(mockSignalRService.startAllConnections).toHaveBeenCalledTimes(1);
    });

    it('should throw error if SignalR not initialized', async () => {
      const service = new ConnectionService(mockClient);
      await expect(service.connect()).rejects.toThrow('SignalR service is not initialized');
    });
  });

  describe('disconnect', () => {
    it('should call stopAllConnections on SignalR service', async () => {
      await connectionService.disconnect();
      expect(mockSignalRService.stopAllConnections).toHaveBeenCalledTimes(1);
    });

    it('should throw error if SignalR not initialized', async () => {
      const service = new ConnectionService(mockClient);
      await expect(service.disconnect()).rejects.toThrow('SignalR service is not initialized');
    });
  });

  describe('getStatus', () => {
    it('should return connection status from SignalR service', () => {
      const status = connectionService.getStatus();
      expect(status).toEqual({
        TaskHubClient: HubConnectionState.Connected,
        VideoGenerationHubClient: HubConnectionState.Connected,
        ImageGenerationHubClient: HubConnectionState.Connected
      });
      expect(mockSignalRService.getConnectionStatus).toHaveBeenCalledTimes(1);
    });

    it('should throw error if SignalR not initialized', () => {
      const service = new ConnectionService(mockClient);
      expect(() => service.getStatus()).toThrow('SignalR service is not initialized');
    });
  });

  describe('isConnected', () => {
    it('should return true when all connections are established', () => {
      mockSignalRService.areAllConnectionsEstablished.mockReturnValue(true);
      expect(connectionService.isConnected()).toBe(true);
    });

    it('should return false when not all connections are established', () => {
      mockSignalRService.areAllConnectionsEstablished.mockReturnValue(false);
      expect(connectionService.isConnected()).toBe(false);
    });
  });

  describe('waitForConnection', () => {
    it('should wait for connections with default timeout', async () => {
      const result = await connectionService.waitForConnection();
      expect(result).toBe(true);
      expect(mockSignalRService.waitForAllConnections).toHaveBeenCalledWith(30000);
    });

    it('should wait for connections with custom timeout', async () => {
      const result = await connectionService.waitForConnection(10000);
      expect(result).toBe(true);
      expect(mockSignalRService.waitForAllConnections).toHaveBeenCalledWith(10000);
    });

    it('should return false on timeout', async () => {
      mockSignalRService.waitForAllConnections.mockResolvedValue(false);
      const result = await connectionService.waitForConnection(5000);
      expect(result).toBe(false);
    });
  });

  describe('reconnect', () => {
    it('should disconnect then connect', async () => {
      await connectionService.reconnect();
      expect(mockSignalRService.stopAllConnections).toHaveBeenCalledTimes(1);
      expect(mockSignalRService.startAllConnections).toHaveBeenCalledTimes(1);
      
      // Verify order
      const stopOrder = mockSignalRService.stopAllConnections.mock.invocationCallOrder[0];
      const startOrder = mockSignalRService.startAllConnections.mock.invocationCallOrder[0];
      expect(stopOrder).toBeLessThan(startOrder);
    });
  });

  describe('updateConfiguration', () => {
    it('should update configuration and reconnect if was connected', async () => {
      mockSignalRService.areAllConnectionsEstablished.mockReturnValue(true);
      
      await connectionService.updateConfiguration({
        reconnectDelay: [0, 1000, 5000],
        connectionTimeout: 20000
      });

      expect(mockSignalRService.stopAllConnections).toHaveBeenCalled();
      expect(mockSignalRService.startAllConnections).toHaveBeenCalled();
      expect(mockClient.getConfig).toHaveBeenCalled();
    });

    it('should not reconnect if autoConnect is disabled', async () => {
      mockSignalRService.areAllConnectionsEstablished.mockReturnValue(true);
      
      await connectionService.updateConfiguration({
        autoConnect: false
      });

      expect(mockSignalRService.stopAllConnections).toHaveBeenCalled();
      expect(mockSignalRService.startAllConnections).not.toHaveBeenCalled();
    });

    it('should not disconnect if not connected', async () => {
      mockSignalRService.areAllConnectionsEstablished.mockReturnValue(false);
      
      await connectionService.updateConfiguration({
        reconnectDelay: [0, 1000, 5000]
      });

      expect(mockSignalRService.stopAllConnections).not.toHaveBeenCalled();
      expect(mockSignalRService.startAllConnections).not.toHaveBeenCalled();
    });
  });

  describe('getDetailedStatus', () => {
    it('should return detailed status for all hubs', () => {
      mockSignalRService.getConnectionStatus.mockReturnValue({
        TaskHubClient: HubConnectionState.Connected,
        VideoGenerationHubClient: HubConnectionState.Connecting,
        ImageGenerationHubClient: HubConnectionState.Disconnected
      });

      const detailed = connectionService.getDetailedStatus();
      
      expect(detailed).toHaveLength(3);
      expect(detailed[0]).toEqual({
        hub: 'TaskHubClient',
        state: HubConnectionState.Connected,
        stateDescription: 'Connected and ready',
        isConnected: true
      });
      expect(detailed[1]).toEqual({
        hub: 'VideoGenerationHubClient',
        state: HubConnectionState.Connecting,
        stateDescription: 'Establishing connection',
        isConnected: false
      });
      expect(detailed[2]).toEqual({
        hub: 'ImageGenerationHubClient',
        state: HubConnectionState.Disconnected,
        stateDescription: 'Not connected',
        isConnected: false
      });
    });
  });

  describe('onConnectionStateChange', () => {
    it('should warn that feature is not implemented', () => {
      const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();
      const callback = jest.fn();
      
      const unsubscribe = connectionService.onConnectionStateChange(callback);
      
      expect(consoleSpy).toHaveBeenCalledWith('Connection state change subscriptions are not yet implemented');
      expect(typeof unsubscribe).toBe('function');
      
      consoleSpy.mockRestore();
    });
  });
});