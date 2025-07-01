import { ConduitCoreClient } from '@knn_labs/conduit-core-client';
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { logger } from '@/lib/utils/logging';
import { useConnectionStore } from '@/stores/useConnectionStore';

export interface SDKSignalRConfig {
  coreApiUrl?: string;
  adminApiUrl?: string;
  virtualKey?: string;
  masterKey?: string;
  autoConnect?: boolean;
  reconnectInterval?: number[];
}

export interface SignalREventHandlers {
  // Navigation State Events
  onNavigationStateUpdate?: (data: NavigationStateUpdate) => void;
  
  // Task Progress Events
  onVideoGenerationProgress?: (data: VideoGenerationProgress) => void;
  onImageGenerationProgress?: (data: ImageGenerationProgress) => void;
  
  // Spend Tracking Events
  onSpendUpdate?: (data: SpendUpdate) => void;
  onSpendLimitAlert?: (data: SpendLimitAlert) => void;
  
  // Model Discovery Events
  onModelDiscovered?: (data: ModelDiscoveryEvent) => void;
  onProviderHealthChange?: (data: ProviderHealthEvent) => void;
  
  // Admin Events
  onVirtualKeyUpdate?: (data: VirtualKeyEvent) => void;
  onConfigurationChange?: (data: ConfigurationEvent) => void;
}

// Event type definitions
export interface NavigationStateUpdate {
  type: 'model_mapping' | 'provider' | 'virtual_key';
  action: 'created' | 'updated' | 'deleted';
  data: any;
  timestamp: string;
}

export interface VideoGenerationProgress {
  taskId: string;
  status: 'queued' | 'processing' | 'completed' | 'failed';
  progress: number;
  estimatedTimeRemaining?: number;
  resultUrl?: string;
  error?: string;
}

export interface ImageGenerationProgress {
  taskId: string;
  status: 'queued' | 'processing' | 'completed' | 'failed';
  progress: number;
  resultUrl?: string;
  error?: string;
}

export interface SpendUpdate {
  virtualKeyId: string;
  amount: number;
  totalSpend: number;
  model: string;
  timestamp: string;
}

export interface SpendLimitAlert {
  virtualKeyId: string;
  currentSpend: number;
  limit: number;
  percentage: number;
  alertLevel: 'warning' | 'critical';
}

export interface ModelDiscoveryEvent {
  providerId: string;
  providerName: string;
  modelsDiscovered: string[];
  timestamp: string;
}

export interface ProviderHealthEvent {
  providerId: string;
  providerName: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  latency?: number;
  error?: string;
}

export interface VirtualKeyEvent {
  keyId: string;
  action: 'created' | 'updated' | 'deleted' | 'disabled' | 'enabled';
  changes?: string[];
}

export interface ConfigurationEvent {
  category: string;
  setting: string;
  oldValue: any;
  newValue: any;
  timestamp: string;
}

export class SDKSignalRManager {
  private coreClient?: ConduitCoreClient;
  private adminClient?: ConduitAdminClient;
  private config: SDKSignalRConfig;
  private eventHandlers: SignalREventHandlers = {};
  private cleanupFunctions: (() => void)[] = [];

  constructor(config: SDKSignalRConfig) {
    this.config = {
      autoConnect: true,
      reconnectInterval: [0, 2000, 10000, 30000],
      ...config,
    };
  }

  // Initialize Core client with SignalR
  async initializeCoreClient(virtualKey: string): Promise<void> {
    if (!this.config.coreApiUrl) {
      throw new Error('Core API URL not configured');
    }

    logger.info('Initializing Core client with SignalR');

    this.coreClient = new ConduitCoreClient({
      baseURL: this.config.coreApiUrl,
      apiKey: virtualKey,
      signalR: {
        enabled: true,
        autoConnect: this.config.autoConnect !== false,
        transports: ['WebSockets', 'ServerSentEvents', 'LongPolling'],
        reconnectInterval: this.config.reconnectInterval,
      },
    });

    // Set up Core event listeners
    await this.setupCoreEventListeners();
  }

  // Initialize Admin client with SignalR
  async initializeAdminClient(masterKey: string): Promise<void> {
    if (!this.config.adminApiUrl) {
      throw new Error('Admin API URL not configured');
    }

    logger.info('Initializing Admin client with SignalR');

    this.adminClient = new ConduitAdminClient({
      adminApiUrl: this.config.adminApiUrl,
      masterKey: masterKey,
      signalR: {
        enabled: true,
        autoConnect: this.config.autoConnect !== false,
        transports: ['WebSockets', 'ServerSentEvents', 'LongPolling'],
        reconnectInterval: this.config.reconnectInterval,
      },
    });

    // Set up Admin event listeners
    await this.setupAdminEventListeners();
  }

