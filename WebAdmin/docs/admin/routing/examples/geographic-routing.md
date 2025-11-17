# Geographic Routing Examples

This document provides examples of routing configurations for geographic compliance, performance optimization, and regional requirements.

## Overview

Geographic routing enables:
- **Data sovereignty compliance** - Keep data within specific regions
- **Latency optimization** - Route to geographically closer providers
- **Legal compliance** - Meet regional data protection requirements
- **Performance optimization** - Reduce network latency and improve response times

## Example 1: EU Data Compliance (GDPR)

### Scenario
Ensure all EU user data stays within EU boundaries for GDPR compliance.

### Configuration

**Rule: "EU Data Sovereignty"**
```yaml
name: "EU Data Sovereignty"
description: "Route EU user requests to EU-based providers for GDPR compliance"
priority: 1
enabled: true

conditions:
  - type: region
    field: user_region
    operator: starts_with
    value: "eu-"

actions:
  - type: set_fallback_chain
    targets: ["azure-eu-west", "azure-eu-north", "gcp-eu-west1"]
  - type: add_metadata
    key: "compliance_reason"
    value: "gdpr_data_sovereignty"
  - type: add_metadata
    key: "data_region"
    value: "eu"
```

**Expected Behavior:**
- All requests from EU regions use only EU providers
- Fallback chain ensures availability within EU
- Metadata tracks compliance routing

### Testing
```yaml
test_config:
  model: "gpt-3.5-turbo"
  region: "eu-west-1"
  metadata:
    user_location: "Germany"

expected_result:
  rule_applied: "EU Data Sovereignty"
  provider_region: "eu"
  fallback_chain: ["azure-eu-west", "azure-eu-north", "gcp-eu-west1"]
  metadata_added:
    compliance_reason: "gdpr_data_sovereignty"
    data_region: "eu"
```

## Example 2: US Regional Optimization

### Scenario
Route US users to the closest regional provider for optimal performance.

### Configuration

**Rule: "US East Coast Optimization"**
```yaml
name: "US East Coast Optimization"
description: "Route US East Coast users to nearby providers"
priority: 10
enabled: true

conditions:
  - type: region
    field: user_region
    operator: in
    value: ["us-east-1", "us-east-2", "ca-central-1"]

actions:
  - type: set_fallback_chain
    targets: ["aws-us-east-1", "azure-us-east", "gcp-us-east1"]
  - type: set_timeout
    value: 15000  # Shorter timeout for nearby providers
  - type: add_metadata
    key: "optimization_type"
    value: "regional_latency"
```

**Rule: "US West Coast Optimization"**
```yaml
name: "US West Coast Optimization"
description: "Route US West Coast users to nearby providers"
priority: 11
enabled: true

conditions:
  - type: region
    field: user_region
    operator: in
    value: ["us-west-1", "us-west-2"]

actions:
  - type: set_fallback_chain
    targets: ["aws-us-west-2", "azure-us-west", "gcp-us-west1"]
  - type: set_timeout
    value: 15000
  - type: add_metadata
    key: "optimization_type"
    value: "regional_latency"
```

### Testing
```yaml
# East Coast Test
test_config_east:
  model: "gpt-4"
  region: "us-east-1"

expected_result_east:
  rule_applied: "US East Coast Optimization"
  fallback_chain: ["aws-us-east-1", "azure-us-east", "gcp-us-east1"]

# West Coast Test
test_config_west:
  model: "gpt-4"
  region: "us-west-2"

expected_result_west:
  rule_applied: "US West Coast Optimization"
  fallback_chain: ["aws-us-west-2", "azure-us-west", "gcp-us-west1"]
```

## Example 3: Asia-Pacific Regional Distribution

### Scenario
Distribute requests across APAC regions based on user location and provider availability.

### Configuration

**Rule: "APAC Regional Distribution"**
```yaml
name: "APAC Regional Distribution"
description: "Optimize routing for Asia-Pacific users"
priority: 15
enabled: true

conditions:
  - type: region
    field: user_region
    operator: regex
    value: "^(ap-|asia-)"

actions:
  - type: route_to_provider
    target: "apac-load-balancer"
  - type: set_fallback_chain
    targets: ["gcp-asia-southeast1", "aws-ap-southeast-1", "azure-asia-east"]
  - type: add_metadata
    key: "regional_group"
    value: "apac"
```

