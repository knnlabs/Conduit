/**
 * SignalR to Blazor event adapter for handling DotNetObjectReference callbacks
 * This adapter maps SignalR events to Blazor JSInvokable methods
 */

window.SignalRBlazorAdapter = (function() {
    'use strict';

    // Use centralized event mappings
    const getMethodName = window.getSignalRHandlerName || function(eventName) { return eventName; };

    /**
     * Create a handler function that invokes the appropriate Blazor method
     * @param {Object} dotNetRef - The DotNetObjectReference
     * @param {string} eventName - The SignalR event name
     * @returns {Function} Handler function
     */
    function createBlazorHandler(dotNetRef, eventName) {
        const methodName = getMethodName(eventName);
        
        return async function(data) {
            try {
                // Convert the data to JSON if it's not already
                const jsonData = typeof data === 'string' ? JSON.parse(data) : data;
                
                // Invoke the Blazor method
                await dotNetRef.invokeMethodAsync(methodName, jsonData);
            } catch (error) {
                console.error(`[SignalRBlazorAdapter] Error invoking ${methodName}:`, error);
            }
        };
    }

    /**
     * Register a Blazor component to receive SignalR events
     * @param {Object} hub - The SignalR hub proxy
     * @param {string} eventName - The event name to listen for
     * @param {Object} dotNetRef - The DotNetObjectReference
     */
    function registerHandler(hub, eventName, dotNetRef) {
        const handler = createBlazorHandler(dotNetRef, eventName);
        hub.on(eventName, handler);
        return handler;
    }

    /**
     * Register multiple handlers at once
     * @param {Object} hub - The SignalR hub proxy
     * @param {Array<string>} eventNames - Array of event names
     * @param {Object} dotNetRef - The DotNetObjectReference
     */
    function registerHandlers(hub, eventNames, dotNetRef) {
        const handlers = {};
        eventNames.forEach(eventName => {
            handlers[eventName] = registerHandler(hub, eventName, dotNetRef);
        });
        return handlers;
    }

    // Extend hub proxy prototypes to support Blazor integration
    if (window.ConduitHubProxies) {
        const HubProxy = window.ConduitHubProxies.HubProxy;
        
        /**
         * Register a Blazor component as an event handler
         */
        HubProxy.prototype.onWithBlazor = function(eventName, dotNetRef) {
            const handler = createBlazorHandler(dotNetRef, eventName);
            this.on(eventName, handler);
            return handler;
        };
        
        /**
         * Register multiple Blazor handlers
         */
        HubProxy.prototype.registerBlazorHandlers = function(eventNames, dotNetRef) {
            return registerHandlers(this, eventNames, dotNetRef);
        };
    }

    return {
        createBlazorHandler,
        registerHandler,
        registerHandlers,
        eventMethodMappings
    };
})();