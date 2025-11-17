# Conduit Routing System Documentation

## Overview

The Conduit routing system provides intelligent request routing capabilities that allow administrators to configure sophisticated routing rules, manage provider priorities, and implement advanced routing strategies to optimize cost, performance, and reliability.

## Quick Start

1. **Navigate to Routing Settings**: Access the routing configuration interface from the admin dashboard
2. **Configure Provider Priorities**: Set up your provider priority order in the Provider Priority tab
3. **Create Routing Rules**: Define custom routing rules in the Routing Rules tab
4. **Test Your Configuration**: Use the Testing & Validation tab to verify your routing logic

## Key Features

### Intelligent Request Routing
- **Rule-based routing**: Create custom rules based on model, region, cost, and other parameters
- **Provider priorities**: Define fallback chains and load balancing strategies
- **Real-time evaluation**: Test routing decisions with sample requests

### Advanced Configuration
- **Condition-based rules**: Support for multiple condition types (equals, contains, greater than, etc.)
- **Action-based routing**: Route to specific providers, apply cost thresholds, or use fallback chains
- **Priority management**: Fine-grained control over rule evaluation order

### Testing and Debugging
- **Rule testing interface**: Test routing rules with custom request parameters
- **Evaluation timeline**: Step-by-step breakdown of routing decisions
- **Provider selection analysis**: Understand why specific providers were chosen

## Documentation Structure

- **[Concepts](./concepts.md)** - Core routing concepts and terminology
- **[Rules Guide](./rules-guide.md)** - Creating and managing routing rules
- **[Providers Guide](./providers-guide.md)** - Managing provider priorities and configurations
- **[Testing Guide](./testing-guide.md)** - Testing and validating routing rules
- **[Examples](./examples/)** - Common routing patterns and use cases
- **[Troubleshooting](./troubleshooting.md)** - Common issues and solutions

## Getting Started

### Understanding Routing in Conduit

Conduit's routing system works by evaluating incoming requests against a set of configurable rules. Each rule contains:

1. **Conditions**: Criteria that must be met for the rule to match
2. **Actions**: What to do when the rule matches (route to provider, apply settings, etc.)
3. **Priority**: The order in which rules are evaluated

### When to Use Routing Rules

Routing rules are beneficial when you need to:

- **Optimize costs** by routing expensive models to specific providers
- **Implement geographic routing** for compliance or performance reasons
- **Set up intelligent fallbacks** for high availability
- **Apply different configurations** based on request characteristics
- **Balance load** across multiple providers

### Basic vs Advanced Routing Strategies

**Basic Routing** (Provider Priority Only):
- Simple priority-based provider selection
- Automatic fallback to next available provider
- Good for simple setups with consistent routing needs

**Advanced Routing** (Custom Rules + Provider Priority):
- Complex condition-based routing decisions
- Multiple routing strategies per request type
- Fine-grained control over routing behavior
- Ideal for complex environments with diverse requirements

### Performance Considerations

- **Rule Evaluation**: Rules are evaluated in priority order (lower numbers first)
- **Caching**: Routing decisions can be cached to improve performance
- **Condition Optimization**: Simpler conditions evaluate faster
- **Rule Count**: Keep the number of rules reasonable for optimal performance

## Next Steps

1. Read the [Concepts Guide](./concepts.md) to understand routing terminology
2. Follow the [Rules Guide](./rules-guide.md) to create your first routing rule
3. Check out [Common Examples](./examples/) for typical routing patterns
4. Use the [Testing Guide](./testing-guide.md) to validate your configuration

## Support

For additional help:
- Check the [Troubleshooting Guide](./troubleshooting.md)
- Review the [Examples](./examples/) for similar use cases
- Contact your system administrator for environment-specific questions