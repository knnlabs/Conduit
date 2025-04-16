// Initialize Bootstrap tooltips
window.initTooltips = function () {
    // Using timeout to ensure DOM is fully loaded
    setTimeout(() => {
        var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }, 100);
};

// Other site-wide JavaScript can be added here
