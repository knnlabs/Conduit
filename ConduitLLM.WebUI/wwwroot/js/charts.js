/**
 * Chart.js integration for ConduitLLM dashboards
 */

// Store chart instances to destroy them before recreating
const chartInstances = {};

/**
 * Renders a line chart for cost trends
 * @param {string} canvasId - The ID of the canvas element
 * @param {string[]} labels - The labels for the chart
 * @param {number[]} data - The data points for the chart
 */
function renderLineChart(canvasId, labels, data) {
  // Destroy previous chart instance if exists
  if (chartInstances[canvasId]) {
    chartInstances[canvasId].destroy();
  }

  const ctx = document.getElementById(canvasId).getContext('2d');
  
  chartInstances[canvasId] = new Chart(ctx, {
    type: 'line',
    data: {
      labels: labels,
      datasets: [{
        label: 'Cost ($)',
        data: data,
        borderColor: '#3498db',
        backgroundColor: 'rgba(52, 152, 219, 0.1)',
        borderWidth: 2,
        tension: 0.2,
        fill: true
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: 'top'
        },
        tooltip: {
          mode: 'index',
          intersect: false,
          callbacks: {
            label: function(context) {
              return `Cost: $${Number(context.raw).toFixed(4)}`;
            }
          }
        }
      },
      scales: {
        y: {
          beginAtZero: true,
          ticks: {
            callback: function(value) {
              return '$' + value.toFixed(2);
            }
          }
        }
      }
    }
  });
}

/**
 * Renders a pie chart for distribution
 * @param {string} canvasId - The ID of the canvas element
 * @param {string[]} labels - The labels for the chart
 * @param {number[]} data - The data points for the chart
 * @param {string[]} colors - The colors for each segment
 */
function renderPieChart(canvasId, labels, data, colors) {
  // Destroy previous chart instance if exists
  if (chartInstances[canvasId]) {
    chartInstances[canvasId].destroy();
  }

  const ctx = document.getElementById(canvasId).getContext('2d');
  
  chartInstances[canvasId] = new Chart(ctx, {
    type: 'pie',
    data: {
      labels: labels,
      datasets: [{
        data: data,
        backgroundColor: colors || [
          '#3498db',
          '#2ecc71',
          '#9b59b6',
          '#e74c3c',
          '#f39c12',
          '#1abc9c',
          '#34495e'
        ]
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          position: 'right',
          labels: {
            boxWidth: 12,
            font: {
              size: 11
            }
          }
        },
        tooltip: {
          callbacks: {
            label: function(context) {
              const label = context.label || '';
              const value = context.raw;
              const total = context.dataset.data.reduce((a, b) => a + b, 0);
              const percentage = total > 0 ? Math.round((value / total) * 100) : 0;
              return `${label}: $${value.toFixed(4)} (${percentage}%)`;
            }
          }
        }
      }
    }
  });
}

/**
 * Renders a bar chart
 * @param {string} canvasId - The ID of the canvas element
 * @param {string[]} labels - The labels for the chart
 * @param {number[]} data - The data points for the chart
 * @param {string} label - The label for the dataset
 */
function renderBarChart(canvasId, labels, data, label) {
  // Destroy previous chart instance if exists
  if (chartInstances[canvasId]) {
    chartInstances[canvasId].destroy();
  }

  const ctx = document.getElementById(canvasId).getContext('2d');
  
  chartInstances[canvasId] = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: labels,
      datasets: [{
        label: label || 'Value',
        data: data,
        backgroundColor: 'rgba(52, 152, 219, 0.7)',
        borderColor: '#3498db',
        borderWidth: 1
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: !!label,
          position: 'top'
        },
        tooltip: {
          mode: 'index',
          intersect: false
        }
      },
      scales: {
        y: {
          beginAtZero: true
        }
      }
    }
  });
}
