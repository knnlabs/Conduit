# Architecture Overview

## System Components

ConduitLLM is built as a modular .NET solution consisting of several interconnected components:

```
ConduitLLM
├── ConduitLLM.Configuration  # Core configuration system
├── ConduitLLM.Core           # Core functionality and interfaces
├── ConduitLLM.Examples       # Example implementations
├── ConduitLLM.Http           # HTTP client implementations
├── ConduitLLM.Providers      # LLM provider integrations
├── ConduitLLM.Tests          # Test projects
└── ConduitLLM.WebUI          # Blazor-based web interface
```

## Component Responsibilities

### ConduitLLM.Configuration

Handles all configuration aspects of the system:
- Provider credentials storage
- Model-to-provider mappings 
- Global application settings
- Database context configuration

### ConduitLLM.Core

Contains the central business logic and interfaces:
- LLM request/response models
- Virtual key management
- Router implementation
- Service interfaces

### ConduitLLM.Http

Implements HTTP-specific functionality:
- API controllers
- Request/response middleware
- Authentication handlers
- Request tracking

### ConduitLLM.Providers

Implements integrations with various LLM providers:
- OpenAI, Anthropic, Cohere, Gemini
- Fireworks, OpenRouter
- Provider-specific request formatting
- Response parsing

### ConduitLLM.WebUI

.NET Blazor application that provides a management interface:
- Provider configuration
- Model mapping
- Virtual key management
- Usage monitoring and visualization

## Key Subsystems

### Routing System

The router enables intelligent distribution of requests across different model deployments:

1. **DefaultLLMRouter**: Implements routing strategies and fallback logic
   - Simple, random, and round-robin distribution
   - Health monitoring and fallback capabilities
   - Retry logic with exponential backoff
   - Streaming support

2. **RouterConfig**: Configuration model for the router
   - Strategy selection
   - Model deployment specifications
   - Fallback configuration

3. **RouterService**: Manages router configuration
   - CRUD operations for model deployments
   - Fallback configuration

### Virtual Key Management

Manages API access, budgets, and usage tracking:

1. **Virtual Key Entity**: Core model for API access
   - Budget constraints
   - Usage tracking
   - Expiration management

2. **Middleware**: Handles request authentication and tracking
   - Key validation
   - Usage accounting
   - Budget enforcement

3. **Notification System**: Alerts users about key status
   - Budget limits
   - Expiration warnings
   - Usage statistics

### WebUI System

A .NET Blazor web application for system configuration:

1. **Database Context**: Stores configuration
   - DbProviderCredentials 
   - DbModelProviderMapping
   - GlobalSettings

2. **Pages**: User interface components
   - Home/Dashboard
   - Configuration
   - Chat testing interface
   - Virtual Keys management

## Data Flow

1. **Request Flow**:
   - Client sends request with virtual key
   - System validates the key
   - Router selects appropriate model
   - Request is sent to provider
   - Response is returned to client
   - Usage is tracked

2. **Configuration Flow**:
   - Admin configures providers via WebUI
   - System stores configuration in database
   - Router is initialized with configuration
   - Virtual keys are created and managed

## Security Architecture

- Master key authentication for sensitive operations
- Virtual key budget enforcement
- API key secure storage
- Request logging and monitoring

## Integration Points

- Provider APIs via HTTP/HTTPS
- Database for configuration storage
- Web interface for management
- Notification system for alerts
