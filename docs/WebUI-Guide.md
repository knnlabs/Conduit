# ConduitLLM WebUI Guide

## Overview

ConduitLLM WebUI is a .NET Blazor application that provides a comprehensive web interface for configuring and managing all aspects of the ConduitLLM system. This interface allows administrators to configure providers, manage model mappings, monitor usage, and test LLM interactions—all without writing any code.

The WebUI communicates with the ConduitLLM Admin API service to perform administrative operations such as managing configurations, virtual keys, and monitoring usage. This architecture provides a clean separation of concerns, improved security, and better scalability.

## Architecture

The WebUI follows a client-server architecture where:

1. **Client**: The Blazor WebUI that runs in the browser
2. **Server**: The WebUI server that hosts the Blazor application
3. **Admin API**: A separate service that provides administrative endpoints

### Communication Flow

The WebUI communicates with the Admin API using an `AdminApiClient` service which implements the following pattern:

1. WebUI components call service interfaces (e.g., `IVirtualKeyService`)
2. Service adapters implement these interfaces and handle:
   - Converting between DTOs
   - Making HTTP requests to the Admin API
   - Handling errors and translating responses
   - Providing backward compatibility

This architecture allows the WebUI to operate without direct database access, improving security and enabling distributed deployments.

## Accessing the WebUI

### Default Configuration

- **WebUI URL**: `http://localhost:5000` (default)
- **Admin API URL**: `http://localhost:5001` (default)
- **LLM API URL**: `http://localhost:5002` (default)
- **Authentication**: Based on configured security options

### First-Time Setup

When accessing the WebUI for the first time:

1. You'll be prompted to configure a master key if none exists
2. You'll see a guided setup experience to configure your first provider
3. The dashboard will display a clear call to action if no providers are configured

## Navigation

The WebUI features a consistent navigation layout:

### Main Navigation

- **Dashboard**: Home page with system overview and statistics
- **Configuration**: Manage providers, models, and router settings
- **Virtual Keys**: Create and manage API access keys
- **Provider Health**: Monitor the status and health of LLM providers
- **Chat**: Test LLM interactions directly
- **About**: Information about the ConduitLLM project

### Secondary Navigation

- **User Profile**: Access user-specific settings (if authentication is enabled)
- **Notifications**: View system notifications and alerts
- **Settings**: Access global application settings

## Key Pages

### Dashboard

The home page provides an overview of your ConduitLLM system:

- **Status Summary**: Quick view of system status
- **Provider Statistics**: Number of configured providers and their status
- **Model Mapping Count**: Total number of configured model mappings
- **Recent Activity**: Latest system events and API calls
- **Usage Metrics**: Charts showing recent usage patterns

[Learn more about dashboard features](Dashboard-Features.md)

### Configuration Page

The central hub for system configuration with multiple tabs:

#### Providers Tab

Manage LLM provider connections:

- List all configured providers
- Add new providers with API keys
- Edit existing provider settings
- Toggle provider active status
- Access provider documentation and API key links

#### Model Mappings Tab

Configure generic-to-specific model mappings:

- Create abstract model names (e.g., "chat-large")
- Map these to provider-specific models
- Configure which providers handle which models
- Manage model aliases and categories

#### Router Tab

Configure the LLM router:

- Select routing strategy (simple, random, round-robin)
- Configure model deployments
- Set up fallback paths between models
- Adjust routing weights
- View router health status

#### Global Settings Tab

Configure system-wide settings:

- Master key management
- Default timeout values
- Logging configuration
- Cache settings
- Security options

### Virtual Keys Page

Manage API access through virtual keys:

#### Keys List

- View all virtual keys with filtering and sorting
- Check key status (active/inactive)
- Monitor budget usage
- See expiration status

#### Key Management

- Create new virtual keys with custom settings
- Edit existing keys (name, budget, expiration)
- Reset key spending
- Revoke/delete keys

#### Usage Dashboard

- Visualize key usage over time
- Track budget consumption
- Identify usage patterns
- Export usage data

### Provider Health Page

Monitor the status and health of LLM providers:

- View a comprehensive list of all configured providers with their current status
- See detailed information including:
  - Status type (Online/Offline/Unknown)
  - Last check timestamp
  - Response time
  - Error details (if any)
  - Monitoring configuration
- Status types explained:
  - **Online** (green): Provider is responding correctly and API keys are validated
  - **Offline** (red): Provider is not responding or returning errors
  - **Unknown** (gray): Status cannot be determined with certainty (e.g., OpenRouter)
- Filter providers by status type
- Manually trigger health checks for specific providers
- View historical health data and response time trends

### Chat Page

Test LLM interactions directly:

- Select models to interact with
- Configure completion parameters (temperature, tokens)
- Send messages and view responses
- Compare outputs between models
- Save and load conversations

## Common Tasks

### Adding a New Provider

1. Navigate to **Configuration > Providers**
2. Click **Add Provider**
3. Select the provider type (e.g., OpenAI, Anthropic)
4. Enter provider name (for your reference)
5. Enter API key (links are provided to get API keys)
6. Optionally configure custom endpoint URL
7. Click **Save**

### Creating a Model Mapping

1. Navigate to **Configuration > Model Mappings**
2. Click **Add Mapping**
3. Enter a generic model name (e.g., "chat-large")
4. Select the provider from the dropdown
5. Select the provider-specific model (e.g., "gpt-4" for OpenAI)
6. Set the mapping as active
7. Click **Save**

### Creating a Virtual Key

