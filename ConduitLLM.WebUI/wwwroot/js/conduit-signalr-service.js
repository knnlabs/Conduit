/**
 * ConduitSignalRService - Centralized SignalR connection management for Conduit
 * Provides a unified interface for managing multiple SignalR hub connections
 * with automatic reconnection, state management, and error handling.
 */

window.ConduitSignalRService = (function() {
    'use strict';

    // Private instance
    let instance = null;

    // Connection states enum
    const ConnectionState = {
        DISCONNECTED: 'disconnected',
        CONNECTING: 'connecting',
        CONNECTED: 'connected',
        RECONNECTING: 'reconnecting',
        FAILED: 'failed'
    };

    /**
     * ConduitSignalRService class
     * Manages multiple SignalR hub connections with advanced features
     */
    class ConduitSignalRService {
        constructor() {
            if (instance) {
                return instance;
            }

            this.connections = new Map();
            this.connectionStates = new Map();
            this.reconnectTimers = new Map();
            this.eventHandlers = new Map();
            this.messageQueues = new Map();
            this.debugMode = false;
            this.virtualKey = null;
            this.baseUrl = window.conduitConfig?.apiBaseUrl || 'http://localhost:5000';
            
            // Performance monitoring
            this.performanceMetrics = new Map();
            
            // Connection options defaults
            this.defaultOptions = {
                maxReconnectAttempts: 10,
                baseReconnectDelay: 5000,
                maxReconnectDelay: 30000,
                enableMessageQueuing: true,
                enableAutoReconnect: true
            };

            instance = this;
        }

        /**
         * Get singleton instance
         */
        static getInstance() {
            if (!instance) {
                instance = new ConduitSignalRService();
            }
            return instance;
        }

        /**
         * Set debug mode
         * @param {boolean} enabled - Enable or disable debug logging
         */
        setDebugMode(enabled) {
            this.debugMode = enabled;
            if (enabled) {
                console.log('[ConduitSignalR] Debug mode enabled');
            }
        }

        /**
         * Set virtual key for authentication
         * @param {string} key - Virtual key for API authentication
         */
        setVirtualKey(key) {
            this.virtualKey = key;
            this.masterKey = null; // Clear master key when setting virtual key
            this._log('Virtual key updated');
        }

        /**
         * Set master key for authentication (admin hubs)
         * @param {string} key - Master key for admin authentication
         */
        setMasterKey(key) {
            this.masterKey = key;
            this.virtualKey = null; // Clear virtual key when setting master key
            this._log('Master key updated');
        }

        /**
         * Connect to a SignalR hub
         * @param {string} hubName - Name of the hub (e.g., 'video-generation', 'spend-notifications')
         * @param {string} virtualKey - Optional virtual key override
         * @param {Object} options - Connection options
         * @returns {Promise<signalR.HubConnection>} The hub connection
         */
        async connectToHub(hubName, virtualKey = null, options = {}) {
            try {
                this._log(`Connecting to hub: ${hubName}`);
                
                // Check if already connected
                if (this.isConnected(hubName)) {
                    this._log(`Hub ${hubName} is already connected`);
                    return this.connections.get(hubName);
                }

                // Merge options with defaults
                const hubOptions = { ...this.defaultOptions, ...options };
                
                // Check if this is an admin hub
                const isAdminHub = hubName === 'admin-notifications';
                
                // Build hub URL
                // Admin hubs use the admin API base URL
                const baseUrl = isAdminHub && window.conduitConfig?.adminApiBaseUrl 
                    ? window.conduitConfig.adminApiBaseUrl 
                    : this.baseUrl;
                const hubUrl = `${baseUrl}/hubs/${hubName}`;
                
                // Configure connection options
                // For admin hubs, use master key if available
                const authKey = isAdminHub && this.masterKey ? this.masterKey : (virtualKey || this.virtualKey);
                const connectionOptions = this._buildConnectionOptions(authKey, isAdminHub);
                
                // Create connection
                const connection = new signalR.HubConnectionBuilder()
                    .withUrl(hubUrl, connectionOptions)
                    .withAutomaticReconnect(hubOptions.enableAutoReconnect ? this._getReconnectPolicy(hubOptions) : false)
                    .configureLogging(this.debugMode ? signalR.LogLevel.Debug : signalR.LogLevel.Information)
                    .build();

                // Store connection and initial state
                this.connections.set(hubName, connection);
                this._updateConnectionState(hubName, ConnectionState.CONNECTING);
                
                // Initialize message queue if enabled
                if (hubOptions.enableMessageQueuing) {
                    this.messageQueues.set(hubName, []);
                }

                // Setup connection event handlers
                this._setupConnectionHandlers(hubName, connection, hubOptions);
                
                // Start performance tracking
                const startTime = performance.now();
                
                // Start connection
                await connection.start();
                
                // Record connection time
                const connectionTime = performance.now() - startTime;
                this._recordMetric(hubName, 'connectionTime', connectionTime);
                
                this._updateConnectionState(hubName, ConnectionState.CONNECTED);
                this._log(`Connected to hub ${hubName} in ${connectionTime.toFixed(2)}ms`);
                
                // Process any queued messages
                await this._processMessageQueue(hubName);
                
                return connection;
                
            } catch (error) {
                this._logError(`Failed to connect to hub ${hubName}:`, error);
                this._updateConnectionState(hubName, ConnectionState.FAILED);
                throw error;
            }
        }

        /**
         * Disconnect from a hub
         * @param {string} hubName - Name of the hub to disconnect from
         * @returns {Promise<void>}
         */
        async disconnectFromHub(hubName) {
            try {
                const connection = this.connections.get(hubName);
                if (!connection) {
                    this._log(`No connection found for hub: ${hubName}`);
                    return;
                }

                this._log(`Disconnecting from hub: ${hubName}`);
                
                // Cancel any reconnect timers
                const timer = this.reconnectTimers.get(hubName);
                if (timer) {
                    clearTimeout(timer);
                    this.reconnectTimers.delete(hubName);
                }

                // Stop the connection
                await connection.stop();
                
                // Clean up
                this.connections.delete(hubName);
                this.connectionStates.delete(hubName);
                this.messageQueues.delete(hubName);
                this.eventHandlers.delete(hubName);
                this.performanceMetrics.delete(hubName);
                
                this._log(`Disconnected from hub: ${hubName}`);
                
            } catch (error) {
                this._logError(`Error disconnecting from hub ${hubName}:`, error);
                throw error;
            }
        }

        /**
         * Get connection for a specific hub
         * @param {string} hubName - Name of the hub
         * @returns {signalR.HubConnection|null} The hub connection or null
         */
        getConnection(hubName) {
            return this.connections.get(hubName) || null;
        }

        /**
         * Check if a hub is connected
         * @param {string} hubName - Name of the hub
         * @returns {boolean} True if connected
         */
        isConnected(hubName) {
            const connection = this.connections.get(hubName);
            return connection?.state === signalR.HubConnectionState.Connected;
        }

        /**
         * Get connection state for a hub
         * @param {string} hubName - Name of the hub
         * @returns {string} Current connection state
         */
        getConnectionState(hubName) {
            const connection = this.connections.get(hubName);
            if (!connection) return 'Disconnected';
            
            switch (connection.state) {
                case signalR.HubConnectionState.Connected:
                    return 'Connected';
                case signalR.HubConnectionState.Connecting:
                    return 'Connecting';
                case signalR.HubConnectionState.Reconnecting:
                    return 'Reconnecting';
                case signalR.HubConnectionState.Disconnected:
                default:
                    return 'Disconnected';
            }
        }
        
        /**
         * Ping a hub to measure latency
         * @param {string} hubName - Hub name
         * @returns {Promise<void>}
         */
        async ping(hubName) {
            const connection = this.connections.get(hubName);
            if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
                throw new Error(`Hub ${hubName} is not connected`);
            }
            
            // Most SignalR hubs don't have a ping method, so we'll use a lightweight operation
            // or just resolve immediately to measure round-trip time
            try {
                await connection.invoke('echo', 'ping');
            } catch (error) {
                // If echo doesn't exist, just resolve to measure connection overhead
                await Promise.resolve();
            }
        }

        /**
         * Register an event handler for a hub
         * @param {string} hubName - Name of the hub
         * @param {string} eventName - Name of the event
         * @param {Function} handler - Event handler function
         */
        on(hubName, eventName, handler) {
            const connection = this.connections.get(hubName);
            if (!connection) {
                this._logError(`Cannot register handler - no connection for hub: ${hubName}`);
                return;
            }

            // Store handler reference for cleanup
            if (!this.eventHandlers.has(hubName)) {
                this.eventHandlers.set(hubName, new Map());
            }
            
            const hubHandlers = this.eventHandlers.get(hubName);
            if (!hubHandlers.has(eventName)) {
                hubHandlers.set(eventName, []);
            }
            
            hubHandlers.get(eventName).push(handler);
            connection.on(eventName, handler);
            
            this._log(`Registered handler for ${hubName}.${eventName}`);
        }

        /**
         * Remove an event handler
         * @param {string} hubName - Name of the hub
         * @param {string} eventName - Name of the event
         * @param {Function} handler - Event handler function to remove
         */
        off(hubName, eventName, handler) {
            const connection = this.connections.get(hubName);
            if (!connection) {
                return;
            }

            connection.off(eventName, handler);
            
            // Remove from stored handlers
            const hubHandlers = this.eventHandlers.get(hubName);
            if (hubHandlers?.has(eventName)) {
                const handlers = hubHandlers.get(eventName);
                const index = handlers.indexOf(handler);
                if (index > -1) {
                    handlers.splice(index, 1);
                }
            }
            
            this._log(`Removed handler for ${hubName}.${eventName}`);
        }

        /**
         * Invoke a hub method
         * @param {string} hubName - Name of the hub
         * @param {string} methodName - Name of the method to invoke
         * @param {...any} args - Method arguments
         * @returns {Promise<any>} Method result
         */
        async invoke(hubName, methodName, ...args) {
            const connection = this.connections.get(hubName);
            
            if (!connection) {
                throw new Error(`No connection found for hub: ${hubName}`);
            }

            // Check if connected
            if (connection.state !== signalR.HubConnectionState.Connected) {
                // Queue message if enabled
                if (this.messageQueues.has(hubName)) {
                    this._log(`Queueing message for ${hubName}.${methodName} (not connected)`);
                    this.messageQueues.get(hubName).push({ methodName, args });
                    return;
                } else {
                    throw new Error(`Hub ${hubName} is not connected`);
                }
            }

            try {
                const startTime = performance.now();
                const result = await connection.invoke(methodName, ...args);
                const duration = performance.now() - startTime;
                
                this._recordMetric(hubName, `invoke.${methodName}`, duration);
                this._log(`Invoked ${hubName}.${methodName} in ${duration.toFixed(2)}ms`);
                
                return result;
            } catch (error) {
                this._logError(`Error invoking ${hubName}.${methodName}:`, error);
                throw error;
            }
        }

        /**
         * Send a message to a hub (fire-and-forget)
         * @param {string} hubName - Name of the hub
         * @param {string} methodName - Name of the method
         * @param {...any} args - Method arguments
         * @returns {Promise<void>}
         */
        async send(hubName, methodName, ...args) {
            const connection = this.connections.get(hubName);
            
            if (!connection) {
                throw new Error(`No connection found for hub: ${hubName}`);
            }

            // Check if connected
            if (connection.state !== signalR.HubConnectionState.Connected) {
                // Queue message if enabled
                if (this.messageQueues.has(hubName)) {
                    this._log(`Queueing message for ${hubName}.${methodName} (not connected)`);
                    this.messageQueues.get(hubName).push({ methodName, args, isInvoke: false });
                    return;
                } else {
                    throw new Error(`Hub ${hubName} is not connected`);
                }
            }

            try {
                await connection.send(methodName, ...args);
                this._log(`Sent message to ${hubName}.${methodName}`);
            } catch (error) {
                this._logError(`Error sending to ${hubName}.${methodName}:`, error);
                throw error;
            }
        }

        /**
         * Get performance metrics for a hub
         * @param {string} hubName - Name of the hub
         * @returns {Object} Performance metrics
         */
        getMetrics(hubName) {
            return this.performanceMetrics.get(hubName) || {};
        }

        /**
         * Get all active connections
         * @returns {Array<string>} Array of hub names with active connections
         */
        getActiveConnections() {
            return Array.from(this.connections.keys()).filter(hub => this.isConnected(hub));
        }

        /**
         * Disconnect from all hubs
         * @returns {Promise<void>}
         */
        async disconnectAll() {
            const disconnectPromises = Array.from(this.connections.keys()).map(hub => 
                this.disconnectFromHub(hub).catch(error => 
                    this._logError(`Error disconnecting from ${hub}:`, error)
                )
            );
            
            await Promise.all(disconnectPromises);
            this._log('Disconnected from all hubs');
        }

        // Private helper methods

        /**
         * Build connection options
         */
        _buildConnectionOptions(authKey, isMasterKey = false) {
            const options = {};
            
            if (authKey) {
                if (isMasterKey) {
                    // For master key authentication, use X-API-Key header
                    options.accessTokenFactory = () => authKey;
                    options.headers = {
                        'X-API-Key': authKey
                    };
                } else {
                    // For virtual key authentication, use Bearer token
                    options.accessTokenFactory = () => authKey;
                    options.headers = {
                        'Authorization': `Bearer ${authKey}`
                    };
                }
            }
            
            return options;
        }

        /**
         * Get reconnect policy
         */
        _getReconnectPolicy(options) {
            return {
                nextRetryDelayInMilliseconds: retryContext => {
                    if (retryContext.previousRetryCount >= options.maxReconnectAttempts) {
                        return null; // Stop reconnecting
                    }
                    
                    const delay = Math.min(
                        options.baseReconnectDelay * Math.pow(2, retryContext.previousRetryCount),
                        options.maxReconnectDelay
                    );
                    
                    this._log(`Next reconnect attempt in ${delay}ms (attempt ${retryContext.previousRetryCount + 1})`);
                    return delay;
                }
            };
        }

        /**
         * Setup connection event handlers
         */
        _setupConnectionHandlers(hubName, connection, options) {
            // Connection closed handler
            connection.onclose(async error => {
                this._log(`Connection closed for hub ${hubName}`, error);
                this._updateConnectionState(hubName, ConnectionState.DISCONNECTED);
                
                // Emit custom event
                this._emitEvent(hubName, 'connectionClosed', { error });
                
                // Handle reconnection if not a manual disconnect
                if (error && options.enableAutoReconnect) {
                    this._scheduleReconnect(hubName, options);
                }
            });

            // Reconnecting handler
            connection.onreconnecting(error => {
                this._log(`Reconnecting to hub ${hubName}`, error);
                this._updateConnectionState(hubName, ConnectionState.RECONNECTING);
                this._emitEvent(hubName, 'reconnecting', { error });
            });

            // Reconnected handler
            connection.onreconnected(connectionId => {
                this._log(`Reconnected to hub ${hubName} with connectionId: ${connectionId}`);
                this._updateConnectionState(hubName, ConnectionState.CONNECTED);
                this._emitEvent(hubName, 'reconnected', { connectionId });
                
                // Process queued messages
                this._processMessageQueue(hubName);
            });
        }

        /**
         * Schedule reconnection attempt
         */
        _scheduleReconnect(hubName, options, attemptNumber = 0) {
            if (attemptNumber >= options.maxReconnectAttempts) {
                this._log(`Max reconnection attempts reached for hub ${hubName}`);
                this._updateConnectionState(hubName, ConnectionState.FAILED);
                this._emitEvent(hubName, 'connectionFailed', { reason: 'Max attempts reached' });
                return;
            }

            const delay = Math.min(
                options.baseReconnectDelay * Math.pow(2, attemptNumber),
                options.maxReconnectDelay
            );

            this._log(`Scheduling reconnect for hub ${hubName} in ${delay}ms (attempt ${attemptNumber + 1})`);
            
            const timer = setTimeout(async () => {
                try {
                    await this.connectToHub(hubName, this.virtualKey, options);
                } catch (error) {
                    this._logError(`Reconnect attempt ${attemptNumber + 1} failed for hub ${hubName}:`, error);
                    this._scheduleReconnect(hubName, options, attemptNumber + 1);
                }
            }, delay);

            this.reconnectTimers.set(hubName, timer);
        }

        /**
         * Process queued messages
         */
        async _processMessageQueue(hubName) {
            const queue = this.messageQueues.get(hubName);
            if (!queue || queue.length === 0) {
                return;
            }

            this._log(`Processing ${queue.length} queued messages for hub ${hubName}`);
            
            while (queue.length > 0) {
                const message = queue.shift();
                try {
                    if (message.isInvoke !== false) {
                        await this.invoke(hubName, message.methodName, ...message.args);
                    } else {
                        await this.send(hubName, message.methodName, ...message.args);
                    }
                } catch (error) {
                    this._logError(`Error processing queued message for ${hubName}.${message.methodName}:`, error);
                }
            }
        }

        /**
         * Update connection state
         */
        _updateConnectionState(hubName, state) {
            const previousState = this.connectionStates.get(hubName);
            this.connectionStates.set(hubName, state);
            
            if (previousState !== state) {
                this._emitEvent(hubName, 'stateChanged', { 
                    previousState, 
                    currentState: state 
                });
            }
        }

        /**
         * Emit custom event
         */
        _emitEvent(hubName, eventName, data) {
            // Add reconnect attempt info if available
            const timer = this.reconnectTimers.get(hubName);
            if (timer && eventName === 'stateChanged' && data.currentState === 'Reconnecting') {
                data.reconnectAttempt = this._getReconnectAttempt(hubName);
                data.nextRetryTime = this._getNextRetryTime(hubName);
            }
            
            // Create custom event
            const event = new CustomEvent(`conduit:${hubName}:${eventName}`, {
                detail: { hubName, ...data }
            });
            
            window.dispatchEvent(event);
        }
        
        /**
         * Get reconnect attempt number (approximation)
         */
        _getReconnectAttempt(hubName) {
            // This is a simplified version - in a real implementation,
            // you'd track this more precisely
            const metrics = this.performanceMetrics.get(hubName);
            return metrics?.connectionTime?.count || 1;
        }
        
        /**
         * Get next retry time in seconds
         */
        _getNextRetryTime(hubName) {
            // Return a default value - in real implementation, 
            // this would calculate based on retry policy
            return 5;
        }

        /**
         * Record performance metric
         */
        _recordMetric(hubName, metricName, value) {
            if (!this.performanceMetrics.has(hubName)) {
                this.performanceMetrics.set(hubName, {});
            }
            
            const metrics = this.performanceMetrics.get(hubName);
            
            if (!metrics[metricName]) {
                metrics[metricName] = {
                    count: 0,
                    total: 0,
                    min: Infinity,
                    max: -Infinity,
                    last: 0
                };
            }
            
            const metric = metrics[metricName];
            metric.count++;
            metric.total += value;
            metric.min = Math.min(metric.min, value);
            metric.max = Math.max(metric.max, value);
            metric.last = value;
            metric.average = metric.total / metric.count;
        }

        /**
         * Log message (debug mode)
         */
        _log(...args) {
            if (this.debugMode) {
                console.log('[ConduitSignalR]', ...args);
            }
        }

        /**
         * Log error
         */
        _logError(...args) {
            console.error('[ConduitSignalR]', ...args);
        }
    }

    // Expose ConnectionState enum
    ConduitSignalRService.ConnectionState = ConnectionState;

    return ConduitSignalRService;
})();

// Create and expose global instance
window.conduitSignalR = window.ConduitSignalRService.getInstance();