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
     * Navigation State Hub Proxy
     */
    class NavigationStateHub extends HubProxy {
        constructor(signalRService) {
            super('navigation-state', signalRService);
        }

        /**
         * Register for navigation state update events
         */
        onNavigationStateUpdated(handler) {
            this.on('NavigationStateUpdated', handler);
        }

        /**
         * Register for model mapping update events
         */
        onModelMappingUpdated(handler) {
            this.on('ModelMappingUpdated', handler);
        }

        /**
         * Register for provider health update events
         */
        onProviderHealthUpdated(handler) {
            this.on('ProviderHealthUpdated', handler);
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
         * Create navigation state hub proxy
         */
        createNavigationStateHub() {
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
        NavigationStateHub,
        TaskHub,
        HubProxyFactory
    };
})();

// Create global factory instance
window.conduitHubs = new window.ConduitHubProxies.HubProxyFactory();