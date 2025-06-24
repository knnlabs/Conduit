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

// Helper for downloading images
window.downloadImage = function (imageUrl, fileName) {
    fetch(imageUrl)
        .then(response => response.blob())
        .then(blob => {
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.style.display = 'none';
            a.href = url;
            a.download = fileName || 'image.png';
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);
        })
        .catch(err => console.error('Failed to download image: ', err));
};

// Helper for downloading videos
window.downloadVideo = function (videoUrl, fileName) {
    fetch(videoUrl)
        .then(response => response.blob())
        .then(blob => {
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.style.display = 'none';
            a.href = url;
            a.download = fileName || 'video.mp4';
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);
        })
        .catch(err => console.error('Failed to download video: ', err));
};
