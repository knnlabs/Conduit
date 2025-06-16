// Blazor initialization helper for .NET 9
// In .NET 9, Blazor starts automatically, so we just monitor the connection

window._blazorStarted = false;

// Monitor Blazor connection state
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOM loaded, monitoring Blazor connection...');
    
    // Check for Blazor availability periodically
    let checkInterval = setInterval(() => {
        if (window.Blazor && window.Blazor._internal) {
            console.log('Blazor is ready and connected');
            window._blazorStarted = true;
            clearInterval(checkInterval);
            
            // Dispatch a custom event to signal Blazor is ready
            window.dispatchEvent(new Event('blazorReady'));
            
            // Set up reconnection handlers if available
            if (Blazor.defaultReconnectionHandler) {
                Blazor.defaultReconnectionHandler._reconnectCallback = function() {
                    console.log('Blazor reconnecting...');
                };
            }
        }
    }, 500);
    
    // Timeout after 10 seconds
    setTimeout(() => {
        if (!window._blazorStarted) {
            console.warn('Blazor connection check timed out');
            clearInterval(checkInterval);
        }
    }, 10000);
});

// Expose diagnostic function
window.blazorDiagnostics = function() {
    return {
        blazorLoaded: typeof Blazor !== 'undefined',
        blazorStarted: window._blazorStarted,
        blazorInternal: window.Blazor && window.Blazor._internal ? 'Available' : 'Not available',
        signalR: window.signalR ? 'Loaded' : 'Not loaded'
    };
};