**Rule: "Australia/New Zealand Specific"**
```yaml
name: "Australia Data Residency"
description: "Keep Australian data within Australia for compliance"
priority: 5
enabled: true

conditions:
  - type: region
    field: user_region
    operator: in
    value: ["ap-southeast-2", "australia-east"]
  - type: metadata
    field: data_classification
    operator: equals
    value: "sensitive"

actions:
  - type: set_fallback_chain
    targets: ["aws-ap-southeast-2", "azure-australia-east"]
  - type: add_metadata
    key: "compliance_reason"
    value: "australian_data_residency"
```

## Example 4: Multi-Regional High Availability

### Scenario
Implement global high availability with intelligent regional failover.

### Configuration

**Rule: "Global High Availability"**
```yaml
name: "Global High Availability"
description: "Global failover with regional preference"
priority: 50
enabled: true

conditions:
  - type: metadata
    field: availability_requirement
    operator: equals
    value: "high"

actions:
  - type: set_fallback_chain
    targets: [
      "primary-regional-provider",
      "secondary-regional-provider", 
      "cross-region-provider-1",
      "cross-region-provider-2",
      "global-fallback"
    ]
  - type: set_timeout
    value: 45000
  - type: add_metadata
    key: "ha_strategy"
    value: "global_failover"
```

### Provider Configuration for High Availability

```yaml
# Regional providers with geographic distribution
providers:
  aws_us_east_1:
    region: "us-east-1"
    priority: 10
    health_check_url: "https://us-east-1.provider.com/health"
    
  azure_us_west:
    region: "us-west-2"
    priority: 15
    health_check_url: "https://us-west.azure.com/health"
    
  gcp_eu_west:
    region: "eu-west-1"
    priority: 20
    health_check_url: "https://eu-west1.gcp.com/health"
    
  global_fallback:
    region: "global"
    priority: 100
    health_check_url: "https://global.fallback.com/health"
```

## Example 5: Compliance-Based Geographic Routing

### Scenario
Route based on data classification and regulatory requirements.

### Configuration

**Rule: "Financial Data Compliance"**
```yaml
name: "Financial Data Compliance"
description: "Route financial data according to regulatory requirements"
priority: 1
enabled: true

conditions:
  - type: metadata
    field: data_type
    operator: equals
    value: "financial"
  - type: region
    field: user_region
    operator: starts_with
    value: "us-"

actions:
  - type: set_fallback_chain
    targets: ["soc2-compliant-us-provider", "fedramp-provider"]
  - type: add_metadata
    key: "compliance_framework"
    value: "sox_pci_dss"
  - type: enable_caching
    value: 0  # Disable caching for financial data
```

**Rule: "Healthcare Data Compliance (HIPAA)"**
```yaml
name: "Healthcare Data HIPAA Compliance"
description: "Route healthcare data to HIPAA-compliant providers"
priority: 1
enabled: true

conditions:
  - type: metadata
    field: data_type
    operator: equals
    value: "healthcare"

actions:
  - type: route_to_provider
    target: "hipaa-compliant-provider"
  - type: add_metadata
    key: "compliance_framework"
    value: "hipaa"
  - type: add_metadata
    key: "encryption_required"
    value: "true"
```

## Example 6: Latency-Optimized Geographic Routing

### Scenario
Route requests to minimize latency based on geographic proximity.

### Configuration

**Rule: "Latency Optimization"**
```yaml
name: "Latency Optimization"
description: "Route to geographically closest provider for minimal latency"
priority: 30
enabled: true

conditions:
  - type: metadata
    field: performance_priority
    operator: equals
    value: "latency"

actions:
  - type: route_to_provider
    target: "closest-provider"  # Dynamically determined
  - type: set_timeout
    value: 10000  # Shorter timeout for nearby providers
  - type: add_metadata
    key: "routing_strategy"
    value: "latency_optimized"
```

### Geographic Provider Mapping

```yaml
# Provider-to-region mapping for latency optimization
geographic_mapping:
  us_east_regions: ["us-east-1", "us-east-2", "ca-central-1"]
  us_west_regions: ["us-west-1", "us-west-2"]
  eu_regions: ["eu-west-1", "eu-central-1", "eu-north-1"]
  apac_regions: ["ap-southeast-1", "ap-northeast-1", "ap-south-1"]

provider_regions:
  aws_us_east: ["us-east-1", "us-east-2"]
  aws_us_west: ["us-west-1", "us-west-2"]
  azure_eu: ["eu-west-1", "eu-central-1"]
  gcp_apac: ["ap-southeast-1", "ap-northeast-1"]
```

## Provider Priority Configuration for Geographic Routing

### Regional Provider Setup

