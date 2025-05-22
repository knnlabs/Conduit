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

  const canvas = document.getElementById(canvasId);
  if (!canvas) {
    console.warn(`Canvas element with id ${canvasId} not found. Chart rendering skipped.`);
    return;
  }
  
  const ctx = canvas.getContext('2d');
  
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

  const canvas = document.getElementById(canvasId);
  if (!canvas) {
    console.warn(`Canvas element with id ${canvasId} not found. Chart rendering skipped.`);
    return;
  }
  
  const ctx = canvas.getContext('2d');
  
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
 * @param {string} xAxisLabel - Label for the X axis
 * @param {string} yAxisLabel - Label for the Y axis
 * @param {string} orientation - 'vertical' or 'horizontal'
 */
function renderBarChart(canvasId, labels, data, label, xAxisLabel, yAxisLabel, orientation) {
  // Destroy previous chart instance if exists
  if (chartInstances[canvasId]) {
    chartInstances[canvasId].destroy();
  }

  const canvas = document.getElementById(canvasId);
  if (!canvas) {
    console.warn(`Canvas element with id ${canvasId} not found. Chart rendering skipped.`);
    return;
  }
  
  const ctx = canvas.getContext('2d');
  const isHorizontal = orientation === 'horizontal';
  
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
      indexAxis: isHorizontal ? 'y' : 'x',
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
        x: {
          beginAtZero: true,
          title: {
            display: !!xAxisLabel,
            text: xAxisLabel || ''
          }
        },
        y: {
          beginAtZero: true,
          title: {
            display: !!yAxisLabel,
            text: yAxisLabel || ''
          }
        }
      }
    }
  });
}

/**
 * Renders a stacked bar chart using raw chart data object
 * @param {string} canvasId - The ID of the canvas element
 * @param {string} chartDataJson - JSON string of chart data object
 * @param {string} title - Title for the chart
 * @param {string} xAxisLabel - Label for the X axis
 * @param {string} yAxisLabel - Label for the Y axis
 */
function renderStackedBarChart(canvasId, chartDataJson, title, xAxisLabel, yAxisLabel) {
  // Destroy previous chart instance if exists
  if (chartInstances[canvasId]) {
    chartInstances[canvasId].destroy();
  }

  const canvas = document.getElementById(canvasId);
  if (!canvas) {
    console.warn(`Canvas element with id ${canvasId} not found. Chart rendering skipped.`);
    return;
  }
  
  const ctx = canvas.getContext('2d');
  const chartData = typeof chartDataJson === 'string' ? JSON.parse(chartDataJson) : chartDataJson;
  
  chartInstances[canvasId] = new Chart(ctx, {
    type: 'bar',
    data: chartData,
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        title: {
          display: !!title,
          text: title || ''
        },
        legend: {
          position: 'top',
        },
        tooltip: {
          mode: 'index',
          intersect: false
        }
      },
      scales: {
        x: {
          stacked: true,
          title: {
            display: !!xAxisLabel,
            text: xAxisLabel || ''
          }
        },
        y: {
          stacked: true,
          beginAtZero: true,
          title: {
            display: !!yAxisLabel,
            text: yAxisLabel || ''
          }
        }
      }
    }
  });
}

/**
 * Destroys all active chart instances
 */
function destroyCharts() {
  Object.keys(chartInstances).forEach(canvasId => {
    if (chartInstances[canvasId]) {
      chartInstances[canvasId].destroy();
      delete chartInstances[canvasId];
    }
  });
}
