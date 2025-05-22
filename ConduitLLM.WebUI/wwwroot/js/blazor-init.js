// Blazor initialization and connection management
window._blazorStarted = false;

// Wait for Blazor to be available
function waitForBlazor() {
    if (typeof Blazor !== 'undefined') {
        console.log('Blazor object detected, initializing...');
        initializeBlazor();
    } else {
        console.log('Waiting for Blazor...');
        setTimeout(waitForBlazor, 100);
    }
}

function initializeBlazor() {
    // Log Blazor availability
    console.log('Blazor is available:', Blazor);
    
    // Set up reconnection handlers
    if (Blazor.defaultReconnectionHandler) {
        Blazor.defaultReconnectionHandler._reconnectCallback = function() {
            console.log('Blazor reconnecting...');
        };
    }
    
    // Monitor connection state
    let checkConnectionInterval = setInterval(() => {
        if (window.Blazor && window.Blazor._internal && window.Blazor._internal.dotNetDispatcher) {
            console.log('Blazor circuit is active');
            window._blazorStarted = true;
            clearInterval(checkConnectionInterval);
            
            // Dispatch a custom event to signal Blazor is ready
            window.dispatchEvent(new Event('blazorReady'));
        }
    }, 500);
    
    // Add circuit handlers if available
    if (Blazor.start) {
        console.log('Starting Blazor with custom configuration...');
        Blazor.start({
            logLevel: 1, // Information level
            reconnectionOptions: {
                maxRetries: 10,
                retryIntervalMilliseconds: 2000
            }
        }).then(() => {
            console.log('Blazor started successfully');
            window._blazorStarted = true;
        }).catch(err => {
            console.error('Error starting Blazor:', err);
        });
    } else {
        console.log('Blazor auto-starting (no manual start required)');
        window._blazorStarted = true;
    }
}

// Listen for Blazor ready event
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOM loaded, waiting for Blazor...');
    waitForBlazor();
});

// Add global error handler for debugging
window.addEventListener('error', function(event) {
    if (event.filename && event.filename.includes('blazor')) {
        console.error('Blazor error:', event.message, 'at', event.filename, ':', event.lineno);
    }
});

// Expose diagnostic function
window.blazorDiagnostics = function() {
    return {
        blazorLoaded: typeof Blazor !== 'undefined',
        blazorStarted: window._blazorStarted,
        blazorInternal: window.Blazor && window.Blazor._internal ? 'Available' : 'Not available',
        signalR: window.signalR ? 'Loaded' : 'Not loaded',
        connection: window.Blazor && window.Blazor.defaultReconnectionHandler ? 'Handler available' : 'No handler'
    };
};