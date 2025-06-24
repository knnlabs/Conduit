// Navigation utilities for Conduit WebUI

export function navigateToProviderDetails(providerId) {
    // Navigate to model mappings page with provider filter
    window.location.href = `/model-mappings?provider=${providerId}`;
}

export function scrollToTop() {
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

export function openInNewTab(url) {
    window.open(url, '_blank');
}

export function copyToClipboard(text) {
    navigator.clipboard.writeText(text).then(function() {
        console.log('Text copied to clipboard');
    }, function(err) {
        console.error('Could not copy text: ', err);
    });
}

export function showTooltip(element, message) {
    if (element && typeof bootstrap !== 'undefined') {
        const tooltip = new bootstrap.Tooltip(element, {
            title: message,
            trigger: 'manual'
        });
        tooltip.show();
        setTimeout(() => tooltip.hide(), 2000);
    }
}