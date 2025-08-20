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

// AdminNotificationHub interfaces removed - hub has been deprecated

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