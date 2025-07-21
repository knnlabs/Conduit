# LLM Routing

## Overview

ConduitLLM's router provides intelligent distribution of requests across different LLM providers and models. This functionality enables failover capabilities, load balancing, and cost optimization by directing requests to the most appropriate model deployment based on availability, cost, and performance.

> **Note:** The router supports both text and multimodal (vision) models. Routing, fallback, and cost optimization features apply to all supported model types.

> **Supported Providers:** ConduitLLM supports routing across providers including OpenAI, Anthropic, Cohere, Gemini, Fireworks, and OpenRouter.

## Router Components

The routing system consists of three main components:

### 1. DefaultLLMRouter

The `DefaultLLMRouter` is the core implementation that handles the actual routing logic:

- **Multiple Routing Strategies**: Supports different ways to distribute requests:
  - **Simple**: Uses the first available deployment for the requested model
  - **Random**: Randomly selects from available deployments
  - **Round-Robin**: Cycles through available deployments sequentially

- **Health Tracking**: Monitors the health status of model deployments:
  - Tracks successful and failed requests
  - Calculates success rates and error frequencies
  - Automatically marks unreliable deployments as unhealthy

- **Fallback Capabilities**: When a model fails, the router can:
  - Try alternative deployments of the same model
  - Fall back to alternative models based on configuration
  - Return appropriate errors when no options remain

- **Retry Logic**: Implements sophisticated retry handling:
  - Exponential backoff for temporary failures
  - Configurable maximum retry attempts
  - Differentiation between retryable and non-retryable errors

- **Streaming Support**: Properly manages streaming completions:
  - Handles async iterators for streaming responses
  - Maintains consistent streaming behavior across providers
  - Properly propagates streaming errors

### 2. RouterConfig

The `RouterConfig` provides the configuration model for the router:

- **Strategy Selection**: String-based selection of routing strategies
- **Model Deployment Specifications**: Defines which models are available
- **Fallback Configuration**: Specifies fallback paths between models

Example configuration:

```json
{
  "strategy": "round-robin",
  "deployments": [
    {
      "model": "gpt-4-equivalent",
      "provider": "openai-provider-id",
      "weight": 1.0,
      "isActive": true
    },
    {
      "model": "gpt-4-equivalent",
      "provider": "anthropic-provider-id",
      "weight": 0.5,
      "isActive": true
    }
  ],
  "fallbacks": [
    {
      "primaryModel": "gpt-4-equivalent",
      "fallbackModels": ["gpt-3.5-equivalent", "command-equivalent"]
    }
  ]
}
```

### 3. RouterService

The `RouterService` manages the router configuration in the database:

- **Configuration Management**: CRUD operations for router settings
- **Model Deployment Management**: Add, update, and remove model deployments
- **Fallback Configuration**: Define fallback paths between models
- **Router Initialization**: Create and configure router instances

## Routing Strategies

### Simple Strategy

The simplest routing approach that uses the first available deployment for a requested model:

- **Advantages**: Predictable behavior, minimal overhead
- **Disadvantages**: No load balancing, single point of failure
- **Use Case**: Development environments, simple deployments

### Random Strategy

Randomly selects from available deployments:

- **Advantages**: Basic load balancing, no state to maintain
- **Disadvantages**: Potential for uneven distribution
- **Use Case**: Multiple deployments of similar capability/cost

### Round-Robin Strategy

Cycles through available deployments in sequence:

- **Advantages**: Fair load distribution, predictable pattern
- **Disadvantages**: Requires state maintenance
- **Use Case**: Production environments with multiple similar deployments

## Fallback Mechanism

The fallback system allows the router to try alternative options when a request fails:

### Deployment-Level Fallback

When a specific deployment fails, the router tries another deployment of the same model:

1. Model X on Provider A fails
2. Router tries Model X on Provider B
3. If successful, the request is fulfilled

### Model-Level Fallback

When all deployments of a model fail, the router tries an alternative model:

1. All deployments of Model X fail
2. Router checks fallback configuration
3. Router tries Model Y (fallback model)
4. If successful, the request is fulfilled

### Configuration Example

