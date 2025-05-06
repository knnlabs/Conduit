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

// Bootstrap Modal Utilities
window.showModal = function (modalId) {
    const modalElement = document.getElementById(modalId);
    if (modalElement) {
        const bootstrapModal = new bootstrap.Modal(modalElement);
        bootstrapModal.show();
        document.body.classList.add('modal-open');
        return true;
    }
    return false;
};

window.hideModal = function (modalId) {
    const modalElement = document.getElementById(modalId);
    if (modalElement) {
        const bootstrapModal = bootstrap.Modal.getInstance(modalElement);
        if (bootstrapModal) {
            bootstrapModal.hide();
            document.body.classList.remove('modal-open');
            return true;
        }
    }
    return false;
};

// Helper for copy to clipboard functionality
window.copyToClipboard = function (text) {
    if (navigator && navigator.clipboard) {
        navigator.clipboard.writeText(text)
            .then(() => console.log('Copied to clipboard successfully'))
            .catch(err => console.error('Failed to copy text: ', err));
        return true;
    }
    return false;
};
