# Fallback Chain Examples

This document provides examples of robust fallback chain configurations for high availability and resilience.

## Overview

Fallback chains ensure service continuity by:
- **Automatic failover** when primary providers are unavailable
- **Graceful degradation** through multiple backup options
- **Load distribution** across equivalent providers
- **Disaster recovery** for critical service scenarios

## Example 1: Basic High Availability Chain

### Scenario
Simple three-tier fallback for reliable service delivery.

### Configuration

**Rule: "Basic HA Fallback"**
```yaml
name: "Basic HA Fallback"
description: "Standard high availability fallback chain"
priority: 100
enabled: true

conditions:
  - type: metadata
    field: require_high_availability
    operator: equals
    value: "true"

actions:
  - type: set_fallback_chain
    targets: [
      "primary-provider",      # Tier 1: Best performance
      "secondary-provider",    # Tier 2: Good performance  
      "tertiary-provider"      # Tier 3: Basic service
    ]
  - type: set_timeout
    value: 30000
  - type: add_metadata
    key: "ha_strategy"
    value: "three_tier_fallback"
```

**Provider Configuration:**
```yaml
providers:
  primary_provider:
    priority: 1
    type: "primary"
    sla_target: "99.9%"
    response_time_target: "50ms"
    
  secondary_provider:
    priority: 10
    type: "backup"
    sla_target: "99.5%"
    response_time_target: "100ms"
    
  tertiary_provider:
    priority: 20
    type: "backup"
    sla_target: "99.0%"
    response_time_target: "200ms"
```

**Expected Behavior:**
- Try primary provider first
- Automatic failover to secondary if primary fails
- Fall back to tertiary if both primary and secondary fail
- Each tier provides progressively more basic service

### Testing
```yaml
test_scenarios:
  normal_operation:
    config:
      metadata:
        require_high_availability: "true"
    expected_provider: "primary-provider"
    
  primary_down:
    config:
      metadata:
        require_high_availability: "true"
      simulate_failure: "primary-provider"
    expected_provider: "secondary-provider"
    
  primary_and_secondary_down:
    config:
      metadata:
        require_high_availability: "true"
      simulate_failure: ["primary-provider", "secondary-provider"]
    expected_provider: "tertiary-provider"
```

## Example 2: Geographic Redundancy Chain

### Scenario
Multi-region fallback chain for geographic resilience.

### Configuration

**Rule: "Geographic Redundancy"**
```yaml
name: "Geographic Redundancy"
description: "Multi-region fallback for disaster recovery"
priority: 50
enabled: true

conditions:
  - type: region
    field: user_region
    operator: starts_with
    value: "us-"

actions:
  - type: set_fallback_chain
    targets: [
      "us-east-primary",       # Same region
      "us-west-backup",        # Different US region
      "ca-central-backup",     # Adjacent country
      "eu-west-emergency",     # Cross-continental
      "global-last-resort"     # Global fallback
    ]
  - type: add_metadata
    key: "fallback_strategy"
    value: "geographic_redundancy"
```

**Provider Geographic Distribution:**
```yaml
geographic_providers:
  us_east_primary:
    region: "us-east-1"
    availability_zone: "us-east-1a"
    disaster_recovery: "us-west-backup"
    
  us_west_backup:
    region: "us-west-2"
    availability_zone: "us-west-2a"
    disaster_recovery: "ca-central-backup"
    
  ca_central_backup:
    region: "ca-central-1"
    availability_zone: "ca-central-1a"
    disaster_recovery: "eu-west-emergency"
    
  eu_west_emergency:
    region: "eu-west-1"
    availability_zone: "eu-west-1a"
    disaster_recovery: "global-last-resort"
    
  global_last_resort:
    region: "global"
    type: "distributed"
    availability: "99.99%"
```

### Testing
```yaml
disaster_scenarios:
  regional_outage:
    description: "US East region completely down"
    simulation:
      failed_regions: ["us-east-1"]
    expected_provider: "us-west-backup"
    
  country_wide_outage:
    description: "All US regions down"
    simulation:
      failed_regions: ["us-east-1", "us-west-2"]
    expected_provider: "ca-central-backup"
    
  continental_outage:
    description: "North America regions down"
    simulation:
      failed_regions: ["us-east-1", "us-west-2", "ca-central-1"]
    expected_provider: "eu-west-emergency"
```

## Example 3: Performance-Tiered Fallback

### Scenario
Fallback chain optimized for different performance characteristics.

### Configuration

**Rule: "Performance Tiered Fallback"**
```yaml
name: "Performance Tiered Fallback"
description: "Fallback based on performance characteristics"
priority: 75
enabled: true

conditions:
  - type: model
    field: name
    operator: in
    value: ["gpt-4", "claude-3-opus"]

actions:
  - type: set_fallback_chain
    targets: [
      "ultra-fast-provider",    # < 50ms response time
      "fast-provider",          # < 100ms response time
      "standard-provider",      # < 200ms response time
      "slow-but-reliable"       # < 500ms but 99.99% uptime
    ]
  - type: add_metadata
    key: "performance_tier_used"
    value: "dynamic"
```

