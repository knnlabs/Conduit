/**
 * Type-safe SignalR method arguments and return types
 */

/**
 * Valid types that can be sent through SignalR
 */
export type SignalRValue = 
  | string 
  | number 
  | boolean 
  | null 
  | undefined
  | SignalRObject
  | SignalRArray;

export interface SignalRObject {
  [key: string]: SignalRValue;
}

export type SignalRArray = Array<SignalRValue>;

/**
 * Type for SignalR method arguments
 */
export type SignalRArgs = SignalRValue[];

/**
 * Virtual key management hub methods
 */
export interface VirtualKeyManagementHubMethods {
  SubscribeToKeyEvents(keyId: number): Promise<void>;
  UnsubscribeFromKeyEvents(keyId: number): Promise<void>;
  SubscribeToAllKeyEvents(): Promise<void>;
  UnsubscribeFromAllKeyEvents(): Promise<void>;
}

/**
 * Virtual key management hub events
 */
export interface VirtualKeyManagementHubEvents {
  KeyCreated: (keyId: number, keyName: string, metadata?: string) => void;
  KeyUpdated: (keyId: number, keyName: string, metadata?: string) => void;
  KeyDeleted: (keyId: number, keyName: string) => void;
  KeyStatusChanged: (keyId: number, isEnabled: boolean) => void;
  SpendUpdated: (keyId: number, currentSpend: number, totalSpend: number) => void;
  BudgetWarning: (keyId: number, percentUsed: number, remainingBudget: number) => void;
  BudgetExceeded: (keyId: number, currentSpend: number, maxBudget: number) => void;
  KeyExpired: (keyId: number, keyName: string) => void;
  BudgetReset: (keyId: number, newPeriodStart: string) => void;
}

/**
 * Admin notification hub methods
 */
export interface AdminNotificationHubMethods {
  Subscribe(): Promise<void>;
  Unsubscribe(): Promise<void>;
  SubscribeToSystemEvents(): Promise<void>;
  UnsubscribeFromSystemEvents(): Promise<void>;
}

/**
 * Admin notification hub events
 */
export interface AdminNotificationHubEvents {
  ProviderHealthChanged: (provider: string, isHealthy: boolean, message?: string) => void;
  ModelAvailabilityChanged: (model: string, isAvailable: boolean) => void;
  SystemAlert: (severity: string, message: string, details?: string) => void;
  MaintenanceScheduled: (startTime: string, endTime: string, description: string) => void;
  PerformanceWarning: (metric: string, value: number, threshold: number) => void;
  ConfigurationChanged: (section: string, key: string, oldValue?: string, newValue?: string) => void;
}

/**
 * Type-safe hub connection interface
 */
export interface TypedHubConnection<TMethods, TEvents> {
  invoke<K extends keyof TMethods>(
    methodName: K,
    ...args: TMethods[K] extends (...args: infer P) => unknown ? P : never
  ): TMethods[K] extends (...args: unknown[]) => Promise<infer R> ? Promise<R> : Promise<void>;

  on<K extends keyof TEvents>(
    eventName: K,
    handler: TEvents[K]
  ): void;

  off<K extends keyof TEvents>(
    eventName: K,
    handler?: TEvents[K]
  ): void;
}