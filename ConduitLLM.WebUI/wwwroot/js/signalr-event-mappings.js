/**
 * SignalR Event Mappings - Single Source of Truth
 * 
 * This file contains all mappings between backend SignalR event names
 * and frontend JavaScript/Blazor handler methods.
 * 
 * IMPORTANT: This is the ONLY place where event mappings should be defined.
 * All other mapping logic should reference this file.
 */

window.SignalREventMappings = {
    // Spend Notification Events
    'SpendUpdate': 'HandleSpendUpdate',
    'BudgetAlert': 'HandleBudgetAlert',
    'SpendSummary': 'HandleSpendSummary',
    'UnusualSpendingDetected': 'HandleUnusualSpending',
    
    // Admin Notification Events
    'VirtualKeyUpdate': 'HandleVirtualKeyUpdate',
    'ProviderHealthUpdate': 'HandleProviderHealthChanged',
    'HighSpendAlert': 'HandleHighSpendAlert',
    'SecurityAlert': 'HandleSecurityAlert',
    'ModelCapabilityUpdate': 'HandleModelDiscovered',
    
    // System Notification Events
    'ProviderHealthChanged': 'HandleProviderHealthChanged',
    'OnProviderHealthChanged': 'HandleProviderHealthChanged',
    'OnModelCapabilitiesDiscovered': 'HandleModelDiscovered',
    'OnModelMappingChanged': 'HandleConfigurationChanged',
    'OnModelAvailabilityChanged': 'HandleModelAvailabilityChanged',
    'OnRateLimitWarning': 'HandleRateLimitWarning',
    'OnSystemAnnouncement': 'HandleSystemAlert',
    'OnServiceDegraded': 'HandleServiceDegraded',
    'OnServiceRestored': 'HandleServiceRestored',
    'SystemAnnouncement': 'HandleSystemAlert',
    
    // Task Events (Generic)
    'TaskStarted': 'OnTaskStarted',
    'TaskProgress': 'OnTaskProgress',
    'TaskCompleted': 'OnTaskCompleted',
    'TaskFailed': 'OnTaskFailed',
    'TaskCancelled': 'OnTaskCancelled',
    'TaskTimedOut': 'OnTaskTimedOut',
    
    // Video Generation Events
    'VideoGenerationStarted': 'OnVideoGenerationStarted',
    'VideoGenerationProgress': 'OnVideoGenerationProgress',
    'VideoGenerationCompleted': 'OnVideoGenerationCompleted',
    'VideoGenerationFailed': 'OnVideoGenerationFailed',
    'VideoGenerationCancelled': 'OnVideoGenerationCancelled',
    
    // Image Generation Events
    'ImageGenerationStarted': 'OnImageGenerationStarted',
    'ImageGenerationProgress': 'OnImageGenerationProgress',
    'ImageGenerationCompleted': 'OnImageGenerationCompleted',
    'ImageGenerationFailed': 'OnImageGenerationFailed',
    'ImageGenerationCancelled': 'OnImageGenerationCancelled',
    
    // Webhook Delivery Events
    'DeliveryAttempted': 'HandleDeliveryAttempted',
    'DeliverySucceeded': 'HandleDeliverySucceeded',
    'DeliveryFailed': 'HandleDeliveryFailed',
    'RetryScheduled': 'HandleRetryScheduled',
    'DeliveryStatisticsUpdated': 'HandleStatisticsUpdated',
    'CircuitBreakerStateChanged': 'HandleCircuitBreakerStateChanged'
};

/**
 * Get the handler method name for a given event
 * @param {string} eventName - The backend event name
 * @returns {string} The frontend handler method name
 */
window.getSignalRHandlerName = function(eventName) {
    return window.SignalREventMappings[eventName] || eventName;
};

/**
 * Check if an event has a mapped handler
 * @param {string} eventName - The backend event name
 * @returns {boolean} True if a mapping exists
 */
window.hasSignalRMapping = function(eventName) {
    return window.SignalREventMappings.hasOwnProperty(eventName);
};