  // Set up Core API event listeners
  private async setupCoreEventListeners(): Promise<void> {
    if (!this.coreClient) {
      throw new Error('Core client not initialized');
    }

    logger.info('Setting up Core event listeners');

    // Video generation progress
    const videoSub = await this.coreClient.notifications.onVideoProgress((event) => {
      if (this.eventHandlers.onVideoGenerationProgress) {
        this.eventHandlers.onVideoGenerationProgress({
          taskId: event.taskId,
          status: event.status as 'queued' | 'processing' | 'completed' | 'failed',
          progress: event.progress || 0,
          estimatedTimeRemaining: undefined, // Not provided by SDK
          resultUrl: event.status === 'completed' && event.metadata?.url ? event.metadata.url : undefined,
          error: event.message,
        });
      }
    });
    this.cleanupFunctions.push(() => videoSub.unsubscribe());

    // Image generation progress
    const imageSub = await this.coreClient.notifications.onImageProgress((event) => {
      if (this.eventHandlers.onImageGenerationProgress) {
        this.eventHandlers.onImageGenerationProgress({
          taskId: event.taskId,
          status: event.status as 'queued' | 'processing' | 'completed' | 'failed',
          progress: event.progress || 0,
          resultUrl: event.images?.[0]?.url,
          error: event.message,
        });
      }
    });
    this.cleanupFunctions.push(() => imageSub.unsubscribe());

    // Spend updates
    const spendSub = await this.coreClient.notifications.onSpendUpdate((event) => {
      if (this.eventHandlers.onSpendUpdate) {
        this.eventHandlers.onSpendUpdate({
          virtualKeyId: event.virtualKeyId,
          amount: event.amount,
          totalSpend: event.totalSpend,
          model: event.model,
          timestamp: event.timestamp,
        });
      }
    });
    this.cleanupFunctions.push(() => spendSub.unsubscribe());

    // Spend limit alerts
    const limitSub = await this.coreClient.notifications.onSpendLimitAlert((event) => {
      if (this.eventHandlers.onSpendLimitAlert) {
        this.eventHandlers.onSpendLimitAlert({
          virtualKeyId: event.virtualKeyId,
          currentSpend: event.currentSpend,
          limit: event.limit,
          percentage: event.percentage,
          alertLevel: event.alertLevel as 'warning' | 'critical',
        });
      }
    });
    this.cleanupFunctions.push(() => limitSub.unsubscribe());

    logger.info('Core event listeners setup complete');
  }

  // Set up Admin API event listeners
  private async setupAdminEventListeners(): Promise<void> {
    if (!this.adminClient) {
      throw new Error('Admin client not initialized');
    }

    logger.info('Setting up Admin event listeners');

    // Get SignalR service from admin client
    const signalRService = (this.adminClient as any).signalRService;
    if (!signalRService) {
      logger.warn('Admin client does not have SignalR service - notifications will not work');
      return;
    }

    // Get or create navigation state hub
    const navHub = await signalRService.getOrCreateNavigationStateHub();
    
    // Navigation state updates
    navHub.onNavigationStateUpdate((event) => {
      if (this.eventHandlers.onNavigationStateUpdate) {
        this.eventHandlers.onNavigationStateUpdate({
          type: event.entityType as 'model_mapping' | 'provider' | 'virtual_key',
          action: event.action as 'created' | 'updated' | 'deleted',
          data: event.entityData,
          timestamp: event.timestamp,
        });
      }
    });

    // Model discovery events
    navHub.onModelDiscovered((event) => {
      if (this.eventHandlers.onModelDiscovered) {
        this.eventHandlers.onModelDiscovered({
          providerId: event.providerId,
          providerName: event.providerName,
          modelsDiscovered: event.models || [],
          timestamp: event.timestamp,
        });
      }
    });

    // Provider health changes
    navHub.onProviderHealthChange((event) => {
      if (this.eventHandlers.onProviderHealthChange) {
        this.eventHandlers.onProviderHealthChange({
          providerId: event.providerId,
          providerName: event.providerName,
          status: event.healthStatus as 'healthy' | 'degraded' | 'unhealthy',
          latency: event.latency,
          error: event.error,
        });
      }
    });

    // Subscribe to all updates
    await navHub.subscribeToUpdates();
    
    // Add cleanup function
    this.cleanupFunctions.push(async () => {
      await navHub.unsubscribeFromUpdates();
    });

    // Get or create admin notification hub if available
    const adminHub = await signalRService.getOrCreateAdminNotificationHub?.();
    if (adminHub) {
      // Virtual key events
      if (adminHub.onVirtualKeyUpdate) {
        adminHub.onVirtualKeyUpdate((event) => {
          if (this.eventHandlers.onVirtualKeyUpdate) {
            this.eventHandlers.onVirtualKeyUpdate({
              keyId: event.keyId,
              action: event.action as 'created' | 'updated' | 'deleted' | 'disabled' | 'enabled',
              changes: event.changes,
            });
          }
        });
      }

      // Configuration changes
      if (adminHub.onConfigurationChange) {
        adminHub.onConfigurationChange((event) => {
          if (this.eventHandlers.onConfigurationChange) {
            this.eventHandlers.onConfigurationChange({
              category: event.category,
              setting: event.setting,
              oldValue: event.oldValue,
              newValue: event.newValue,
              timestamp: event.timestamp,
            });
          }
        });
      }

      // Subscribe to all admin updates
      if (adminHub.subscribeToUpdates) {
        await adminHub.subscribeToUpdates();
        this.cleanupFunctions.push(async () => {
          await adminHub.unsubscribeFromUpdates?.();
        });
      }
    }

    logger.info('Admin event listeners setup complete');
  }