```json
{
  "fallbacks": [
    {
      "primaryModel": "gpt-4-equivalent",
      "fallbackModels": ["gpt-3.5-equivalent", "claude-equivalent"]
    },
    {
      "primaryModel": "gpt-3.5-equivalent",
      "fallbackModels": ["command-equivalent"]
    }
  ]
}
```

## Health Monitoring

The router tracks the health of model deployments to ensure reliability:

### Health Metrics

- **Success Rate**: Percentage of successful requests
- **Error Frequency**: Rate of errors over time
- **Response Time**: Average and percentile response times

### Health Status

Based on metrics, deployments are marked as:

- **Healthy**: Available for routing
- **Degraded**: Available but with caution
- **Unhealthy**: Temporarily excluded from routing

### Recovery

Unhealthy deployments are periodically tested to check if they've recovered:

- **Circuit Breaker Pattern**: Allows occasional test requests
- **Automatic Recovery**: Restores deployment to rotation when healthy
- **Manual Override**: Admin can force deployment status

## Implementation Details

### Request Flow

1. Client sends request for a generic model
2. Router selects a deployment based on strategy
3. Router sends request to the selected provider
4. If successful, response is returned to client
5. If failed, router tries fallback options
6. If all options fail, error is returned to client

### Error Handling

The router handles various error types:

- **Transient Errors**: Automatically retried with backoff
- **Provider Errors**: Trigger fallback to alternative providers
- **Model Errors**: Trigger fallback to alternative models
- **Catastrophic Errors**: Returned to client with helpful context

### Performance Considerations

- **Caching**: Deployment health status is cached
- **Concurrency**: Router handles concurrent requests safely
- **Overhead**: Minimal latency added by routing logic

## Configuration Examples

### Basic Setup

```json
{
  "strategy": "simple",
  "deployments": [
    {
      "model": "chat-model",
      "provider": "openai-provider-id",
      "weight": 1.0,
      "isActive": true
    }
  ]
}
```

### Load Balancing Setup

```json
{
  "strategy": "round-robin",
  "deployments": [
    {
      "model": "chat-model",
      "provider": "openai-provider-id",
      "weight": 1.0,
      "isActive": true
    },
    {
      "model": "chat-model",
      "provider": "anthropic-provider-id",
      "weight": 1.0,
      "isActive": true
    },
    {
      "model": "chat-model",
      "provider": "cohere-provider-id",
      "weight": 0.5,
      "isActive": true
    }
  ]
}
```

### Cost Optimization Setup

```json
{
  "strategy": "simple",
  "deployments": [
    {
      "model": "gpt-4-equivalent",
      "provider": "openai-provider-id",
      "weight": 1.0,
      "isActive": true
    }
  ],
  "fallbacks": [
    {
      "primaryModel": "gpt-4-equivalent",
      "fallbackModels": ["gpt-3.5-equivalent"]
    }
  ]
}
```

## API Endpoints

### Get Router Configuration

```
GET /api/router/config
```

### Update Router Strategy

```
PUT /api/router/strategy
```

### Add Model Deployment

```
POST /api/router/deployments
```

### Update Model Deployment

```
PUT /api/router/deployments/{id}
```

### Delete Model Deployment

```
DELETE /api/router/deployments/{id}
```

### Configure Fallbacks

```
POST /api/router/fallbacks
```

See the [API Reference](API-Reference.md) for detailed endpoint documentation.

## WebUI Configuration

The Router can be configured through the WebUI:

1. Navigate to the **Configuration** page
2. Select the **Router** tab
3. Configure the routing strategy
4. Add and manage model deployments
5. Configure fallback paths

## Best Practices

1. **Multiple Providers**: Configure multiple providers for critical models
2. **Fallback Chains**: Create thoughtful fallback paths from expensive to cheaper models
3. **Weights**: Use weights to control traffic distribution based on cost and performance
4. **Health Monitoring**: Regularly review deployment health in the WebUI
5. **Testing**: Test fallback behavior before relying on it in production
6. **Cost Optimization:** The router can optimize for cost by considering model pricing, including vision/multimodal models, when distributing requests and configuring fallbacks.
