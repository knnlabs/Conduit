// JavaScript functions for handling notification component behaviors

// Setup click outside listener for the notification panel
function setupClickOutsideListener(dotnetRef) {
    document.addEventListener('click', function(event) {
        // Check if the click is outside the notification container
        const container = document.querySelector('.notification-container');
        if (container && !container.contains(event.target)) {
            dotnetRef.invokeMethodAsync('ClosePanel');
        }
    });
}

// Remove click outside listener
function removeClickOutsideListener() {
    // Could be enhanced to use a named function instead of anonymous
    // This is a placeholder for proper cleanup
}
