/**
 * TypeScript definitions for ConduitSignalRService
 * Provides type safety and IntelliSense support for the SignalR service
 */

declare namespace ConduitSignalR {
    /**
     * Connection state enumeration
     */
    enum ConnectionState {
        DISCONNECTED = 'disconnected',
        CONNECTING = 'connecting', 
        CONNECTED = 'connected',
        RECONNECTING = 'reconnecting',
        FAILED = 'failed'
    }

    /**
     * Connection options interface
     */
    interface ConnectionOptions {
        maxReconnectAttempts?: number;
        baseReconnectDelay?: number;
        maxReconnectDelay?: number;
        enableMessageQueuing?: boolean;
        enableAutoReconnect?: boolean;
    }

    /**
     * Performance metric interface
     */
    interface PerformanceMetric {
        count: number;
        total: number;
        min: number;
        max: number;
        last: number;
        average: number;
    }

    /**
     * Hub metrics interface
     */
    interface HubMetrics {
        [metricName: string]: PerformanceMetric;
    }

    /**
     * State change event data
     */
    interface StateChangeEventData {
        hubName: string;
        previousState: string;
        currentState: string;
    }

    /**
     * Connection event data
     */
    interface ConnectionEventData {
        hubName: string;
        error?: any;
        connectionId?: string;
        reason?: string;
    }

    /**
     * Main ConduitSignalRService class
     */
    class ConduitSignalRService {
        /**
         * Connection state enumeration
         */
        static readonly ConnectionState: typeof ConnectionState;

        /**
         * Get singleton instance
         */
        static getInstance(): ConduitSignalRService;

        /**
         * Set debug mode
         */
        setDebugMode(enabled: boolean): void;

        /**
         * Set virtual key for authentication
         */
        setVirtualKey(key: string): void;

        /**
         * Connect to a SignalR hub
         */
        connectToHub(
            hubName: string, 
            virtualKey?: string | null, 
            options?: ConnectionOptions
        ): Promise<signalR.HubConnection>;

        /**
         * Disconnect from a hub
         */
        disconnectFromHub(hubName: string): Promise<void>;

        /**
         * Get connection for a specific hub
         */
        getConnection(hubName: string): signalR.HubConnection | null;

        /**
         * Check if a hub is connected
         */
        isConnected(hubName: string): boolean;

        /**
         * Get connection state for a hub
         */
        getConnectionState(hubName: string): string;

        /**
         * Register an event handler for a hub
         */
        on(hubName: string, eventName: string, handler: (...args: any[]) => void): void;

        /**
         * Remove an event handler
         */
        off(hubName: string, eventName: string, handler: (...args: any[]) => void): void;

        /**
         * Invoke a hub method
         */
        invoke<T = any>(hubName: string, methodName: string, ...args: any[]): Promise<T>;

        /**
         * Send a message to a hub (fire-and-forget)
         */
        send(hubName: string, methodName: string, ...args: any[]): Promise<void>;

        /**
         * Get performance metrics for a hub
         */
        getMetrics(hubName: string): HubMetrics;

        /**
         * Get all active connections
         */
        getActiveConnections(): string[];

        /**
         * Disconnect from all hubs
         */
        disconnectAll(): Promise<void>;
    }

    /**
     * Typed hub proxy interfaces for common hubs
     */
    
    /**
     * Spend Notifications Hub proxy
     */
    interface SpendNotificationsHub {
        on(event: 'SpendUpdate', handler: (notification: SpendUpdateNotification) => void): void;
        on(event: 'BudgetAlert', handler: (alert: BudgetAlertNotification) => void): void;
        on(event: 'SpendSummary', handler: (summary: SpendSummaryNotification) => void): void;
        on(event: 'UnusualSpendingDetected', handler: (notification: UnusualSpendingNotification) => void): void;
    }

    /**
     * Video Generation Hub proxy
     */
    interface VideoGenerationHub {
        on(event: 'TaskStarted', handler: (taskId: string, prompt: string) => void): void;
        on(event: 'TaskProgress', handler: (taskId: string, progress: number, message?: string) => void): void;
        on(event: 'TaskCompleted', handler: (taskId: string, result: VideoGenerationResult) => void): void;
        on(event: 'TaskFailed', handler: (taskId: string, error: string) => void): void;
        invoke(method: 'SubscribeToTask', taskId: string): Promise<void>;
        invoke(method: 'UnsubscribeFromTask', taskId: string): Promise<void>;
    }

