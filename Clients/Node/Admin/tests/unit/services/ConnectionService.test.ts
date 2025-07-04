import { ConnectionService } from '../../../src/services/ConnectionService';
import { SignalRService } from '../../../src/services/SignalRService';
import { HubConnectionState } from '../../../src/models/signalr';
import { NavigationStateHubClient } from '../../../src/signalr/NavigationStateHubClient';
import { AdminNotificationHubClient } from '../../../src/signalr/AdminNotificationHubClient';

// Mock implementations
jest.mock('../../../src/services/SignalRService');
jest.mock('../../../src/signalr/NavigationStateHubClient');
jest.mock('../../../src/signalr/AdminNotificationHubClient');

describe('ConnectionService (Admin)', () => {
  let connectionService: ConnectionService;
  let mockSignalRService: jest.Mocked<SignalRService>;
  let mockNavigationHub: jest.Mocked<NavigationStateHubClient>;
  let mockNotificationHub: jest.Mocked<AdminNotificationHubClient>;

  beforeEach(() => {
    // Create mock hubs
    mockNavigationHub = {
      start: jest.fn().mockResolvedValue(undefined),
      stop: jest.fn().mockResolvedValue(undefined),
      state: HubConnectionState.Connected
    } as any;
    
    // Define isConnected as a getter
    Object.defineProperty(mockNavigationHub, 'isConnected', {
      get: jest.fn().mockReturnValue(true),
      configurable: true
    });

    mockNotificationHub = {
      start: jest.fn().mockResolvedValue(undefined),
      stop: jest.fn().mockResolvedValue(undefined),
      state: HubConnectionState.Connected
    } as any;
    
    // Define isConnected as a getter
    Object.defineProperty(mockNotificationHub, 'isConnected', {
      get: jest.fn().mockReturnValue(true),
      configurable: true
    });

    // Create mock SignalR service
    mockSignalRService = {
      connectAll: jest.fn().mockResolvedValue(undefined),
      disconnectAll: jest.fn().mockResolvedValue(undefined),
      getConnectionStates: jest.fn().mockReturnValue({
        navigationState: HubConnectionState.Connected,
        adminNotifications: HubConnectionState.Connected
      }),
      isAnyConnected: jest.fn().mockReturnValue(true),
      getOrCreateConnection: jest.fn().mockImplementation((type: string) => {
        return type === 'navigation' ? mockNavigationHub : mockNotificationHub;
      })
    } as any;

    // Create connection service
    connectionService = new ConnectionService();
    connectionService.initializeSignalR(mockSignalRService);
  });

  describe('connect', () => {
    it('should call connectAll on SignalR service', async () => {
      await connectionService.connect();
      expect(mockSignalRService.connectAll).toHaveBeenCalledTimes(1);
    });

    it('should throw error if SignalR not initialized', async () => {
      const service = new ConnectionService();
      await expect(service.connect()).rejects.toThrow('SignalR service is not initialized');
    });
  });

  describe('disconnect', () => {
    it('should call disconnectAll on SignalR service', async () => {
      await connectionService.disconnect();
      expect(mockSignalRService.disconnectAll).toHaveBeenCalledTimes(1);
    });
  });

  describe('getStatus', () => {
    it('should return connection states from SignalR service', () => {
      const status = connectionService.getStatus();
      expect(status).toEqual({
        navigationState: HubConnectionState.Connected,
        adminNotifications: HubConnectionState.Connected
      });
      expect(mockSignalRService.getConnectionStates).toHaveBeenCalledTimes(1);
    });
  });

  describe('isConnected', () => {
    it('should return true when any hub is connected', () => {
      mockSignalRService.isAnyConnected.mockReturnValue(true);
      expect(connectionService.isConnected()).toBe(true);
    });

    it('should return false when no hubs are connected', () => {
      mockSignalRService.isAnyConnected.mockReturnValue(false);
      expect(connectionService.isConnected()).toBe(false);
    });
  });

  describe('isFullyConnected', () => {
    it('should return true when all hubs are connected', () => {
      mockSignalRService.getConnectionStates.mockReturnValue({
        navigationState: HubConnectionState.Connected,
        adminNotifications: HubConnectionState.Connected
      });
      expect(connectionService.isFullyConnected()).toBe(true);
    });

    it('should return false when any hub is not connected', () => {
      mockSignalRService.getConnectionStates.mockReturnValue({
        navigationState: HubConnectionState.Connected,
        adminNotifications: HubConnectionState.Disconnected
      });
      expect(connectionService.isFullyConnected()).toBe(false);
    });
  });

  describe('waitForConnection', () => {
    it('should resolve true when all connections are established', async () => {
      jest.spyOn(connectionService, 'isFullyConnected')
        .mockReturnValueOnce(false)
        .mockReturnValueOnce(false)
        .mockReturnValueOnce(true);

      const result = await connectionService.waitForConnection(1000);
      expect(result).toBe(true);
    });

    it('should resolve false on timeout', async () => {
      jest.spyOn(connectionService, 'isFullyConnected').mockReturnValue(false);
      
      const result = await connectionService.waitForConnection(500);
      expect(result).toBe(false);
    });
  });

  describe('reconnect', () => {
    it('should disconnect then connect', async () => {
      await connectionService.reconnect();
      expect(mockSignalRService.disconnectAll).toHaveBeenCalledTimes(1);
      expect(mockSignalRService.connectAll).toHaveBeenCalledTimes(1);
      
      // Verify order
      const disconnectOrder = mockSignalRService.disconnectAll.mock.invocationCallOrder[0];
      const connectOrder = mockSignalRService.connectAll.mock.invocationCallOrder[0];
      expect(disconnectOrder).toBeLessThan(connectOrder);
    });
  });

  describe('connectHub', () => {
    it('should connect navigation hub when not connected', async () => {
      // Redefine isConnected getter to return false
      Object.defineProperty(mockNavigationHub, 'isConnected', {
        get: jest.fn().mockReturnValue(false),
        configurable: true
      });
      
      await connectionService.connectHub('navigation');
      
      expect(mockSignalRService.getOrCreateConnection).toHaveBeenCalledWith('navigation');
      expect(mockNavigationHub.start).toHaveBeenCalledTimes(1);
    });

    it('should not reconnect navigation hub if already connected', async () => {
      // Redefine isConnected getter to return true
      Object.defineProperty(mockNavigationHub, 'isConnected', {
        get: jest.fn().mockReturnValue(true),
        configurable: true
      });
      
      await connectionService.connectHub('navigation');
      
      expect(mockSignalRService.getOrCreateConnection).toHaveBeenCalledWith('navigation');
      expect(mockNavigationHub.start).not.toHaveBeenCalled();
    });

    it('should connect notification hub', async () => {
      // Redefine isConnected getter to return false
      Object.defineProperty(mockNotificationHub, 'isConnected', {
        get: jest.fn().mockReturnValue(false),
        configurable: true
      });
      
      await connectionService.connectHub('notifications');
      
      expect(mockSignalRService.getOrCreateConnection).toHaveBeenCalledWith('notifications');
      expect(mockNotificationHub.start).toHaveBeenCalledTimes(1);
    });
  });

  describe('disconnectHub', () => {
    it('should disconnect navigation hub when connected', async () => {
      // Redefine isConnected getter to return true
      Object.defineProperty(mockNavigationHub, 'isConnected', {
        get: jest.fn().mockReturnValue(true),
        configurable: true
      });
      
      await connectionService.disconnectHub('navigation');
      
      expect(mockSignalRService.getOrCreateConnection).toHaveBeenCalledWith('navigation');
      expect(mockNavigationHub.stop).toHaveBeenCalledTimes(1);
    });

    it('should not disconnect navigation hub if already disconnected', async () => {
      // Redefine isConnected getter to return false
      Object.defineProperty(mockNavigationHub, 'isConnected', {
        get: jest.fn().mockReturnValue(false),
        configurable: true
      });
      
      await connectionService.disconnectHub('navigation');
      
      expect(mockSignalRService.getOrCreateConnection).toHaveBeenCalledWith('navigation');
      expect(mockNavigationHub.stop).not.toHaveBeenCalled();
    });
  });

  describe('getHubStatus', () => {
    it('should return navigation hub status', () => {
      mockSignalRService.getConnectionStates.mockReturnValue({
        navigationState: HubConnectionState.Connecting,
        adminNotifications: HubConnectionState.Connected
      });
      
      const status = connectionService.getHubStatus('navigation');
      expect(status).toBe(HubConnectionState.Connecting);
    });

    it('should return notification hub status', () => {
      mockSignalRService.getConnectionStates.mockReturnValue({
        navigationState: HubConnectionState.Connected,
        adminNotifications: HubConnectionState.Reconnecting
      });
      
      const status = connectionService.getHubStatus('notifications');
      expect(status).toBe(HubConnectionState.Reconnecting);
    });

    it('should return Disconnected for unknown hub', () => {
      mockSignalRService.getConnectionStates.mockReturnValue({});
      
      const status = connectionService.getHubStatus('navigation');
      expect(status).toBe(HubConnectionState.Disconnected);
    });
  });

  describe('getDetailedStatus', () => {
    it('should return detailed status for all hubs', () => {
      mockSignalRService.getConnectionStates.mockReturnValue({
        navigationState: HubConnectionState.Connected,
        adminNotifications: HubConnectionState.Reconnecting
      });

      const detailed = connectionService.getDetailedStatus();
      
      expect(detailed).toHaveLength(2);
      expect(detailed[0]).toEqual({
        hub: 'navigationState',
        state: HubConnectionState.Connected,
        stateDescription: 'Connected and ready',
        isConnected: true
      });
      expect(detailed[1]).toEqual({
        hub: 'adminNotifications',
        state: HubConnectionState.Reconnecting,
        stateDescription: 'Connection lost, attempting to reconnect',
        isConnected: false
      });
    });
  });

  describe('updateConfiguration', () => {
    it('should update configuration and reconnect if was connected', async () => {
      jest.spyOn(connectionService, 'isConnected').mockReturnValue(true);
      
      await connectionService.updateConfiguration({
        reconnectDelay: [0, 1000, 5000],
        connectionTimeout: 20000
      });

      expect(mockSignalRService.disconnectAll).toHaveBeenCalled();
      expect(mockSignalRService.connectAll).toHaveBeenCalled();
    });

    it('should not reconnect if autoConnect is disabled', async () => {
      jest.spyOn(connectionService, 'isConnected').mockReturnValue(true);
      
      await connectionService.updateConfiguration({
        autoConnect: false
      });

      expect(mockSignalRService.disconnectAll).toHaveBeenCalled();
      expect(mockSignalRService.connectAll).not.toHaveBeenCalled();
    });
  });
});