```yaml
# US East Coast Providers
us_east_providers:
  aws_us_east_1:
    priority: 1
    region: "us-east-1"
    latency_target: 50ms
    
  azure_us_east:
    priority: 2
    region: "us-east-1"
    latency_target: 60ms
    
  gcp_us_east1:
    priority: 3
    region: "us-east1"
    latency_target: 55ms

# EU Providers
eu_providers:
  azure_eu_west:
    priority: 1
    region: "eu-west-1"
    compliance: ["gdpr", "data_residency"]
    
  gcp_eu_west1:
    priority: 2
    region: "eu-west1"
    compliance: ["gdpr", "data_residency"]
    
  aws_eu_west_1:
    priority: 3
    region: "eu-west-1"
    compliance: ["gdpr", "data_residency"]

# APAC Providers
apac_providers:
  gcp_asia_southeast1:
    priority: 1
    region: "asia-southeast1"
    countries_served: ["Singapore", "Malaysia", "Thailand"]
    
  aws_ap_southeast_1:
    priority: 2
    region: "ap-southeast-1" 
    countries_served: ["Singapore", "Indonesia", "Philippines"]
    
  azure_asia_east:
    priority: 3
    region: "asia-east"
    countries_served: ["Hong Kong", "Taiwan", "South Korea"]
```

## Monitoring Geographic Routing

### Key Metrics to Track

**Latency Metrics:**
- Average response time by region
- 95th percentile latency by provider
- Cross-region latency measurements
- Regional performance comparisons

**Compliance Metrics:**
- Data residency adherence rates
- Compliance framework coverage
- Audit trail completeness
- Regional regulation compliance

**Availability Metrics:**
- Regional provider uptime
- Cross-region failover frequency
- Regional disaster recovery tests
- Geographic redundancy coverage

### Geographic Performance Dashboard

Track these KPIs:

```yaml
geographic_kpis:
  latency:
    target: "< 100ms 95th percentile"
    measurement: "by region pair"
    
  compliance:
    target: "100% data residency adherence"
    measurement: "by regulation framework"
    
  availability:
    target: "99.9% uptime per region"
    measurement: "by provider and region"
    
  coverage:
    target: "3+ providers per major region"
    measurement: "redundancy factor"
```

## Best Practices

### Geographic Routing Strategy

1. **Compliance First**: Always prioritize regulatory compliance over performance
2. **Redundancy Planning**: Ensure multiple providers per region
3. **Latency Monitoring**: Continuously measure and optimize latency
4. **Regular Testing**: Test cross-region failover scenarios

### Data Sovereignty Management

1. **Clear Boundaries**: Define exact geographic boundaries for data
2. **Audit Trails**: Maintain complete records of data location
3. **Regular Reviews**: Periodically review compliance requirements
4. **Incident Response**: Have procedures for compliance violations

### Performance Optimization

1. **Baseline Measurements**: Establish latency baselines for each region
2. **Dynamic Routing**: Adjust routing based on real-time performance
3. **Edge Optimization**: Consider edge computing for latency reduction
4. **CDN Integration**: Use CDNs for static content and caching

### Provider Management

1. **Regional Contracts**: Negotiate region-specific agreements
2. **Compliance Verification**: Regularly verify provider compliance
3. **Performance SLAs**: Establish region-specific performance targets
4. **Disaster Recovery**: Plan for regional provider failures

## Troubleshooting

### Common Geographic Routing Issues

**High Cross-Region Latency:**
- Symptoms: Slow response times for certain regions
- Solutions: Add regional providers, optimize routing rules

**Compliance Violations:**
- Symptoms: Data flowing to incorrect regions
- Solutions: Review rule priorities, add compliance checks

**Regional Provider Failures:**
- Symptoms: No available providers in region
- Solutions: Implement cross-region fallbacks, increase redundancy

**Inconsistent Regional Performance:**
- Symptoms: Variable performance within same region
- Solutions: Load balance across regional providers, monitor health

### Debugging Geographic Issues

Use the testing interface to:

1. **Test Regional Routing**: Verify requests route to correct regions
2. **Check Compliance**: Ensure compliance metadata is applied
3. **Measure Latency**: Test response times for different regions
4. **Validate Fallbacks**: Test cross-region failover scenarios

### Emergency Procedures

**Regional Disaster Recovery:**
1. Activate cross-region fallback providers
2. Update routing rules to exclude affected region
3. Monitor performance impact of routing changes
4. Communicate with users about service impacts

**Compliance Incident Response:**
1. Immediately halt non-compliant routing
2. Audit affected requests and data
3. Implement corrective routing rules
4. Report to compliance team and stakeholders