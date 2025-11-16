# ConduitLLM.Security

This project contains shared security services, interfaces, and middleware used by both the Core API (ConduitLLM.Http) and Admin API (ConduitLLM.Admin).

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
- `SecurityEventMonitoringService` - Monitors and logs security events
- `ThreatDetectionService` - Analyzes patterns for threat detection
- `SecurityMetricsService` - Collects and aggregates security metrics

### Interfaces
- `ISecurityEventMonitoringService` - Security event monitoring contract
- `IThreatDetectionService` - Threat detection contract
- `ISecurityMetricsService` - Security metrics contract

### Middleware
- Shared security middleware for authentication and authorization
- Rate limiting middleware
- IP filtering middleware

## Usage

Both Core API and Admin API reference this project to access shared security functionality. The services are registered via dependency injection and can be used throughout the application.

## Dependencies

- ConduitLLM.Core - Core interfaces and models
- ConduitLLM.Configuration - Security DTOs and configuration