1. Navigate to **Virtual Keys**
2. Click **Create Key**
3. Enter a name for the key
4. Optionally set a budget limit
5. Choose a budget reset period (daily/monthly)
6. Set an expiration date if desired
7. Click **Create**
8. Copy the generated key for API use

### Testing a Conversation

1. Navigate to **Chat**
2. Select the model to test
3. Configure parameters (temperature, max tokens)
4. Enter a system message (optional)
5. Type your message in the chat input
6. Click **Send** or press Enter
7. View the model's response
8. Continue the conversation as needed

## UI Components

### Provider Edit Form

![Provider Health Monitoring](../assets/provider-health.png)

The Provider Health Monitoring page shows the status of all configured providers.

This form includes:
- Provider type selection
- Name field
- API key field (with copy/paste support)
- Endpoint URL field
- Helpful links for API documentation and key generation
- Test connection button

### Model Mapping Manager

![Model Mapping Manager](../assets/config-model-mapping.png)

The Model Mapping interface allows you to create and manage model aliases that map to provider-specific models.

Features:
- Generic model name field
- Provider selection
- Provider-specific model selection
- Active status toggle
- Bulk operations for multiple mappings

### Virtual Key Creator

![Dashboard Home](../assets/dashboard-home.png)

The Dashboard provides an overview of system status including provider health, routing configuration, and caching system status.

Includes:
- Name field
- Budget amount input
- Budget period selection
- Expiration date picker
- Active status toggle
- Generated key display (after creation)

### Chat Interface

![Chat Interface](../assets/chat-interface.png)

The Chat interface allows you to test model responses directly from the WebUI with configurable parameters.

Provides:
- Model selection dropdown
- Parameter configuration panel
- System message input
- Conversation history display
- Message input
- Response streaming (when supported)

## Notifications

The WebUI includes a notification system that alerts you to important events:

### Types of Notifications

- **System Events**: Server starts, updates, configuration changes
- **Error Alerts**: API failures, configuration issues
- **Budget Alerts**: Virtual keys approaching or exceeding budgets
- **Expiration Notices**: Virtual keys nearing expiration
- **Usage Milestones**: Significant usage thresholds

### Notification Display

Notifications appear in:
- The notification panel (accessed via the bell icon)
- Toast messages for critical alerts
- Dashboard summaries
- Email (if configured)

## Customization

The WebUI supports several customization options:

### Theme Settings

- Light and dark mode toggle
- Color scheme customization
- Layout density options

### Display Preferences

- Table view customization
- Chart type selection
- Dashboard widget arrangement
- Metric display options

### User Preferences

- Default models for chat
- Parameter presets
- Notification preferences

## Security Considerations

### Authentication

The WebUI can be configured with different authentication methods:

- Local authentication *(coming soon – [see issue #17](https://github.com/knnlabs/Conduit/issues/17))*
- OAuth/OpenID Connect *(coming soon – [see issue #17](https://github.com/knnlabs/Conduit/issues/17))*
- LDAP/Active Directory *(coming soon – [see issue #17](https://github.com/knnlabs/Conduit/issues/17))*
- Single Sign-On integration *(coming soon – [see issue #17](https://github.com/knnlabs/Conduit/issues/17))*
- **Current:** Master key authentication for admin access

> Only master key authentication is currently available. Additional authentication methods are planned and tracked in [issue #17](https://github.com/knnlabs/Conduit/issues/17).

## Deployment Options

ConduitLLM supports multiple deployment configurations thanks to its microservices architecture:

### Single-Host Deployment

All services run on a single machine:
- WebUI service
- Admin API service
- LLM API service
- Database (SQLite or other)

This is the simplest deployment and works well for development or small-scale usage.

### Distributed Deployment

Services can be distributed across multiple machines:
1. **Frontend Tier**: WebUI service
2. **Backend Tier**: Admin API and LLM API services
3. **Data Tier**: Database server

This approach is recommended for production use and provides:
- Better scalability
- Improved security (database not accessible from frontend)
- Higher availability
- Load balancing capability

### Docker Deployment

The recommended approach is using Docker Compose or Kubernetes to orchestrate the services:
- Each service runs in its own container
- Shared database container or external database service
- Redis container for optional caching

See the [Getting Started Guide](Getting-Started.md) for Docker Compose examples.

### Authorization

Role-based access control can restrict access to sensitive operations:

- Admin role: Full access to all features
- Operator role: Can manage but not create providers and keys
- User role: Can use chat and view own usage
- Read-only role: Can view but not modify configuration

### Sensitive Data

- API keys are masked in the UI (displayed as ******)
- Caching is disabled for pages with sensitive information
- Master key changes require confirmation

## Troubleshooting

### Common UI Issues

- **Page Doesn't Load**: Check server status and browser console
- **Changes Not Saved**: Verify form validation and server connectivity
- **Provider Test Fails**: Check API key and endpoint URL
- **Chat Not Responding**: Verify provider configuration and connectivity

### Browser Compatibility

The WebUI works best with modern browsers:
- Chrome/Edge (recommended)
- Firefox
- Safari

### Error Logging

UI errors are logged to:
- Browser console (client-side issues)
- Server logs (API and backend issues)
- Network tab (API request problems)

## Best Practices

### WebUI Security

- Access WebUI only on secure networks
- Change the master key periodically
- Use strong passwords for authentication
- Log out when not actively using the interface

### Performance Optimization

- Clear browser cache periodically
- Don't leave the WebUI open for extended periods
- Limit the number of records displayed in tables
- Use filtering to narrow down large data sets

### Configuration Management

- Make configuration changes systematically
- Test changes in the Chat interface
- Document custom configurations
- Regularly review provider settings
