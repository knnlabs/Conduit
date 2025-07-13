# Cost Optimization Routing Examples

This document provides examples of routing configurations designed to optimize costs while maintaining service quality.

## Overview

Cost optimization routing helps control expenses by:
- Routing expensive models to cost-effective providers
- Applying cost thresholds to prevent overruns
- Using cheaper alternatives during off-peak hours
- Implementing budget controls for high-usage scenarios

## Example 1: Expensive Model Cost Control

### Scenario
Route expensive models (GPT-4, Claude-3-Opus) to specific providers with cost controls.

### Configuration

**Rule: "Expensive Model Cost Control"**
```yaml
name: "Expensive Model Cost Control"
description: "Route expensive models with cost thresholds and fallback to cheaper alternatives"
priority: 10
enabled: true

conditions:
  - type: model
    field: name
    operator: in
    value: ["gpt-4", "claude-3-opus", "gpt-4-turbo"]

actions:
  - type: set_cost_threshold
    value: 0.002  # $0.002 per token max
  - type: route_to_provider
    target: "cost-optimized-premium"
  - type: set_fallback_chain
    targets: ["azure-openai", "anthropic-direct", "local-fallback"]
```

**Expected Behavior:**
- GPT-4 requests limited to $0.002 per token
- Routes to cost-optimized premium provider first
- Falls back to other providers if cost threshold exceeded
- Protects against unexpected cost spikes

### Testing
```yaml
test_config:
  model: "gpt-4"
  cost_threshold: 0.002
  
expected_result:
  rule_applied: "Expensive Model Cost Control"
  provider: "cost-optimized-premium"
  cost_limit: 0.002
```

## Example 2: High-Volume User Budget Control

### Scenario
Apply stricter cost controls for users who have already consumed significant resources this month.

### Configuration

**Rule: "High Volume User Budget Control"**
```yaml
name: "High Volume User Budget Control"
description: "Apply cost limits for users with high monthly usage"
priority: 5
enabled: true

conditions:
  - type: metadata
    field: monthly_usage_usd
    operator: greater_than
    value: 100
  - type: model
    field: name
    operator: in
    value: ["gpt-4", "claude-3-opus"]

actions:
  - type: set_cost_threshold
    value: 0.0015  # Reduced threshold for high users
  - type: route_to_provider
    target: "budget-provider"
  - type: add_metadata
    key: "cost_control_applied"
    value: "high_usage_limit"
```

**Expected Behavior:**
- Users with >$100 monthly usage get reduced cost thresholds
- Routes to budget-conscious provider
- Adds metadata for tracking cost control application

### Testing
```yaml
test_config:
  model: "gpt-4"
  metadata:
    monthly_usage_usd: 150
    user_id: "user123"

expected_result:
  rule_applied: "High Volume User Budget Control"
  provider: "budget-provider"
  cost_limit: 0.0015
  metadata_added:
    cost_control_applied: "high_usage_limit"
```

## Example 3: Off-Hours Cost Optimization

### Scenario
Use cheaper providers during off-peak hours when response time is less critical.

### Configuration

**Rule: "Off-Hours Cost Optimization"**
```yaml
name: "Off-Hours Cost Optimization"
description: "Use cost-effective providers during off-peak hours (10 PM - 6 AM)"
priority: 15
enabled: true

conditions:
  - type: time
    field: hour
    operator: in
    value: [22, 23, 0, 1, 2, 3, 4, 5]  # 10 PM - 6 AM

actions:
  - type: route_to_provider
    target: "cost-effective-provider"
  - type: set_timeout
    value: 120000  # Allow longer timeout for cheaper provider
  - type: add_metadata
    key: "routing_reason"
    value: "off_hours_cost_optimization"
```

**Expected Behavior:**
- Requests during 10 PM - 6 AM use cheaper providers
- Extended timeout accommodates potentially slower response
- Metadata tracks cost optimization routing

### Testing
```yaml
test_config:
  model: "gpt-3.5-turbo"
  time: "2023-12-01T02:00:00Z"  # 2 AM

expected_result:
  rule_applied: "Off-Hours Cost Optimization"
  provider: "cost-effective-provider"
  timeout: 120000
  metadata_added:
    routing_reason: "off_hours_cost_optimization"
```

## Example 4: Tiered Cost Management

### Scenario
Implement different cost thresholds based on user subscription tier.

### Configuration

**Rule: "Free Tier Cost Limits"**
```yaml
name: "Free Tier Cost Limits"
description: "Strict cost limits for free tier users"
priority: 1
enabled: true

conditions:
  - type: metadata
    field: user_tier
    operator: equals
    value: "free"

actions:
  - type: set_cost_threshold
    value: 0.0001  # Very low threshold for free users
  - type: route_to_provider
    target: "free-tier-provider"
  - type: set_timeout
    value: 30000  # Shorter timeout
```

**Rule: "Premium Tier Allowances"**
```yaml
name: "Premium Tier Allowances"
description: "Higher cost limits for premium users"
priority: 2
enabled: true

conditions:
  - type: metadata
    field: user_tier
    operator: equals
    value: "premium"

actions:
  - type: set_cost_threshold
    value: 0.005  # Higher threshold for premium users
  - type: route_to_provider
    target: "premium-provider"
  - type: set_timeout
    value: 90000  # Longer timeout for better service
```

**Expected Behavior:**
- Free users get strict cost limits and basic providers
- Premium users get higher limits and better providers
- Different service levels based on subscription

