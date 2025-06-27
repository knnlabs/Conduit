window.SecurityDashboard = {
    threatTrendsChart: null,

    initializeThreatTrendsChart: function(canvasId) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        // Destroy existing chart if it exists
        if (this.threatTrendsChart) {
            this.threatTrendsChart.destroy();
        }

        // Generate sample data for last 24 hours
        const labels = [];
        const authFailures = [];
        const rateLimits = [];
        const suspicious = [];
        
        for (let i = 23; i >= 0; i--) {
            const hour = new Date();
            hour.setHours(hour.getHours() - i);
            labels.push(hour.getHours() + ':00');
            
            // Generate random data for demo
            authFailures.push(Math.floor(Math.random() * 20));
            rateLimits.push(Math.floor(Math.random() * 15));
            suspicious.push(Math.floor(Math.random() * 10));
        }

        this.threatTrendsChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Auth Failures',
                    data: authFailures,
                    borderColor: '#dc3545',
                    backgroundColor: 'rgba(220, 53, 69, 0.1)',
                    tension: 0.3
                }, {
                    label: 'Rate Limits',
                    data: rateLimits,
                    borderColor: '#ffc107',
                    backgroundColor: 'rgba(255, 193, 7, 0.1)',
                    tension: 0.3
                }, {
                    label: 'Suspicious Activity',
                    data: suspicious,
                    borderColor: '#fd7e14',
                    backgroundColor: 'rgba(253, 126, 20, 0.1)',
                    tension: 0.3
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                    },
                    title: {
                        display: false
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            stepSize: 5
                        }
                    }
                }
            }
        });
    },

    updateThreatTrendsChart: function(data) {
        if (!this.threatTrendsChart) return;

        // Update chart data
        this.threatTrendsChart.data.labels = data.labels;
        this.threatTrendsChart.data.datasets[0].data = data.authFailures;
        this.threatTrendsChart.data.datasets[1].data = data.rateLimits;
        this.threatTrendsChart.data.datasets[2].data = data.suspicious;
        this.threatTrendsChart.update();
    },

    dispose: function() {
        if (this.threatTrendsChart) {
            this.threatTrendsChart.destroy();
            this.threatTrendsChart = null;
        }
    }
};