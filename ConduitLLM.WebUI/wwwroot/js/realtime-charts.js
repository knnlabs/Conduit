/**
 * Real-time chart updates for Conduit dashboards
 * Provides smooth animations and data updates for Chart.js charts
 */

window.RealtimeCharts = (function() {
    'use strict';

    // Store chart instances
    const charts = new Map();
    
    /**
     * Initialize or get a chart instance
     */
    function getOrCreateChart(canvasId, config) {
        if (charts.has(canvasId)) {
            return charts.get(canvasId);
        }
        
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error(`Canvas element not found: ${canvasId}`);
            return null;
        }
        
        const ctx = canvas.getContext('2d');
        const chart = new Chart(ctx, config);
        charts.set(canvasId, chart);
        
        return chart;
    }
    
    /**
     * Update spending chart with real-time data
     */
    function updateSpendingChart(canvasId, notification) {
        const chart = charts.get(canvasId);
        if (!chart) {
            console.warn(`Chart not found: ${canvasId}`);
            return;
        }
        
        try {
            // Get current date/time label
            const now = new Date();
            const label = now.toLocaleTimeString();
            
            // Add new data point
            const datasetIndex = 0; // Assuming single dataset for spending
            const dataset = chart.data.datasets[datasetIndex];
            
            if (dataset) {
                // Add new data point
                chart.data.labels.push(label);
                dataset.data.push(notification.totalSpend);
                
                // Keep only last 50 data points for performance
                const maxPoints = 50;
                if (chart.data.labels.length > maxPoints) {
                    chart.data.labels.shift();
                    dataset.data.shift();
                }
                
                // Update chart with animation
                chart.update('active');
                
                // Add visual indicator for the new point
                animateNewDataPoint(chart, datasetIndex, dataset.data.length - 1);
            }
        } catch (error) {
            console.error('Error updating spending chart:', error);
        }
    }
    
    /**
     * Add visual animation to new data point
     */
    function animateNewDataPoint(chart, datasetIndex, pointIndex) {
        const meta = chart.getDatasetMeta(datasetIndex);
        const point = meta.data[pointIndex];
        
        if (point) {
            // Store original radius
            const originalRadius = point.options.radius || 3;
            
            // Animate radius
            let frame = 0;
            const maxFrames = 20;
            const animate = () => {
                frame++;
                
                // Pulse effect
                const scale = 1 + Math.sin((frame / maxFrames) * Math.PI) * 0.5;
                point.options.radius = originalRadius * scale;
                
                chart.draw();
                
                if (frame < maxFrames) {
                    requestAnimationFrame(animate);
                } else {
                    // Reset to original
                    point.options.radius = originalRadius;
                    chart.draw();
                }
            };
            
            requestAnimationFrame(animate);
        }
    }
    
    /**
     * Initialize spending analytics chart
     */
    function initSpendingChart(canvasId, initialData = {}) {
        const config = {
            type: 'line',
            data: {
                labels: initialData.labels || [],
                datasets: [{
                    label: 'Total Spend ($)',
                    data: initialData.data || [],
                    borderColor: 'rgb(102, 126, 234)',
                    backgroundColor: 'rgba(102, 126, 234, 0.1)',
                    borderWidth: 3,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 4,
                    pointHoverRadius: 6,
                    pointBackgroundColor: 'rgb(102, 126, 234)',
                    pointBorderColor: '#fff',
                    pointBorderWidth: 2
                }]
            },
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
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        titleColor: '#fff',
                        bodyColor: '#fff',
                        borderColor: 'rgb(102, 126, 234)',
                        borderWidth: 1,
                        cornerRadius: 4,
                        padding: 10,
                        displayColors: false,
                        callbacks: {
                            label: function(context) {
                                return 'Spend: $' + context.parsed.y.toFixed(2);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            maxRotation: 0,
                            autoSkip: true,
                            maxTicksLimit: 10
                        }
                    },
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            callback: function(value) {
                                return '$' + value.toFixed(0);
                            }
                        }
                    }
                },
                animation: {
                    duration: 750,
                    easing: 'easeInOutQuart'
                }
            }
        };
        
        return getOrCreateChart(canvasId, config);
    }
    
    /**
     * Update budget progress charts
     */
    function updateBudgetProgress(keyId, percentage) {
        const progressBars = document.querySelectorAll(`[data-key-id="${keyId}"] .progress-bar`);
        
        progressBars.forEach(bar => {
            // Update width with animation
            bar.style.transition = 'width 0.6s ease-out';
            bar.style.width = `${Math.min(100, percentage)}%`;
            bar.setAttribute('aria-valuenow', percentage);
            
            // Update color class based on percentage
            bar.className = 'progress-bar';
            if (percentage >= 90) {
                bar.classList.add('bg-danger');
            } else if (percentage >= 75) {
                bar.classList.add('bg-warning');
            } else if (percentage >= 50) {
                bar.classList.add('bg-info');
            } else {
                bar.classList.add('bg-success');
            }
            
            // Add pulse animation
            bar.style.animation = 'pulse 0.6s ease-out';
            setTimeout(() => {
                bar.style.animation = '';
            }, 600);
        });
    }
    
    /**
     * Create real-time activity indicator
     */
    function createActivityIndicator(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;
        
        const indicator = document.createElement('div');
        indicator.className = 'realtime-activity-indicator';
        indicator.innerHTML = `
            <div class="activity-dot"></div>
            <span class="activity-text">Live</span>
        `;
        
        container.appendChild(indicator);
        
        // Pulse animation
        setInterval(() => {
            indicator.querySelector('.activity-dot').style.animation = 'pulse 1s ease-out';
            setTimeout(() => {
                indicator.querySelector('.activity-dot').style.animation = '';
            }, 1000);
        }, 3000);
    }
    
    /**
     * Handle chart resize
     */
    function handleResize() {
        charts.forEach(chart => {
            chart.resize();
        });
    }
    
    // Listen for window resize
    window.addEventListener('resize', debounce(handleResize, 250));
    
    /**
     * Debounce helper
     */
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }
    
    /**
     * Destroy all charts (cleanup)
     */
    function destroyAllCharts() {
        charts.forEach(chart => {
            chart.destroy();
        });
        charts.clear();
    }
    
    // Public API
    return {
        initSpendingChart,
        updateSpendingChart,
        updateBudgetProgress,
        createActivityIndicator,
        destroyAllCharts,
        getChart: (canvasId) => charts.get(canvasId)
    };
})();

// Export for module usage
export const updateChartData = window.RealtimeCharts.updateSpendingChart;
export const initChart = window.RealtimeCharts.initSpendingChart;
export const updateBudget = window.RealtimeCharts.updateBudgetProgress;