# VirtualKeysDashboard User Guide

## Overview

The VirtualKeysDashboard provides a comprehensive interface for monitoring and managing virtual API keys in ConduitLLM. This guide will help you navigate its features and make the most of its capabilities.

## Accessing the Dashboard

1. Log in to your ConduitLLM WebUI
2. Navigate to the "Virtual Keys" section
3. Click on "Dashboard" in the submenu

## Dashboard Components

### Key Status Overview

The dashboard's main panel displays a summary of all virtual keys with status indicators:

- **Active Keys**: Currently enabled and operational keys
- **Disabled Keys**: Keys that have been manually disabled
- **Expired Keys**: Keys that have passed their expiration date
- **Budget Limited Keys**: Keys that have reached one or more budget limits

Each key is color-coded based on its status for quick visual assessment.

### Usage Statistics

The usage statistics section provides visualizations of:

- **Token Consumption**: Bar charts showing input and output token usage by key
- **Cost Distribution**: Pie charts showing cost breakdown by model and key
- **Historical Trends**: Line graphs showing usage patterns over time
- **Budget Utilization**: Progress bars showing current spend against budgets

### Key Performance Metrics

For each key, the dashboard displays key performance indicators:

- **Average Cost Per Request**: The mean cost across all requests
- **Request Volume**: Total number of requests made with the key
- **Success Rate**: Percentage of requests that returned a 200 status code
- **Average Latency**: Mean response time for requests

## Interactive Features

### Filtering and Sorting

Use the dashboard controls to:

- **Filter by Date Range**: View data for specific time periods
- **Filter by Key**: Focus on one or multiple specific keys
- **Filter by Model**: See usage for particular models
- **Sort by Metrics**: Arrange data by cost, usage, or other criteria

### Alerts and Notifications

The dashboard includes an alerts panel that displays:

- **Budget Warnings**: When keys approach or exceed budget limits
- **Expiration Alerts**: When keys are nearing their expiration date
- **Usage Anomalies**: Unusual patterns in API usage
- **System Notices**: Important information about the ConduitLLM system

### Data Export

Export dashboard data in various formats:

1. Click the "Export" button in the top-right corner
2. Select your preferred format (CSV, JSON, PDF)
3. Choose the data range and metrics to include
4. Click "Download"

## Budget Management

### Monitoring Budgets

The budget monitoring panel shows:

- **Daily Budget**: Usage against daily limits with reset countdown
- **Monthly Budget**: Usage against monthly limits with reset countdown
- **Total Budget**: Lifetime usage against total budget limits

### Budget Adjustments

To adjust a key's budget:

1. Select the key from the list
2. Click "Edit Budget"
3. Enter new budget values
4. Click "Save Changes"

Changes take effect immediately.

## Usage Analysis

### Request Breakdown

The request breakdown section provides detailed insights:

- **Endpoint Distribution**: Which API endpoints are being used
- **Model Usage**: Distribution of requests across different models
- **Token Efficiency**: Analysis of token usage efficiency
- **Time-of-Day Patterns**: When API usage peaks and ebbs

### Cost Analysis

The cost analysis tools help you understand and optimize spending:

- **Cost Projections**: Estimated future costs based on current usage
- **Cost Saving Opportunities**: Suggestions for optimizing model selection
- **Budget Forecasting**: Predictions for when budgets might be exceeded

## Advanced Features

### Custom Reports

Create tailored reports for specific needs:

1. Click "Custom Report" in the dashboard menu
2. Select metrics, keys, and time period
3. Configure visualization preferences
4. Save for future use or generate one-time report

### Automated Alerts

Configure automated notifications:

1. Navigate to the "Alert Settings" section
2. Define threshold conditions (e.g., "80% of daily budget")
3. Select notification delivery method (email, in-app, webhook)
4. Set frequency and importance level

## Best Practices

- **Regular Monitoring**: Check the dashboard daily for budget and usage trends
- **Budget Planning**: Set appropriate budgets based on historical usage patterns
- **Anomaly Investigation**: Promptly investigate unusual spikes in usage or cost
- **Key Rotation**: Create new keys periodically for better security
- **Granular Keys**: Use multiple keys for different purposes to track usage more precisely

## Troubleshooting

- **Missing Data**: If recent requests aren't appearing, wait a few minutes for processing
- **Visualization Issues**: Try refreshing the browser or clearing cache
- **Export Problems**: Ensure you're not attempting to export too large a data set
- **Performance Concerns**: Filter to a shorter time period for faster dashboard loading
