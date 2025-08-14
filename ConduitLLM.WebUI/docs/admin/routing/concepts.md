# Routing Concepts

## Core Concepts

### Routing Rules

A **routing rule** is a configuration that determines how requests should be handled based on specific conditions. Each rule consists of:

- **Name**: A descriptive identifier for the rule
- **Description**: Optional documentation explaining the rule's purpose
- **Priority**: Numeric value determining evaluation order (lower = higher priority)
- **Enabled Status**: Whether the rule is active
- **Conditions**: Criteria that must be met for the rule to apply
- **Actions**: What to do when the rule matches

### Conditions

**Conditions** define the criteria for when a rule should apply. They consist of:

- **Type**: The category of data to evaluate (model, region, cost, metadata, etc.)
- **Field**: Specific field within the type (if applicable)
- **Operator**: How to compare the values (equals, contains, greater than, etc.)
- **Value**: The expected value to match against

#### Condition Types

| Type | Description | Example Fields |
|------|-------------|----------------|
| `model` | Model-related conditions | `name`, `provider`, `capabilities` |
| `region` | Geographic routing | `user_region`, `provider_region` |
| `cost` | Cost-based routing | `cost_per_token`, `monthly_budget` |
| `metadata` | Custom request metadata | `user_id`, `organization`, `priority` |
| `headers` | HTTP headers | `x-api-version`, `authorization` |
| `time` | Time-based conditions | `hour`, `day_of_week`, `timezone` |

#### Operators

| Operator | Description | Compatible Types |
|----------|-------------|------------------|
| `equals` | Exact match | All types |
| `not_equals` | Not equal to | All types |
| `contains` | Contains substring | String types |
| `not_contains` | Does not contain | String types |
| `in` | Value in list | All types |
| `not_in` | Value not in list | All types |
| `greater_than` | Numeric comparison | Numbers, dates |
| `less_than` | Numeric comparison | Numbers, dates |
| `greater_than_or_equal` | Numeric comparison | Numbers, dates |
| `less_than_or_equal` | Numeric comparison | Numbers, dates |
| `regex` | Regular expression | String types |
| `starts_with` | String prefix | String types |
| `ends_with` | String suffix | String types |

### Actions

**Actions** define what happens when a rule's conditions are met:

#### Action Types

| Action Type | Description | Parameters |
|-------------|-------------|------------|
| `route_to_provider` | Route to specific provider | `provider_id` |
| `set_cost_threshold` | Apply cost limit | `max_cost_per_token` |
| `set_fallback_chain` | Define provider fallbacks | `provider_ids[]` |
| `add_metadata` | Add request metadata | `key`, `value` |
| `set_timeout` | Override request timeout | `timeout_ms` |
| `enable_caching` | Enable response caching | `cache_ttl` |
| `reject_request` | Block the request | `reason` |

### Provider Priority

**Provider Priority** determines the default order in which providers are tried when no specific routing rule applies. This creates a fallback chain:

1. **Primary Provider**: First choice for requests
2. **Secondary Provider**: Used if primary fails
3. **Tertiary Provider**: Used if both primary and secondary fail

### Rule Evaluation Process

The routing engine follows this process:

1. **Load Rules**: Retrieve all enabled rules, sorted by priority
2. **Evaluate Conditions**: Check each rule's conditions against the request
3. **Apply Actions**: Execute actions from the first matching rule
4. **Fallback to Priority**: If no rules match, use provider priority order
5. **Provider Selection**: Choose the highest priority available provider

### Evaluation Timeline

The **evaluation timeline** provides debugging information showing:

- **Step Number**: Order of evaluation
- **Action**: What was evaluated or executed
- **Duration**: How long each step took
- **Success/Failure**: Whether the step succeeded
- **Details**: Additional context about the step

### Provider Selection Reasoning

The system provides detailed reasoning for provider selection:

- **Strategy Used**: Priority-based, rule-based, or fallback
- **Rule Applied**: Which rule (if any) determined the selection
- **Fallback Chain**: List of providers in fallback order
- **Selection Reason**: Why the specific provider was chosen

## Routing Strategies

### Priority-Based Routing

The simplest strategy where providers are tried in priority order:

```
Request → Check Priority 1 → Available? → Use Provider 1
                          → Unavailable? → Check Priority 2 → Use Provider 2
```

### Rule-Based Routing

Advanced strategy where custom rules determine routing:

```
Request → Evaluate Rule 1 → Match? → Apply Actions → Route to Specific Provider
                         → No Match? → Evaluate Rule 2 → Continue...
                                                      → No Rules Match? → Use Priority Order
```

### Hybrid Routing

Combination of rules and priorities:

```
Request → Check Custom Rules → Rule Match? → Follow Rule Actions
                            → No Match? → Use Provider Priority Order
```

## Best Practices

### Rule Design

1. **Keep Rules Simple**: Simpler conditions are easier to understand and faster to evaluate
2. **Use Descriptive Names**: Make rules easy to identify and maintain
3. **Document Complex Logic**: Add descriptions for complex rules
4. **Test Thoroughly**: Use the testing interface to verify rule behavior

### Priority Management

1. **Start Simple**: Begin with basic priority order before adding complex rules
2. **Monitor Performance**: Check that routing decisions don't add significant latency
3. **Plan for Failure**: Ensure fallback chains cover common failure scenarios
4. **Regular Review**: Periodically review and update priorities based on performance

### Condition Optimization

1. **Order Matters**: Put most selective conditions first
2. **Use Appropriate Operators**: Choose the most efficient operator for your use case
3. **Avoid Complex Regex**: Use simpler string operations when possible
4. **Cache When Possible**: Enable caching for stable routing decisions

### Action Guidelines

1. **Single Responsibility**: Each rule should have a clear, single purpose
2. **Avoid Conflicts**: Ensure rules don't contradict each other
3. **Fail Gracefully**: Always provide fallback options
4. **Monitor Results**: Track the effectiveness of routing decisions

## Performance Characteristics

### Rule Evaluation Performance

- **Linear Evaluation**: Rules are evaluated in priority order until a match is found
- **Short-Circuit Logic**: Evaluation stops at the first matching rule
- **Condition Caching**: Some condition results may be cached for performance
- **Target Performance**: Rule evaluation should complete in under 10ms for most cases

### Scaling Considerations

- **Rule Count**: Performance degrades linearly with the number of rules
- **Condition Complexity**: Complex conditions (regex, nested logic) take longer to evaluate
- **Provider Health**: Routing performance depends on provider availability checks
- **Caching Strategy**: Effective caching can significantly improve performance

## Common Patterns

### Geographic Routing
Route requests based on user or data location for compliance or performance reasons.

### Cost Optimization
Implement cost controls by routing expensive operations to specific providers or applying cost thresholds.

### High Availability
Set up robust fallback chains to ensure service continuity during provider outages.

### Load Balancing
Distribute requests across multiple providers to optimize resource utilization.

### Feature-Based Routing
Route requests to providers that support specific features or capabilities.

See the [Examples](./examples/) directory for detailed implementations of these patterns.