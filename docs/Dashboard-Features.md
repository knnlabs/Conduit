# Dashboard Features

## Overview

The ConduitLLM Dashboard serves as the central hub for monitoring system status, tracking usage, and accessing key functionality. This document provides a comprehensive guide to all dashboard components and visualizations.

## Dashboard Layout

The dashboard is organized into several key sections:

### Header Section

Located at the top of the dashboard:

- **System Status Indicator**: Shows overall health of the system
- **Quick Stats**: Provider count, model mapping count, active virtual keys
- **Time Period Selector**: Filter dashboard data by time period
- **Refresh Button**: Manually refresh dashboard data
- **Settings Menu**: Configure dashboard appearance and content

### Main Content Sections

The dashboard is divided into functional areas:

1. **System Overview**
2. **Provider Status**
3. **Request Analytics**
4. **Budget Utilization**
5. **Recent Activity**

## System Overview

### Welcome Card

When first setting up ConduitLLM, the dashboard displays a welcome card:

- **Setup Guidance**: Step-by-step setup instructions
- **Configuration Status**: Shows completion of setup steps
- **Quick Actions**: Buttons to complete remaining setup tasks

The welcome card is replaced by usage statistics once the system is configured.

### System Health

Visual indicators of overall system health:

- **API Availability**: Status of the API endpoints
- **Database Connection**: Database connectivity status
- **Provider Connectivity**: Aggregated provider status
- **Critical Alerts**: Count of unresolved critical issues

### Key Metrics

Summary of important system metrics:

- **Total Requests**: Count of API requests in the selected period
- **Success Rate**: Percentage of successful requests
- **Average Response Time**: Mean time to fulfill requests
- **Active Models**: Count of actively used models

## Provider Status

### Provider Cards

Individual cards for each configured provider:

- **Status Indicator**: Shows if the provider is active/inactive
- **Connection Status**: Displays current connectivity
- **Request Count**: Number of requests sent to this provider
- **Error Rate**: Percentage of failed requests
- **Quick Actions**: Test connection, edit configuration

### Provider Usage Chart

Visualizes usage distribution across providers:

- **Pie Chart**: Shows proportion of requests by provider
- **Bar Chart**: Compares request counts across providers
- **Line Chart**: Shows usage trends over time
- **Filters**: Filter by time period, model type

## Request Analytics

### Request Volume

Charts showing API request patterns:

- **Requests Over Time**: Line chart of requests per hour/day
- **Peak Usage**: Highlights periods of highest activity
- **Request Types**: Breakdown of completion vs. chat requests
- **User Agent Analysis**: Requests by client application

### Token Usage

Analysis of token consumption:

- **Total Tokens**: Aggregate token usage
- **Prompt vs. Completion**: Comparison of input/output tokens
- **Token Efficiency**: Ratio of output to input tokens
- **Cost Implications**: Estimated costs based on token usage

### Performance Metrics

Visualizations of system performance:

- **Response Times**: Distribution of request latencies
- **Queue Times**: Time spent waiting for processing
- **Provider Latency**: Comparison of provider response times
- **Error Distribution**: Types and frequencies of errors

## Budget Utilization

### Budget Overview

Summary of spending across virtual keys:

- **Total Spend**: Aggregate spending for all keys
- **Budget Utilization**: Percentage of total budget consumed
- **Projected Spend**: Estimated future spending based on trends
- **Cost Breakdown**: Spending by model and request type

### Virtual Key Budget Status

Visual representation of key budgets:

- **Budget Gauge**: Shows consumed vs. remaining budget
- **Spend Rate**: Daily/hourly spending rate
- **Time to Depletion**: Estimated time until budget exhaustion
- **Budget History**: Trend of spending over time

### Cost Optimization

Insights for reducing costs:

- **Expensive Requests**: Identification of high-cost operations
- **Model Recommendations**: Suggestions for cost-effective models
- **Caching Opportunities**: Potential savings from response caching
- **Usage Anomalies**: Unusual spending patterns

## Recent Activity

### Activity Feed

Chronological list of system events:

- **API Requests**: Recent API calls with status
- **Configuration Changes**: Updates to system configuration
- **Error Events**: Recent system or API errors
- **Maintenance Events**: System maintenance activities

