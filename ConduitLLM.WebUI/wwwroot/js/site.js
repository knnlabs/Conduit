// This file contains general JavaScript utility functions for the application

// Hide loading screen when Blazor is ready
document.addEventListener('DOMContentLoaded', function () {
    setTimeout(function () {
        // Add the blazor-ready class for CSS transitions
        document.body.classList.add('blazor-ready');
        
        // Hide the loading screen
        const loadingElement = document.getElementById('app-loading');
        if (loadingElement) {
            loadingElement.style.display = 'none';
        }
    }, 500);
});
