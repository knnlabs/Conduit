window.searchableDropdown = {
    addClickOutsideHandler: (dotNetRef, elementId) => {
        const element = document.getElementById(elementId);
        if (!element) return;

        const clickHandler = (event) => {
            // Check if the click was outside the dropdown
            if (!element.contains(event.target)) {
                dotNetRef.invokeMethodAsync('CloseDropdown');
            }
        };

        // Add event listener
        document.addEventListener('click', clickHandler);

        // Store the handler for cleanup
        element._clickHandler = clickHandler;
    },

    removeClickOutsideHandler: (elementId) => {
        const element = document.getElementById(elementId);
        if (!element || !element._clickHandler) return;

        document.removeEventListener('click', element._clickHandler);
        delete element._clickHandler;
    }
};