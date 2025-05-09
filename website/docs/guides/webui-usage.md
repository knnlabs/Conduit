---
sidebar_position: 4
title: Web UI Guide
description: Learn how to use Conduit's Web UI for configuration and management
---

# Web UI Guide

Conduit provides a comprehensive Web UI for configuration, monitoring, and management. This guide covers all the major features and how to use them effectively.

## Accessing the Web UI

By default, the Web UI is available at `http://localhost:5001` (or the port you configured) after starting Conduit.

## Authentication

Access to the Web UI requires authentication with the master key:

1. Navigate to the Web UI URL
2. Enter your master key in the login form
3. Click **Login**

## Dashboard

The dashboard provides an overview of your Conduit instance:

- **Request Metrics**: Total requests, success rate, and throughput
- **Cost Summary**: Current spending and trends
- **Provider Status**: Health status of configured providers
- **Recent Activities**: Latest requests and events

## Navigation

The sidebar provides access to all major sections:

- **Dashboard**: System overview
- **Virtual Keys**: Manage API keys
- **Cost Dashboard**: Analyze spending
- **Request Logs**: View request history
- **Configuration**: System settings
- **Provider Health**: Monitor LLM services
- **Documentation**: Access help resources

## Virtual Keys Management

The Virtual Keys section allows you to create and manage API keys:

### Creating a New Key

1. Navigate to **Virtual Keys**
2. Click **Create New Key**
3. Fill in the form:
   - **Name**: Descriptive name
   - **Description**: Purpose of the key
   - **Permissions**: Access controls
   - **Rate Limits**: Usage restrictions
   - **Budget Controls**: Spending limits
4. Click **Create**
5. Copy the generated key (it will only be shown once)

### Managing Existing Keys

- **View Details**: Click on a key to see its configuration
- **Edit**: Update settings for an existing key
- **Regenerate**: Create a new key value while keeping settings
- **Disable/Enable**: Temporarily suspend access
- **Delete**: Permanently remove the key

## Configuration

The Configuration section includes several sub-sections:

### Provider Credentials

1. Navigate to **Configuration > Provider Credentials**
2. Add or manage API keys for different LLM providers
3. Test connections to ensure credentials are valid

### Model Mappings

1. Navigate to **Configuration > Model Mappings**
2. Define how virtual model names map to actual provider models
3. Set priorities and weights for routing

### Routing

1. Navigate to **Configuration > Routing**
2. Select the default routing strategy
3. Configure fallback settings and rules

### Caching

1. Navigate to **Configuration > Caching**
2. Enable or disable the cache
3. Configure the cache provider and settings

### System Settings

1. Navigate to **Configuration > System**
2. Adjust global settings like logging level
3. Configure security options

## Monitoring & Analytics

### Request Logs

1. Navigate to **Request Logs**
2. View detailed logs of all API requests
3. Filter by date, model, provider, or status
4. Inspect request and response details

### Cost Dashboard

1. Navigate to **Cost Dashboard**
2. View spending breakdowns by various dimensions
3. Analyze trends and patterns
4. Export data for reporting

### Provider Health

1. Navigate to **Provider Health**
2. Monitor the status of all configured providers
3. View historical uptime and performance
4. Configure health check settings

## Chat Interface

Conduit includes a built-in chat interface for testing:

1. Navigate to **Chat**
2. Select a model from the dropdown
3. Type a message and send it
4. View the response and detailed request information

## Customization

### Theme Settings

1. Click your username in the top-right corner
2. Select **Preferences**
3. Choose between light and dark themes
4. Adjust other display options

### Notifications

1. Navigate to **Configuration > Notifications**
2. Configure email or webhook notifications
3. Set up alerts for important events

## Keyboard Shortcuts

The Web UI supports several keyboard shortcuts:

- **Ctrl+K** or **/** - Open search
- **Ctrl+,** - Open settings
- **Ctrl+L** - Clear chat (in Chat interface)
- **Ctrl+S** - Save changes (when editing)

## Next Steps

- Learn about [Virtual Keys](../features/virtual-keys) for API authentication
- Explore [Budget Management](budget-management) for cost control
- See the [Cache Configuration](cache-configuration) guide for performance optimization