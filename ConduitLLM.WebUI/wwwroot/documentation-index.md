# ConduitLLM Documentation

## Core Documentation

- [API Documentation](api-documentation.md) - Comprehensive documentation of ConduitLLM's REST API endpoints, including authentication, virtual key management, request tracking, and notification systems.

## Feature-Specific Guides

- [Virtual Keys Dashboard Guide](virtual-keys-dashboard-guide.md) - Detailed instructions for using the VirtualKeysDashboard to monitor and manage virtual keys, analyze usage patterns, and track budgets.
- [Notification System Guide](notification-system-guide.md) - Configuration guide for setting up and managing the notification system, including delivery methods, alert thresholds, and best practices.

## System Overview

ConduitLLM provides a robust infrastructure for managing access to various LLM providers:

- **Virtual Key Management** - Create, update, and monitor API keys with fine-grained control over permissions, budgets, and model access.
- **Request Tracking** - Detailed logging and analysis of all API requests, including token usage, costs, and performance metrics.
- **Budget Enforcement** - Automated budget management with daily, monthly, and total spending limits.
- **Notification System** - Real-time alerts for budget limits, key expirations, and system events.
- **Administrator Tools** - Comprehensive dashboard and management interfaces.

## Getting Started

1. Configure your LLM providers in the WebUI
2. Set up your master key for administrative access
3. Create virtual keys for your applications and users
4. Monitor usage through the VirtualKeysDashboard
5. Configure notifications for important events

## Additional Resources

- For code examples and SDK documentation, refer to the `/Examples` directory in the codebase
- For integration guides with specific LLM providers, see the provider-specific documentation
