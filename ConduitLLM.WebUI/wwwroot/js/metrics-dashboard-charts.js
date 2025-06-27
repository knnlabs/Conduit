// Metrics Dashboard Chart Functions

// Store chart instances
const chartInstances = {};

// Initialize Chart.js with default options
Chart.defaults.font.family = '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
Chart.defaults.color = '#666';

// Update endpoint request rate chart
window.updateEndpointChart = function(chartId, data) {
    const ctx = document.getElementById(chartId);
    if (!ctx) return;

    // Destroy existing chart if it exists
    if (chartInstances[chartId]) {
        chartInstances[chartId].destroy();
    }

    const labels = data.map(d => d.endpoint);
    const values = data.map(d => d.rate);

    chartInstances[chartId] = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Requests/sec',
                data: values,
                backgroundColor: 'rgba(33, 150, 243, 0.6)',
                borderColor: 'rgba(33, 150, 243, 1)',
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    callbacks: {
                        label: function(context) {
                            return context.parsed.y.toFixed(1) + ' req/s';
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: function(value) {
                            return value + '/s';
                        }
                    }
                }
            }
        }
    });
};

// Update status code distribution chart
window.updateStatusCodeChart = function(chartId, data) {
    const ctx = document.getElementById(chartId);
    if (!ctx) return;

    // Destroy existing chart if it exists
    if (chartInstances[chartId]) {
        chartInstances[chartId].destroy();
    }

    const labels = data.map(d => d.code.toString());
    const values = data.map(d => d.count);
    const colors = data.map(d => {
        const code = d.code;
        if (code >= 200 && code < 300) return 'rgba(76, 175, 80, 0.6)';  // Success - green
        if (code >= 400 && code < 500) return 'rgba(255, 152, 0, 0.6)';  // Client error - orange
        if (code >= 500) return 'rgba(244, 67, 54, 0.6)';                // Server error - red
        return 'rgba(158, 158, 158, 0.6)';                               // Other - gray
    });

    chartInstances[chartId] = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: values,
                backgroundColor: colors,
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'right'
                },
                tooltip: {
                    callbacks: {
                        label: function(context) {
                            const label = context.label || '';
                            const value = context.parsed || 0;
                            const total = context.dataset.data.reduce((a, b) => a + b, 0);
                            const percentage = ((value / total) * 100).toFixed(1);
                            return `${label}: ${value} (${percentage}%)`;
                        }
                    }
                }
            }
        }
    });
};

// Update trend chart (line chart for historical data)
window.updateTrendChart = function(chartId, data, label) {
    const ctx = document.getElementById(chartId);
    if (!ctx) return;

    // Destroy existing chart if it exists
    if (chartInstances[chartId]) {
        chartInstances[chartId].destroy();
    }

    if (!data || data.length === 0) {
        // Show empty state
        ctx.parentElement.innerHTML = '<div class="text-center text-muted py-5">No data available</div>';
        return;
    }

    const chartData = {
        labels: data.map(d => new Date(d.timestamp).toLocaleTimeString()),
        datasets: [{
            label: label,
            data: data.map(d => d.value),
            borderColor: getColorForMetric(label),
            backgroundColor: getColorForMetric(label, 0.1),
            borderWidth: 2,
            tension: 0.1,
            fill: true
        }]
    };

    chartInstances[chartId] = new Chart(ctx, {
        type: 'line',
        data: chartData,
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false
            },
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    callbacks: {
                        label: function(context) {
                            let value = context.parsed.y;
                            if (label.includes('%')) {
                                return value.toFixed(1) + '%';
                            } else if (label.includes('$')) {
                                return '$' + value.toFixed(2);
                            } else if (label.includes('ms')) {
                                return value.toFixed(0) + 'ms';
                            }
                            return value.toFixed(1);
                        }
                    }
                }
            },
            scales: {
                x: {
                    display: true,
                    grid: {
                        display: false
                    }
                },
                y: {
                    display: true,
                    beginAtZero: true,
                    ticks: {
                        callback: function(value) {
                            if (label.includes('%')) {
                                return value + '%';
                            } else if (label.includes('$')) {
                                return '$' + value;
                            } else if (label.includes('ms')) {
                                return value + 'ms';
                            }
                            return value;
                        }
                    }
                }
            }
        }
    });
};

// Helper function to get color based on metric type
function getColorForMetric(label, alpha = 1) {
    if (label.toLowerCase().includes('error')) {
        return `rgba(244, 67, 54, ${alpha})`;  // Red
    } else if (label.toLowerCase().includes('cost')) {
        return `rgba(76, 175, 80, ${alpha})`;  // Green
    } else if (label.toLowerCase().includes('response')) {
        return `rgba(255, 152, 0, ${alpha})`;  // Orange
    } else {
        return `rgba(33, 150, 243, ${alpha})`;  // Blue
    }
}

// Clean up charts when navigating away
window.cleanupCharts = function() {
    Object.keys(chartInstances).forEach(key => {
        if (chartInstances[key]) {
            chartInstances[key].destroy();
            delete chartInstances[key];
        }
    });
};