    /**
     * Image Generation Hub proxy
     */
    interface ImageGenerationHub {
        on(event: 'TaskStarted', handler: (taskId: string, prompt: string) => void): void;
        on(event: 'TaskProgress', handler: (taskId: string, progress: number, message?: string) => void): void;
        on(event: 'TaskCompleted', handler: (taskId: string, result: ImageGenerationResult) => void): void;
        on(event: 'TaskFailed', handler: (taskId: string, error: string) => void): void;
        invoke(method: 'SubscribeToTask', taskId: string): Promise<void>;
        invoke(method: 'UnsubscribeFromTask', taskId: string): Promise<void>;
    }

    /**
     * Webhook Delivery Hub proxy
     */
    interface WebhookDeliveryHub {
        on(event: 'DeliveryAttempted', handler: (attempt: WebhookDeliveryAttempt) => void): void;
        on(event: 'DeliverySucceeded', handler: (success: WebhookDeliverySuccess) => void): void;
        on(event: 'DeliveryFailed', handler: (failure: WebhookDeliveryFailure) => void): void;
        on(event: 'RetryScheduled', handler: (retry: WebhookRetryInfo) => void): void;
        on(event: 'DeliveryStatisticsUpdated', handler: (stats: WebhookStatistics) => void): void;
        on(event: 'CircuitBreakerStateChanged', handler: (state: WebhookCircuitBreakerState) => void): void;
        invoke(method: 'SubscribeToWebhooks', webhookUrls: string[]): Promise<void>;
        invoke(method: 'UnsubscribeFromWebhooks', webhookUrls: string[]): Promise<void>;
        invoke(method: 'RequestStatistics'): Promise<void>;
    }

    /**
     * DTO interfaces
     */
    interface SpendUpdateNotification {
        timestamp: string;
        newSpend: number;
        totalSpend: number;
        remainingBudget?: number;
        budgetPercentage?: number;
        budget?: number;
        model?: string;
        taskType?: string;
    }

    interface BudgetAlertNotification {
        percentage: number;
        remaining: number;
        severity: string;
        message: string;
        budgetPeriodEnd: string;
    }

    interface SpendSummaryNotification {
        periodType: string;
        totalSpend: number;
        modelBreakdown: { [model: string]: number };
        topModels: Array<{ model: string; spend: number; percentage: number }>;
        periodStart: string;
        periodEnd: string;
    }

    interface UnusualSpendingNotification {
        patternType: string;
        currentSpend: number;
        averageSpend: number;
        percentageIncrease: number;
        timeframe: string;
        recommendations: string[];
    }

    interface VideoGenerationResult {
        videoUrl: string;
        thumbnailUrl?: string;
        duration?: number;
        resolution?: string;
    }

    interface ImageGenerationResult {
        imageUrl: string;
        width?: number;
        height?: number;
        revisedPrompt?: string;
    }

    interface WebhookDeliveryAttempt {
        webhookId: string;
        webhookUrl: string;
        taskId: string;
        taskType: string;
        eventType: string;
        attemptNumber: number;
        timestamp: string;
    }

    interface WebhookDeliverySuccess {
        webhookId: string;
        webhookUrl: string;
        taskId: string;
        statusCode: number;
        responseTimeMs: number;
        totalAttempts: number;
        timestamp: string;
    }

    interface WebhookDeliveryFailure {
        webhookId: string;
        webhookUrl: string;
        taskId: string;
        errorMessage: string;
        statusCode?: number;
        attemptNumber: number;
        isPermanentFailure: boolean;
        nextRetryTime?: string;
        timestamp: string;
    }

    interface WebhookRetryInfo {
        webhookId: string;
        webhookUrl: string;
        taskId: string;
        retryNumber: number;
        maxRetries: number;
        scheduledTime: string;
        delaySeconds: number;
    }

    interface WebhookStatistics {
        totalAttempts: number;
        successfulDeliveries: number;
        failedDeliveries: number;
        pendingRetries: number;
        averageResponseTimeMs: number;
        successRate: number;
        lastUpdateTime: string;
        urlStatistics: WebhookUrlStatistics[];
    }

    interface WebhookUrlStatistics {
        webhookUrl: string;
        totalAttempts: number;
        successfulDeliveries: number;
        failedDeliveries: number;
        averageResponseTimeMs: number;
        lastAttemptTime?: string;
        lastSuccessTime?: string;
        lastFailureTime?: string;
    }

    interface WebhookCircuitBreakerState {
        webhookUrl: string;
        state: string;
        previousState: string;
        reason: string;
        failureCount: number;
        lastFailureTime?: string;
        willRetryAt?: string;
    }
}

/**
 * Global instance declaration
 */
declare const conduitSignalR: ConduitSignalR.ConduitSignalRService;

/**
 * Window augmentation
 */
interface Window {
    ConduitSignalRService: typeof ConduitSignalR.ConduitSignalRService;
    conduitSignalR: ConduitSignalR.ConduitSignalRService;
}