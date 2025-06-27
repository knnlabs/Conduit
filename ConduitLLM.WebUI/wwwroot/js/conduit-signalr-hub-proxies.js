/**
 * Typed hub proxy helpers for ConduitSignalRService
 * Provides strongly-typed wrappers for common SignalR hubs
 */

window.ConduitHubProxies = (function() {
    'use strict';

    /**
     * Base hub proxy class
     */
    class HubProxy {
        constructor(hubName, signalRService) {
            this.hubName = hubName;
            this.signalRService = signalRService || window.conduitSignalR;
            this._handlers = new Map();
        }

        /**
         * Connect to the hub
         */
        async connect(virtualKey, options) {
            return await this.signalRService.connectToHub(this.hubName, virtualKey, options);
        }

        /**
         * Disconnect from the hub
         */
        async disconnect() {
            return await this.signalRService.disconnectFromHub(this.hubName);
        }

        /**
         * Check if connected
         */
        isConnected() {
            return this.signalRService.isConnected(this.hubName);
        }

        /**
         * Get connection state
         */
        getState() {
            return this.signalRService.getConnectionState(this.hubName);
        }

        /**
         * Register event handler
         */
        on(eventName, handler) {
            this.signalRService.on(this.hubName, eventName, handler);
            
            // Track handlers for cleanup
            if (!this._handlers.has(eventName)) {
                this._handlers.set(eventName, []);
            }
            this._handlers.get(eventName).push(handler);
        }

        /**
         * Remove event handler
         */
        off(eventName, handler) {
            this.signalRService.off(this.hubName, eventName, handler);
            
            // Remove from tracked handlers
            const handlers = this._handlers.get(eventName);
            if (handlers) {
                const index = handlers.indexOf(handler);
                if (index > -1) {
                    handlers.splice(index, 1);
                }
            }
        }

        /**
         * Remove all handlers for an event
         */
        offAll(eventName) {
            const handlers = this._handlers.get(eventName);
            if (handlers) {
                handlers.forEach(handler => this.off(eventName, handler));
                this._handlers.delete(eventName);
            }
        }

        /**
         * Invoke hub method
         */
        async invoke(methodName, ...args) {
            return await this.signalRService.invoke(this.hubName, methodName, ...args);
        }

        /**
         * Send to hub (fire-and-forget)
         */
        async send(methodName, ...args) {
            return await this.signalRService.send(this.hubName, methodName, ...args);
        }
    }

    /**
     * Spend Notifications Hub Proxy
     */
    class SpendNotificationsHub extends HubProxy {
        constructor(signalRService) {
            super('spend-notifications', signalRService);
        }

        /**
         * Register for spend update notifications
         */
        onSpendUpdate(handler) {
            this.on('SpendUpdate', handler);
        }

        /**
         * Register for budget alert notifications
         */
        onBudgetAlert(handler) {
            this.on('BudgetAlert', handler);
        }

        /**
         * Register for spend summary notifications
         */
        onSpendSummary(handler) {
            this.on('SpendSummary', handler);
        }

        /**
         * Register for unusual spending notifications
         */
        onUnusualSpending(handler) {
            this.on('UnusualSpendingDetected', handler);
        }
    }

    /**
     * Video Generation Hub Proxy
     */
    class VideoGenerationHub extends HubProxy {
        constructor(signalRService) {
            super('video-generation', signalRService);
        }

        /**
         * Subscribe to a specific task
         */
        async subscribeToTask(taskId) {
            return await this.invoke('SubscribeToTask', taskId);
        }

        /**
         * Unsubscribe from a task
         */
        async unsubscribeFromTask(taskId) {
            return await this.invoke('UnsubscribeFromTask', taskId);
        }

        /**
         * Register for task started events
         */
        onTaskStarted(handler) {
            this.on('TaskStarted', handler);
        }

        /**
         * Register for task progress events
         */
        onTaskProgress(handler) {
            this.on('TaskProgress', handler);
        }

        /**
         * Register for task completed events
         */
        onTaskCompleted(handler) {
            this.on('TaskCompleted', handler);
        }

        /**
         * Register for task failed events
         */
        onTaskFailed(handler) {
            this.on('TaskFailed', handler);
        }

        /**
         * Register for task cancelled events
         */
        onTaskCancelled(handler) {
            this.on('TaskCancelled', handler);
        }

        /**
         * Register for task timeout events
         */
        onTaskTimedOut(handler) {
            this.on('TaskTimedOut', handler);
        }
    }

    /**
     * Image Generation Hub Proxy
     */
    class ImageGenerationHub extends HubProxy {
        constructor(signalRService) {
            super('image-generation', signalRService);
        }

        /**
         * Subscribe to a specific task
         */
        async subscribeToTask(taskId) {
            return await this.invoke('SubscribeToTask', taskId);
        }

        /**
         * Unsubscribe from a task
         */
        async unsubscribeFromTask(taskId) {
            return await this.invoke('UnsubscribeFromTask', taskId);
        }

        /**
         * Register for task started events
         */
        onTaskStarted(handler) {
            this.on('TaskStarted', handler);
        }

        /**
         * Register for task progress events
         */
        onTaskProgress(handler) {
            this.on('TaskProgress', handler);
        }

        /**
         * Register for task completed events
         */
        onTaskCompleted(handler) {
            this.on('TaskCompleted', handler);
        }

        /**
         * Register for task failed events
         */
        onTaskFailed(handler) {
            this.on('TaskFailed', handler);
        }

        /**
         * Register for task cancelled events
         */
        onTaskCancelled(handler) {
            this.on('TaskCancelled', handler);
        }
    }

    /**
     * Webhook Delivery Hub Proxy
     */
    class WebhookDeliveryHub extends HubProxy {
        constructor(signalRService) {
            super('webhooks', signalRService);
        }

        /**
         * Subscribe to webhook URLs
         */
        async subscribeToWebhooks(webhookUrls) {
            return await this.invoke('SubscribeToWebhooks', webhookUrls);
        }

        /**
         * Unsubscribe from webhook URLs
         */
        async unsubscribeFromWebhooks(webhookUrls) {
            return await this.invoke('UnsubscribeFromWebhooks', webhookUrls);
        }

        /**
         * Request current statistics
         */
        async requestStatistics() {
            return await this.invoke('RequestStatistics');
        }

        /**
         * Register for delivery attempt events
         */
        onDeliveryAttempted(handler) {
            this.on('DeliveryAttempted', handler);
        }

        /**
         * Register for delivery success events
         */
        onDeliverySucceeded(handler) {
            this.on('DeliverySucceeded', handler);
        }

        /**
         * Register for delivery failure events
         */
        onDeliveryFailed(handler) {
            this.on('DeliveryFailed', handler);
        }

        /**
         * Register for retry scheduled events
         */
        onRetryScheduled(handler) {
            this.on('RetryScheduled', handler);
        }

        /**
         * Register for statistics update events
         */
        onStatisticsUpdated(handler) {
            this.on('DeliveryStatisticsUpdated', handler);
        }

        /**
         * Register for circuit breaker state change events
         */
        onCircuitBreakerStateChanged(handler) {
            this.on('CircuitBreakerStateChanged', handler);
        }
    }

    /**
     * System Notification Hub Proxy (replaces NavigationStateHub)
     */
    class SystemNotificationHub extends HubProxy {
        constructor(signalRService) {
            super('notifications', signalRService);
        }

        /**
         * Register for provider health changed events
         */
        onProviderHealthChanged(handler) {
            this.on('OnProviderHealthChanged', handler);
        }

        /**
         * Register for model mapping changed events
         */
        onModelMappingChanged(handler) {
            this.on('OnModelMappingChanged', handler);
        }

        /**
         * Register for model capabilities discovered events
         */
        onModelCapabilitiesDiscovered(handler) {
            this.on('OnModelCapabilitiesDiscovered', handler);
        }

        /**
         * Register for model availability changed events
         */
        onModelAvailabilityChanged(handler) {
            this.on('OnModelAvailabilityChanged', handler);
        }

        /**
         * Register for rate limit warning events
         */
        onRateLimitWarning(handler) {
            this.on('OnRateLimitWarning', handler);
        }

        /**
         * Register for system announcement events
         */
        onSystemAnnouncement(handler) {
            this.on('OnSystemAnnouncement', handler);
        }

        /**
         * Register for service degraded events
         */
        onServiceDegraded(handler) {
            this.on('OnServiceDegraded', handler);
        }

        /**
         * Register for service restored events
         */
        onServiceRestored(handler) {
            this.on('OnServiceRestored', handler);
        }
    }

    /**
     * Navigation State Hub Proxy (deprecated - use SystemNotificationHub)
     * Maintained for backward compatibility
     */
    class NavigationStateHub extends SystemNotificationHub {
        constructor(signalRService) {
            super(signalRService);
            console.warn('NavigationStateHub is deprecated. Use SystemNotificationHub instead.');
        }
    }

    /**
     * Admin Notification Hub Proxy (requires master key authentication)
     */
    class AdminNotificationHub extends HubProxy {
        constructor(signalRService) {
            super('admin-notifications', signalRService);
        }

        /**
         * Connect with master key authentication
         */
        async connect(masterKey, options) {
            // Override to use master key in appropriate header/query
            const connectOptions = {
                ...options,
                headers: {
                    'X-API-Key': masterKey,
                    ...options?.headers
                }
            };
            return await this.signalRService.connectToHub(this.hubName, masterKey, connectOptions);
        }

        /**
         * Subscribe to a specific virtual key
         */
        async subscribeToVirtualKey(virtualKeyId) {
            return await this.invoke('SubscribeToVirtualKey', virtualKeyId);
        }

        /**
         * Unsubscribe from a virtual key
         */
        async unsubscribeFromVirtualKey(virtualKeyId) {
            return await this.invoke('UnsubscribeFromVirtualKey', virtualKeyId);
        }

        /**
         * Subscribe to a specific provider
         */
        async subscribeToProvider(providerName) {
            return await this.invoke('SubscribeToProvider', providerName);
        }

        /**
         * Unsubscribe from a provider
         */
        async unsubscribeFromProvider(providerName) {
            return await this.invoke('UnsubscribeFromProvider', providerName);
        }

        /**
         * Request provider health refresh
         */
        async refreshProviderHealth() {
            return await this.invoke('RefreshProviderHealth');
        }

        /**
         * Register for virtual key update events
         */
        onVirtualKeyUpdate(handler) {
            this.on('VirtualKeyUpdate', handler);
        }

        /**
         * Register for provider health update events
         */
        onProviderHealthUpdate(handler) {
            this.on('ProviderHealthUpdate', handler);
        }

        /**
         * Register for high spend alert events
         */
        onHighSpendAlert(handler) {
            this.on('HighSpendAlert', handler);
        }

        /**
         * Register for security alert events
         */
        onSecurityAlert(handler) {
            this.on('SecurityAlert', handler);
        }

        /**
         * Register for system announcement events
         */
        onSystemAnnouncement(handler) {
            this.on('SystemAnnouncement', handler);
        }

        /**
         * Register for model capability update events
         */
        onModelCapabilityUpdate(handler) {
            this.on('ModelCapabilityUpdate', handler);
        }

        /**
         * Also support standard system notification events
         */
        onProviderHealthChanged(handler) {
            this.on('ProviderHealthUpdate', handler);
        }

        onModelCapabilitiesDiscovered(handler) {
            this.on('ModelCapabilityUpdate', handler);
        }

        onModelMappingChanged(handler) {
            this.on('OnModelMappingChanged', handler);
        }
    }

    /**
     * Task Hub Proxy (unified async operations)
     */
    class TaskHub extends HubProxy {
        constructor(signalRService) {
            super('tasks', signalRService);
        }

        /**
         * Subscribe to a specific task
         */
        async subscribeToTask(taskId) {
            return await this.invoke('SubscribeToTask', taskId);
        }

        /**
         * Unsubscribe from a task
         */
        async unsubscribeFromTask(taskId) {
            return await this.invoke('UnsubscribeFromTask', taskId);
        }

        /**
         * Subscribe to all tasks of a type
         */
        async subscribeToTaskType(taskType) {
            return await this.invoke('SubscribeToTaskType', taskType);
        }

        /**
         * Unsubscribe from task type
         */
        async unsubscribeFromTaskType(taskType) {
            return await this.invoke('UnsubscribeFromTaskType', taskType);
        }

        /**
         * Register for task started events
         */
        onTaskStarted(handler) {
            this.on('TaskStarted', handler);
        }

        /**
         * Register for task progress events
         */
        onTaskProgress(handler) {
            this.on('TaskProgress', handler);
        }

        /**
         * Register for task completed events
         */
        onTaskCompleted(handler) {
            this.on('TaskCompleted', handler);
        }

        /**
         * Register for task failed events
         */
        onTaskFailed(handler) {
            this.on('TaskFailed', handler);
        }

        /**
         * Register for task cancelled events
         */
        onTaskCancelled(handler) {
            this.on('TaskCancelled', handler);
        }

        /**
         * Register for task timeout events
         */
        onTaskTimedOut(handler) {
            this.on('TaskTimedOut', handler);
        }
    }

    /**
     * Factory for creating hub proxies
     */
    class HubProxyFactory {
        constructor(signalRService) {
            this.signalRService = signalRService || window.conduitSignalR;
        }

        /**
         * Create spend notifications hub proxy
         */
        createSpendNotificationsHub() {
            return new SpendNotificationsHub(this.signalRService);
        }

        /**
         * Create video generation hub proxy
         */
        createVideoGenerationHub() {
            return new VideoGenerationHub(this.signalRService);
        }

        /**
         * Create image generation hub proxy
         */
        createImageGenerationHub() {
            return new ImageGenerationHub(this.signalRService);
        }

        /**
         * Create webhook delivery hub proxy
         */
        createWebhookDeliveryHub() {
            return new WebhookDeliveryHub(this.signalRService);
        }

        /**
         * Create system notification hub proxy (replaces navigation state hub)
         */
        createSystemNotificationHub() {
            return new SystemNotificationHub(this.signalRService);
        }

        /**
         * Create admin notification hub proxy (requires master key)
         */
        createAdminNotificationHub() {
            return new AdminNotificationHub(this.signalRService);
        }

        /**
         * Create navigation state hub proxy (deprecated - use createSystemNotificationHub)
         */
        createNavigationStateHub() {
            console.warn('createNavigationStateHub is deprecated. Use createSystemNotificationHub instead.');
            return new NavigationStateHub(this.signalRService);
        }

        /**
         * Create task hub proxy
         */
        createTaskHub() {
            return new TaskHub(this.signalRService);
        }

        /**
         * Create generic hub proxy
         */
        createHub(hubName) {
            return new HubProxy(hubName, this.signalRService);
        }
    }

    // Export classes
    return {
        HubProxy,
        SpendNotificationsHub,
        VideoGenerationHub,
        ImageGenerationHub,
        WebhookDeliveryHub,
        SystemNotificationHub,
        AdminNotificationHub,
        NavigationStateHub,
        TaskHub,
        HubProxyFactory
    };
})();

// Create global factory instance
window.conduitHubs = new window.ConduitHubProxies.HubProxyFactory();