### Testing
```yaml
# Free tier test
test_config_free:
  model: "gpt-3.5-turbo"
  metadata:
    user_tier: "free"

expected_result_free:
  rule_applied: "Free Tier Cost Limits"
  provider: "free-tier-provider"
  cost_limit: 0.0001

# Premium tier test
test_config_premium:
  model: "gpt-4"
  metadata:
    user_tier: "premium"

expected_result_premium:
  rule_applied: "Premium Tier Allowances"
  provider: "premium-provider"
  cost_limit: 0.005
```

## Example 5: Dynamic Cost Thresholds

### Scenario
Adjust cost thresholds based on current usage patterns and provider costs.

### Configuration

**Rule: "Dynamic Cost Control"**
```yaml
name: "Dynamic Cost Control"
description: "Adjust cost thresholds based on provider pricing and usage"
priority: 20
enabled: true

conditions:
  - type: metadata
    field: enable_dynamic_pricing
    operator: equals
    value: "true"

actions:
  - type: set_cost_threshold
    value: "dynamic"  # Calculated based on current provider costs
  - type: route_to_provider
    target: "best-value-provider"
  - type: add_metadata
    key: "pricing_strategy"
    value: "dynamic"
```

**Expected Behavior:**
- Cost thresholds adjust automatically based on provider pricing
- Routes to the best value provider for current conditions
- Enables dynamic pricing strategies

## Example 6: Model-Specific Cost Optimization

### Scenario
Different cost strategies for different model types and capabilities.

### Configuration

**Rule: "Text Generation Cost Optimization"**
```yaml
name: "Text Generation Cost Optimization"
description: "Optimize costs for text generation models"
priority: 25
enabled: true

conditions:
  - type: model
    field: capability
    operator: equals
    value: "text-generation"
  - type: metadata
    field: length_estimate
    operator: greater_than
    value: 1000  # Long text generation

actions:
  - type: route_to_provider
    target: "bulk-text-provider"
  - type: set_cost_threshold
    value: 0.0008
  - type: enable_caching
    value: 3600  # Cache long responses
```

**Rule: "Code Generation Premium"**
```yaml
name: "Code Generation Premium"
description: "Route code generation to higher quality providers"
priority: 30
enabled: true

conditions:
  - type: model
    field: capability
    operator: equals
    value: "code-generation"

actions:
  - type: route_to_provider
    target: "code-specialist-provider"
  - type: set_cost_threshold
    value: 0.003  # Higher threshold for code quality
  - type: set_timeout
    value: 60000
```

## Provider Priority Configuration

### Cost-Optimized Provider Setup

```yaml
providers:
  cost_effective_provider:
    priority: 1
    type: primary
    cost_per_token: 0.0005
    capabilities: ["text-generation", "chat"]
    
  balanced_provider:
    priority: 2
    type: primary
    cost_per_token: 0.001
    capabilities: ["text-generation", "chat", "code-generation"]
    
  premium_provider:
    priority: 10
    type: backup
    cost_per_token: 0.002
    capabilities: ["text-generation", "chat", "code-generation", "analysis"]
    
  local_fallback:
    priority: 100
    type: backup
    cost_per_token: 0
    capabilities: ["text-generation"]
```

## Monitoring and Optimization

### Cost Tracking Metrics

Track these metrics to optimize cost routing:

**Per-Provider Costs:**
- Average cost per request
- Total monthly spending
- Cost per token by model type

**Per-Rule Costs:**
- Costs generated by each routing rule
- Cost savings from optimization rules
- Rule effectiveness metrics

**User-Level Costs:**
- Cost distribution by user tier
- High-cost user identification
- Budget adherence tracking

### Cost Optimization Alerts

Set up alerts for:
- Users approaching budget limits
- Providers with increasing costs
- Rules generating unexpected costs
- Cost threshold violations

### Regular Cost Review

Monthly cost optimization review:

1. **Analyze Cost Patterns**: Review spending by provider, model, and user
2. **Update Thresholds**: Adjust cost limits based on usage patterns
3. **Provider Performance**: Evaluate cost vs. quality trade-offs
4. **Rule Effectiveness**: Measure cost savings from optimization rules

## Best Practices

### Cost Control Strategy

1. **Start Conservative**: Begin with lower cost thresholds and adjust upward
2. **Monitor Closely**: Track cost impact of routing changes
3. **User Communication**: Inform users about cost-based limitations
4. **Graceful Degradation**: Provide alternatives when cost limits are hit

### Cost Threshold Management

1. **Tiered Approach**: Different thresholds for different user types
2. **Model-Specific**: Adjust thresholds based on model capabilities
3. **Time-Based**: Lower thresholds during peak usage periods
4. **Dynamic Adjustment**: Update thresholds based on provider pricing

### Provider Selection for Cost Optimization

1. **Cost Comparison**: Regularly compare provider costs
2. **Quality Metrics**: Balance cost with quality requirements
3. **Capability Matching**: Ensure cost-effective providers meet needs
4. **Geographic Considerations**: Factor in regional pricing differences

### Emergency Cost Controls

1. **Circuit Breakers**: Automatic stops for runaway costs
2. **Emergency Thresholds**: Ultra-low limits for crisis situations
3. **Manual Overrides**: Admin ability to bypass cost controls
4. **Incident Response**: Procedures for cost-related incidents

## Troubleshooting

### Common Cost Control Issues

**Cost Limits Too Restrictive:**
- Symptoms: High rejection rates, user complaints
- Solution: Analyze usage patterns and increase limits gradually

**Cost Savings Not Materializing:**
- Symptoms: Similar costs despite optimization rules
- Solution: Review rule priorities and provider selection

**Quality Impact from Cost Optimization:**
- Symptoms: User complaints about response quality
- Solution: Balance cost and quality requirements

**Complex Cost Calculations:**
- Symptoms: Unpredictable cost behavior
- Solution: Simplify cost rules and add monitoring