### Key Events Timeline

Visual timeline of significant events:

- **Provider Additions**: When new providers were added
- **Model Deployments**: New model mapping activations
- **Virtual Key Creation**: New virtual key issuance
- **System Updates**: Software updates and changes

## Interactive Features

The dashboard includes several interactive elements:

### Filtering and Search

- **Time Period Selection**: Filter data by custom time ranges
- **Provider Filtering**: Focus on specific providers
- **Model Filtering**: View data for specific models
- **Status Filtering**: Filter by success/error status

### Drill-Down Capabilities

- **Click-Through**: Access detailed views from summary metrics
- **Expandable Panels**: Reveal additional information
- **Detailed Reports**: Generate comprehensive reports
- **Export Options**: Download data in CSV/JSON formats

### Alerts Configuration

- **Threshold Setting**: Configure alert thresholds
- **Notification Preferences**: Set notification channels
- **Alert Rules**: Create custom alert conditions
- **Alert History**: View past alerts and resolutions

## VirtualKeys Dashboard

### Key Listing

Comprehensive view of all virtual keys:

- **Status Indicators**: Active/inactive status
- **Utilization Bars**: Visual representation of budget usage
- **Expiration Countdown**: Time remaining until expiration
- **Last Used**: Most recent usage timestamp

### Key Metrics

Detailed analytics for each key:

- **Request Volume**: Number of requests made with the key
- **Spending Rate**: Average daily/monthly spend
- **Token Usage**: Total tokens consumed
- **Model Distribution**: Usage across different models

### Usage Patterns

Visualization of key usage patterns:

- **Usage Calendar**: Heatmap of activity by day/hour
- **Usage Consistency**: Variability in usage over time
- **Burst Analysis**: Identification of usage spikes
- **Idle Periods**: Detection of unused time periods

## Dashboard Customization

### Layout Options

The dashboard layout can be customized:

- **Widget Arrangement**: Drag and drop to reorganize
- **Section Visibility**: Show/hide specific sections
- **Chart Types**: Select preferred visualization types
- **Data Density**: Adjust information density

### Display Preferences

Personalization options:

- **Time Zone**: Set preferred time zone for timestamps
- **Date Format**: Configure date display format
- **Currency**: Select currency for cost displays
- **Color Schemes**: Choose dashboard theme colors

### Saved Views

Create personalized dashboard configurations:

- **Custom Layouts**: Save specific dashboard arrangements
- **Focused Views**: Create topic-specific dashboards
- **Scheduled Reports**: Configure automatic report generation
- **Shared Views**: Share dashboard configurations with team members

## Mobile Responsiveness

The dashboard is fully responsive for mobile devices:

- **Adaptive Layout**: Reorganizes for smaller screens
- **Touch Optimization**: Larger touch targets for mobile
- **Simplified Views**: Streamlined information for mobile
- **Performance Optimization**: Faster loading on mobile networks

## Technical Implementation

### Data Refresh

The dashboard data refreshes through multiple mechanisms:

- **Automatic Refresh**: Data updates every 5 minutes
- **Manual Refresh**: User can force immediate refresh
- **Real-Time Updates**: Critical metrics update in real-time
- **Cached Data**: Historical data is cached for performance

### Data Visualization

Built with modern visualization libraries:

- **Interactive Charts**: Hover/click for detailed information
- **Responsive Design**: Charts adapt to container size
- **Accessibility Features**: Screen reader support, keyboard navigation
- **Export Capabilities**: Download charts as images or data

## Best Practices

### Dashboard Monitoring

Effective use of the dashboard for monitoring:

- **Regular Reviews**: Check dashboard daily for system health
- **Alert Configuration**: Set up alerts for critical thresholds
- **Trend Analysis**: Look for patterns and anomalies
- **Detail Investigation**: Drill down into unusual metrics

### Performance Optimization

Keep the dashboard performing well:

- **Time Range Limitations**: Avoid excessively long time ranges
- **Filter Usage**: Apply filters to focus on relevant data
- **Browser Cache**: Clear browser cache periodically
- **Resource Management**: Close unused dashboard tabs