  // Register event handlers
  on<K extends keyof SignalREventHandlers>(
    event: K,
    handler: SignalREventHandlers[K]
  ): void {
    this.eventHandlers[event] = handler;
  }

  // Remove event handlers
  off<K extends keyof SignalREventHandlers>(event: K): void {
    delete this.eventHandlers[event];
  }

  // Manual connection management
  async connect(): Promise<void> {
    const promises: Promise<void>[] = [];

    // Connect Core SignalR
    if (this.coreClient) {
      const signalR = (this.coreClient as any).signalr;
      if (signalR) {
        promises.push(signalR.startAllConnections());
      }
    }

    // Connect Admin SignalR
    if (this.adminClient) {
      const signalRService = (this.adminClient as any).signalRService;
      if (signalRService) {
        // Start navigation state hub
        const navHub = await signalRService.getOrCreateNavigationStateHub();
        if (navHub) {
          promises.push(navHub.start());
        }
        
        // Start admin notification hub if available
        const adminHub = await signalRService.getOrCreateAdminNotificationHub?.();
        if (adminHub) {
          promises.push(adminHub.start());
        }
      }
    }

    await Promise.all(promises);
    logger.info('SignalR connections started');
  }

  async disconnect(): Promise<void> {
    const promises: Promise<void>[] = [];

    // Disconnect Core SignalR
    if (this.coreClient) {
      const signalR = (this.coreClient as any).signalr;
      if (signalR) {
        promises.push(signalR.stopAllConnections());
      }
    }

    // Disconnect Admin SignalR
    if (this.adminClient) {
      const signalRService = (this.adminClient as any).signalRService;
      if (signalRService) {
        // Stop navigation state hub
        const navHub = await signalRService.getOrCreateNavigationStateHub();
        if (navHub) {
          promises.push(navHub.stop());
        }
        
        // Stop admin notification hub if available
        const adminHub = await signalRService.getOrCreateAdminNotificationHub?.();
        if (adminHub) {
          promises.push(adminHub.stop());
        }
      }
    }

    await Promise.all(promises);
    logger.info('SignalR connections stopped');
  }

  // Check connection status
  isConnected(): boolean {
    let connected = false;

    // Check Core SignalR status
    if (this.coreClient) {
      const signalR = (this.coreClient as any).signalr;
      if (signalR) {
        const statuses = signalR.getConnectionStatus?.();
        if (statuses) {
          connected = Object.values(statuses).some((status: any) => status === 'Connected');
        }
      }
    }

    // Check Admin SignalR status
    if (!connected && this.adminClient) {
      const signalRService = (this.adminClient as any).signalRService;
      if (signalRService) {
        const connectionStates = signalRService.getConnectionStates?.();
        if (connectionStates) {
          connected = Object.values(connectionStates).some((state: any) => state === 'Connected');
        }
      }
    }

    return connected;
  }

  // Clean up all resources
  async cleanup(): Promise<void> {
    // Run all cleanup functions
    this.cleanupFunctions.forEach(cleanup => cleanup());
    this.cleanupFunctions = [];

    // Disconnect clients
    await this.disconnect();

    // Clear references
    this.coreClient = undefined;
    this.adminClient = undefined;
    this.eventHandlers = {};
  }

  // Get current clients (for direct access if needed)
  getClients() {
    return {
      core: this.coreClient,
      admin: this.adminClient,
    };
  }
}

// Singleton instance
let signalRManager: SDKSignalRManager | null = null;

export function getSDKSignalRManager(config?: SDKSignalRConfig): SDKSignalRManager {
  if (!signalRManager && config) {
    signalRManager = new SDKSignalRManager(config);
  }
  
  if (!signalRManager) {
    throw new Error('SignalR manager not initialized. Provide config on first call.');
  }
  
  return signalRManager;
}

export async function cleanupSDKSignalRManager(): Promise<void> {
  if (signalRManager) {
    await signalRManager.cleanup();
    signalRManager = null;
  }
}