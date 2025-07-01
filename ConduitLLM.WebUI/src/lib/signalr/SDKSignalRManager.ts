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
      // TODO: SDK does not yet support signalR configuration
      // Need to handle SignalR connection separately
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
      // TODO: SDK does not yet support signalR configuration
      // Need to handle SignalR connection separately
    });

    // Set up Admin event listeners
    await this.setupAdminEventListeners();
  }

  // Set up Core API event listeners
  private async setupCoreEventListeners(): Promise<void> {
    // TODO: SDK does not yet support notifications API
    // The SDK needs to add:
    // - coreClient.notifications.onVideoProgress()
    // - coreClient.notifications.onImageProgress()
    // - coreClient.notifications.onSpendUpdate()
    // - coreClient.notifications.onSpendLimitAlert()
    
    // Stub implementation - no-op for now
    logger.info('Core event listeners setup skipped - SDK does not support notifications yet');
  }

  // Set up Admin API event listeners
  private async setupAdminEventListeners(): Promise<void> {
    // TODO: SDK does not yet support notifications API
    // The SDK needs to add:
    // - adminClient.notifications.onNavigationStateUpdate()
    // - adminClient.notifications.onModelDiscovered()
    // - adminClient.notifications.onProviderHealthChange()
    // - adminClient.notifications.onVirtualKeyEvent()
    // - adminClient.notifications.onConfigurationChange()
    
    // Stub implementation - no-op for now
    logger.info('Admin event listeners setup skipped - SDK does not support notifications yet');
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
    // TODO: SDK does not yet support signalR property
    // The SDK needs to add:
    // - coreClient.signalR.connect()
    // - adminClient.signalR.connect()
    
    // Stub implementation
    logger.info('SignalR connect called - SDK does not support SignalR yet');
  }

  async disconnect(): Promise<void> {
    // TODO: SDK does not yet support signalR property
    // The SDK needs to add:
    // - coreClient.signalR.disconnect()
    // - adminClient.signalR.disconnect()
    
    // Stub implementation
    logger.info('SignalR disconnect called - SDK does not support SignalR yet');
  }

  // Check connection status
  isConnected(): boolean {
    // TODO: SDK does not yet support signalR property
    // The SDK needs to add:
    // - coreClient.signalR.isConnected()
    // - adminClient.signalR.isConnected()
    
    // Stub implementation - always return false for now
    return false;
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