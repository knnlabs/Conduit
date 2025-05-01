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

// Initialize Bootstrap components like dropdowns
window.initBootstrapComponents = function () {
    // Using timeout to ensure DOM is fully loaded
    setTimeout(() => {
        // Initialize dropdowns
        var dropdownTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="dropdown"]'));
        var dropdownList = dropdownTriggerList.map(function (dropdownTriggerEl) {
            return new bootstrap.Dropdown(dropdownTriggerEl);
        });
        
        // Initialize any other Bootstrap components as needed
        console.log('Bootstrap components initialized');
    }, 100);
};

// Handle clicks outside dropdown
window.addClickOutsideHandler = function (dropdownId, dotnetRef) {
    document.addEventListener('click', function (event) {
        // Check if the click is outside both the dropdown button and menu
        const container = document.querySelector('.quick-setup-container');
        
        if (container && !container.contains(event.target)) {
            dotnetRef.invokeMethodAsync('CloseQuickSetupDropdown');
        }
    });
};

// Other site-wide JavaScript can be added here