**Performance Characteristics:**
```yaml
performance_tiers:
  ultra_fast_provider:
    avg_response_time: "30ms"
    p95_response_time: "45ms"
    availability: "99.5%"
    cost_multiplier: 3.0
    
  fast_provider:
    avg_response_time: "75ms"
    p95_response_time: "90ms"
    availability: "99.7%"
    cost_multiplier: 1.5
    
  standard_provider:
    avg_response_time: "150ms"
    p95_response_time: "180ms"
    availability: "99.8%"
    cost_multiplier: 1.0
    
  slow_but_reliable:
    avg_response_time: "300ms"
    p95_response_time: "450ms"
    availability: "99.99%"
    cost_multiplier: 0.7
```

## Example 4: Cost-Optimized Fallback

### Scenario
Fallback chain that balances cost and availability.

### Configuration

**Rule: "Cost-Optimized Fallback"**
```yaml
name: "Cost-Optimized Fallback"
description: "Fallback chain optimized for cost efficiency"
priority: 80
enabled: true

conditions:
  - type: metadata
    field: cost_optimization
    operator: equals
    value: "enabled"

actions:
  - type: set_fallback_chain
    targets: [
      "cost-effective-primary",     # Best cost/performance ratio
      "budget-secondary",           # Lower cost, acceptable quality
      "free-tier-backup",          # Free but limited
      "emergency-premium"          # Expensive but guaranteed
    ]
  - type: set_cost_threshold
    value: 0.001
  - type: add_metadata
    key: "cost_strategy"
    value: "optimized_fallback"
```

**Cost Structure:**
```yaml
cost_tiers:
  cost_effective_primary:
    cost_per_token: 0.0008
    quality_score: 8.5
    availability: "99.5%"
    
  budget_secondary:
    cost_per_token: 0.0005
    quality_score: 7.0
    availability: "99.0%"
    
  free_tier_backup:
    cost_per_token: 0.0000
    quality_score: 6.0
    availability: "95.0%"
    rate_limits: "100 requests/hour"
    
  emergency_premium:
    cost_per_token: 0.0020
    quality_score: 9.5
    availability: "99.99%"
    guaranteed_capacity: true
```

## Example 5: Capability-Based Fallback

### Scenario
Fallback based on provider capabilities and model support.

### Configuration

**Rule: "Capability-Based Fallback"**
```yaml
name: "Capability-Based Fallback"
description: "Fallback based on model capabilities"
priority: 60
enabled: true

conditions:
  - type: model
    field: capability
    operator: equals
    value: "code-generation"

actions:
  - type: set_fallback_chain
    targets: [
      "code-specialist-provider",   # Best for code
      "general-ai-provider",        # Good general capability
      "basic-text-provider",        # Basic text generation
      "local-code-model"           # Local fallback
    ]
  - type: add_metadata
    key: "capability_routing"
    value: "code_generation"
```

**Capability Matrix:**
```yaml
provider_capabilities:
  code_specialist_provider:
    capabilities:
      code_generation: 9.5
      text_generation: 7.0
      analysis: 6.0
    models: ["codex", "code-davinci", "github-copilot"]
    
  general_ai_provider:
    capabilities:
      code_generation: 7.5
      text_generation: 9.0
      analysis: 8.5
    models: ["gpt-4", "claude-3", "gemini-pro"]
    
  basic_text_provider:
    capabilities:
      code_generation: 5.0
      text_generation: 8.0
      analysis: 6.0
    models: ["gpt-3.5", "llama-2"]
    
  local_code_model:
    capabilities:
      code_generation: 6.0
      text_generation: 4.0
      analysis: 3.0
    models: ["code-llama-local"]
    deployment: "on-premises"
```

## Example 6: Load-Balanced Fallback Pools

### Scenario
Multiple providers at each fallback level for load distribution.

### Configuration

**Rule: "Load-Balanced Fallback Pools"**
```yaml
name: "Load-Balanced Fallback Pools"
description: "Multiple providers per fallback tier with load balancing"
priority: 90
enabled: true

conditions:
  - type: metadata
    field: load_balancing
    operator: equals
    value: "enabled"

actions:
  - type: set_fallback_chain
    targets: [
      # Tier 1: Multiple primary providers (load balanced)
      ["primary-a", "primary-b", "primary-c"],
      
      # Tier 2: Multiple secondary providers (load balanced)
      ["secondary-a", "secondary-b"],
      
      # Tier 3: Single tertiary provider
      "tertiary-provider"
    ]
  - type: add_metadata
    key: "fallback_type"
    value: "load_balanced_pools"
```

**Load Balancing Configuration:**
```yaml
load_balancing_pools:
  tier_1_primaries:
    providers:
      primary_a:
        weight: 40
        max_concurrent: 100
        health_threshold: 95
      primary_b:
        weight: 35
        max_concurrent: 80
        health_threshold: 95
      primary_c:
        weight: 25
        max_concurrent: 60
        health_threshold: 90
    strategy: "weighted_round_robin"
    
  tier_2_secondaries:
    providers:
      secondary_a:
        weight: 60
        max_concurrent: 50
        health_threshold: 90
      secondary_b:
        weight: 40
        max_concurrent: 40
        health_threshold: 90
    strategy: "least_connections"
```

