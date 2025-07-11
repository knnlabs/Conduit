// Temporary type augmentation for incomplete SDK
// This file should be removed once the SDK is updated with all services

import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

declare module '@knn_labs/conduit-admin-client' {
  interface ConduitAdminClient {
    providers: {
      list(): Promise<any>;
      get(id: string): Promise<any>;
      create(data: any): Promise<any>;
      update(id: string, data: any): Promise<any>;
      delete(id: string): Promise<void>;
    };
    providerModels: {
      getProviderModels(providerId: string): Promise<any>;
    };
    modelMappings: {
      list(): Promise<any>;
      get(id: string): Promise<any>;
      create(data: any): Promise<any>;
      update(id: string, data: any): Promise<any>;
      delete(id: string): Promise<void>;
      test(id: string): Promise<any>;
      discover(): Promise<any>;
    };
    system: {
      getSystemInfo(): Promise<any>;
      getHealth(): Promise<any>;
    };
    providerHealth: {
      getProviderHealth(providerId: string): Promise<any>;
      getHealthSummary(): Promise<any>;
    };
    analytics: {
      getRequestLogs(params?: any): Promise<any>;
      getUsageAnalytics(params?: any): Promise<any>;
      getVirtualKeyAnalytics(params?: any): Promise<any>;
    };
    settings: {
      get(key: string): Promise<any>;
      update(key: string, value: any): Promise<any>;
      batchUpdate(settings: any): Promise<any>;
    };
  }
}

// Note: This is a temporary workaround until the SDK is updated
// The actual SDK only has virtualKeys and dashboard services