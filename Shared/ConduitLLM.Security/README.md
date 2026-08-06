# ConduitLLM.Security

This project contains shared security services, interfaces, and middleware used by both the Gateway API (ConduitLLM.Gateway) and Admin API (ConduitLLM.Admin).

## Overview

The ConduitLLM.Security project provides:
- Security event monitoring and logging
- Threat detection and analysis
- Security metrics collection
- Shared authentication handlers
- Security middleware components
- Rate limiting and IP filtering logic

## Project Structure

```
ConduitLLM.Security/
├── Services/           # Security service implementations
├── Interfaces/         # Security service interfaces
├── Models/            # Internal security models
├── Middleware/        # Security middleware components
└── Extensions/        # Extension methods for security configuration
```

## Key Components

### Services
- `SecurityServiceBase` - Failed-auth tracking, IP bans and rate-limit state shared by
  the Gateway and Admin security services
- `FixedWindowCounter` - Counter backing the fixed-window rate limiters

### Interfaces
- `ISecurityService` - Request authorization contract

> Security telemetry is exported as Prometheus metrics and structured logs; dashboards
> and alerting live in the Grafana stack rather than in this project.

### Middleware
- Shared security middleware for authentication and authorization
- Rate limiting middleware
- IP filtering middleware

## Usage

Both Gateway API and Admin API reference this project to access shared security functionality. The services are registered via dependency injection and can be used throughout the application.

## Dependencies

- ConduitLLM.Core - Core interfaces and models
- ConduitLLM.Configuration - Security DTOs and configuration