## Example 7: Intelligent Circuit Breaker Fallback

### Scenario
Advanced fallback with circuit breaker patterns for rapid failure detection.

### Configuration

**Rule: "Circuit Breaker Fallback"**
```yaml
name: "Circuit Breaker Fallback"
description: "Advanced fallback with circuit breaker protection"
priority: 40
enabled: true

conditions:
  - type: metadata
    field: circuit_breaker
    operator: equals
    value: "enabled"

actions:
  - type: set_fallback_chain
    targets: [
      "monitored-primary",
      "monitored-secondary", 
      "monitored-tertiary",
      "emergency-bypass"
    ]
  - type: add_metadata
    key: "circuit_breaker_config"
    value: "standard"
```

**Circuit Breaker Configuration:**
```yaml
circuit_breaker_settings:
  monitored_primary:
    failure_threshold: 5        # Failures before opening circuit
    timeout: 60000             # Time before retry (ms)
    success_threshold: 3       # Successes needed to close circuit
    monitoring_window: 30000   # Sliding window for failure detection
    
  monitored_secondary:
    failure_threshold: 3
    timeout: 30000
    success_threshold: 2
    monitoring_window: 20000
    
  monitored_tertiary:
    failure_threshold: 2
    timeout: 15000
    success_threshold: 1
    monitoring_window: 10000
    
  emergency_bypass:
    circuit_breaker: disabled  # Always available
    rate_limit: 10             # Requests per minute
```

## Fallback Chain Monitoring

### Key Metrics

**Fallback Usage Metrics:**
- Fallback activation frequency
- Provider failure rates
- Recovery times
- Fallback depth (how far down the chain)

**Performance Impact:**
- Latency increase per fallback level
- Success rates at each tier
- User experience degradation
- Cost implications of fallbacks

**Health Monitoring:**
```yaml
fallback_health_metrics:
  provider_availability:
    metric: "uptime_percentage"
    target: "> 99.5%"
    alert_threshold: "< 99.0%"
    
  fallback_frequency:
    metric: "fallback_activations_per_hour"
    target: "< 10"
    alert_threshold: "> 50"
    
  recovery_time:
    metric: "time_to_primary_recovery"
    target: "< 5 minutes"
    alert_threshold: "> 15 minutes"
    
  chain_depth:
    metric: "average_fallback_depth"
    target: "< 1.5"
    alert_threshold: "> 2.0"
```

## Best Practices

### Fallback Chain Design

1. **Logical Progression**: Each fallback should be genuinely different (region, provider, technology)
2. **Capability Matching**: Ensure fallback providers can handle the workload
3. **Performance Degradation**: Plan for graceful performance reduction
4. **Cost Awareness**: Consider cost implications of each fallback tier

### Provider Selection for Fallbacks

1. **Diversity**: Use different technologies, regions, or vendors
2. **Capacity Planning**: Ensure fallback providers can handle redirected load
3. **SLA Alignment**: Match fallback capabilities to service requirements
4. **Regular Testing**: Verify fallback providers work under load

### Circuit Breaker Implementation

1. **Failure Detection**: Quick identification of provider issues
2. **Automatic Recovery**: Self-healing when providers recover
3. **Gradual Restoration**: Careful re-introduction of recovered providers
4. **Monitoring Integration**: Alert on circuit breaker activations

### Testing and Validation

1. **Chaos Engineering**: Regularly test failure scenarios
2. **Load Testing**: Verify fallback capacity under realistic loads
3. **End-to-End Testing**: Test complete fallback chains
4. **Recovery Testing**: Verify smooth recovery when providers return

## Troubleshooting

### Common Fallback Issues

**Cascading Failures:**
- Symptoms: Multiple providers failing simultaneously
- Solutions: Increase provider diversity, implement circuit breakers

**Slow Failover:**
- Symptoms: Long delays when switching providers
- Solutions: Reduce timeout values, improve health checks

**Fallback Overload:**
- Symptoms: Fallback providers becoming overwhelmed
- Solutions: Implement rate limiting, scale fallback capacity

**Recovery Problems:**
- Symptoms: Providers not returning to service after recovery
- Solutions: Implement gradual recovery, monitor health metrics

### Debugging Fallback Behavior

Use the testing interface to:

1. **Simulate Failures**: Test behavior with specific providers down
2. **Measure Timing**: Check fallback activation times
3. **Verify Routing**: Ensure correct fallback provider selection
4. **Test Recovery**: Verify smooth transition back to primary

### Emergency Procedures

**Mass Provider Failure:**
1. Activate emergency fallback providers
2. Implement rate limiting to protect remaining capacity
3. Communicate service impacts to users
4. Monitor recovery and gradually restore normal service

**Fallback Chain Exhaustion:**
1. Implement emergency request queuing
2. Activate additional emergency capacity
3. Consider degraded service modes
4. Escalate to disaster recovery procedures