---
sidebar_position: 3
title: Model Routing
description: Learn how Conduit routes requests to different LLM providers
---

# Model Routing

Conduit's model routing system allows you to define how requests are directed to different LLM providers based on a variety of criteria and strategies.

## Model Mappings

The core of Conduit's routing system is the model mapping table. Each mapping consists of:

- **Virtual Model Name** - The name clients use in their requests
- **Provider Model** - The actual provider-specific model name
- **Provider** - Which LLM provider to use
- **Priority** - A numerical value used for routing decisions
- **Weight** - Optional value for weighted routing strategies

For example, you might map the virtual model name `my-gpt4` to OpenAI's `gpt-4` or Anthropic's `claude-3-opus-20240229`.

## Routing Strategies

Conduit offers several strategies for routing requests:

| Strategy | Description |
|----------|-------------|
| Simple | Uses the first available mapping for a requested model |
| Priority | Uses the mapping with the highest priority |
| Least Cost | Routes to the provider with the lowest cost |
| Round Robin | Distributes requests evenly across providers |
| Random | Randomly selects among available providers |
| Least Used | Favors providers with fewer recent requests |
| Least Latency | Routes to the provider with the lowest recent latency |

## Configuring Routing

Routing can be configured through the Web UI:

1. Navigate to **Configuration > Model Mappings** to define mappings
2. Go to **Configuration > Routing** to set the routing strategy
3. Configure additional parameters like fallbacks and health checks

## Fallback Configuration

Fallbacks allow you to automatically redirect requests when a provider is unavailable:

1. Navigate to **Configuration > Routing > Fallbacks**
2. Define fallback rules with primary and backup providers
3. Set conditions like timeout thresholds or error codes

Example fallback rule:
- Primary: `gpt-4` on OpenAI
- Fallback: `claude-3-opus` on Anthropic
- Condition: If OpenAI returns a 429 (rate limit) error

## Advanced Routing Features

### Health Checks

Conduit can perform health checks to detect provider issues:

1. Navigate to **Configuration > Provider Health**
2. Configure how often to check provider availability
3. Set automatic fallback behavior

### Context-Aware Routing

You can implement custom routing logic using the API:

```json
{
  "model": "my-gpt4",
  "routing": {
    "strategy": "least_cost",
    "fallback_enabled": true
  },
  "messages": [{"role": "user", "content": "Hello!"}]
}
```

## Best Practices

- Define clear priorities for different providers
- Set up fallbacks for critical models
- Monitor provider usage and adjust routing as needed
- Use cost-based routing for budget optimization
- Implement health checks for improved reliability

## Next Steps

- Learn about [Provider Integration](provider-integration) for adding new LLM services
- Explore [Budget Management](../guides/budget-management) for cost control
- See [Cache Configuration](../guides/cache-configuration) to reduce costs and latency