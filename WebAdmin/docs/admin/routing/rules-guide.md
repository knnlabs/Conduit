# Rules Guide

## Creating Routing Rules

This guide walks you through creating and managing routing rules in the Conduit admin interface.

## Accessing the Rule Builder

1. Navigate to **Routing Settings** in the admin dashboard
2. Click on the **Routing Rules** tab
3. Click **Add Rule** to create a new rule or click an existing rule to edit it

## Rule Components

### Basic Information

#### Rule Name
- **Purpose**: Unique identifier for the rule
- **Requirements**: Must be unique across all rules
- **Best Practice**: Use descriptive names that explain the rule's purpose
- **Examples**: 
  - "Route GPT-4 to OpenAI Primary"
  - "EU Users to EU Providers"
  - "High Priority Users Fast Lane"

#### Description
- **Purpose**: Documentation for the rule's intent and logic
- **Optional**: Not required but highly recommended
- **Best Practice**: Explain why the rule exists and what it accomplishes
- **Example**: "Routes all GPT-4 requests to the primary OpenAI provider to ensure best performance and availability"

#### Priority
- **Purpose**: Determines the order of rule evaluation
- **Range**: 1-1000 (lower numbers = higher priority)
- **Default**: Auto-assigned based on creation order
- **Important**: Rules are evaluated in priority order until the first match

#### Enabled Status
- **Purpose**: Controls whether the rule is active
- **Default**: Enabled
- **Use Case**: Temporarily disable rules without deleting them

### Conditions

Conditions define when a rule should apply. All conditions in a rule must be satisfied for the rule to match.

#### Adding Conditions

1. Click **Add Condition** in the rule builder
2. Select the **Condition Type** from the dropdown
3. Choose the appropriate **Field** (if applicable)
4. Select the **Operator** for comparison
5. Enter the **Value** to match against

#### Condition Examples

**Model-based Routing:**
```
Type: model
Field: name
Operator: equals
Value: gpt-4
```

**Geographic Routing:**
```
Type: region
Field: user_region
Operator: equals
Value: eu-west-1
```

**Cost-based Routing:**
```
Type: cost
Field: cost_per_token
Operator: greater_than
Value: 0.001
```

**Metadata-based Routing:**
```
Type: metadata
Field: priority
Operator: equals
Value: high
```

**Time-based Routing:**
```
Type: time
Field: hour
Operator: greater_than_or_equal
Value: 18
```

#### Multiple Conditions

When you add multiple conditions to a rule, **ALL** conditions must be satisfied:

```
Condition 1: model.name equals "gpt-4"
AND
Condition 2: region.user_region equals "us-east-1"
AND
Condition 3: metadata.priority equals "high"
```

### Actions

Actions define what happens when a rule matches. You can specify multiple actions for a single rule.

#### Available Actions

**Route to Provider:**
- **Purpose**: Send request to a specific provider
- **Parameters**: Provider ID
- **Example**: Route to "openai-primary"

**Set Cost Threshold:**
- **Purpose**: Apply maximum cost limit
- **Parameters**: Maximum cost per token (USD)
- **Example**: Set threshold to $0.002 per token

**Set Fallback Chain:**
- **Purpose**: Define provider fallback order
- **Parameters**: Ordered list of provider IDs
- **Example**: ["primary", "secondary", "tertiary"]

**Add Metadata:**
- **Purpose**: Attach additional data to the request
- **Parameters**: Key-value pairs
- **Example**: priority="high", source="rule-based"

**Set Timeout:**
- **Purpose**: Override default request timeout
- **Parameters**: Timeout in milliseconds
- **Example**: 30000ms for high-priority requests

**Enable Caching:**
- **Purpose**: Cache response for specified duration
- **Parameters**: Cache TTL in seconds
- **Example**: Cache for 3600 seconds (1 hour)

**Reject Request:**
- **Purpose**: Block the request
- **Parameters**: Rejection reason
- **Example**: "Model not available in this region"

#### Action Examples

**Simple Provider Routing:**
```yaml
Actions:
  - Type: route_to_provider
    Target: openai-primary
```

**Cost Control with Fallback:**
```yaml
Actions:
  - Type: set_cost_threshold
    Value: 0.001
  - Type: set_fallback_chain
    Targets: [azure-east, azure-west, local-fallback]
```

**Enhanced Request:**
```yaml
Actions:
  - Type: route_to_provider
    Target: premium-provider
  - Type: add_metadata
    Key: priority
    Value: high
  - Type: set_timeout
    Value: 60000
```

## Common Rule Patterns

### 1. Model-Specific Routing

**Scenario**: Route expensive models to specific providers

```yaml
Name: "Expensive Models to Premium Provider"
Description: "Route GPT-4 and Claude-3-Opus to premium provider for best performance"
Priority: 10
Conditions:
  - Type: model
    Field: name
    Operator: in
    Value: ["gpt-4", "claude-3-opus"]
Actions:
  - Type: route_to_provider
    Target: premium-provider
```

### 2. Geographic Compliance

**Scenario**: Keep EU data in EU providers

```yaml
Name: "EU Data Compliance"
Description: "Route EU user requests to EU-based providers"
Priority: 5
Conditions:
  - Type: region
    Field: user_region
    Operator: starts_with
    Value: "eu-"
Actions:
  - Type: set_fallback_chain
    Targets: [azure-eu-west, azure-eu-north, openai-eu]
```

### 3. Cost Optimization

**Scenario**: Apply cost controls for budget management

```yaml
Name: "Cost Control for High-Volume Users"
Description: "Apply cost limits for users with high monthly usage"
Priority: 20
Conditions:
  - Type: metadata
    Field: monthly_usage
    Operator: greater_than
    Value: 1000
Actions:
  - Type: set_cost_threshold
    Value: 0.0005
  - Type: route_to_provider
    Target: cost-effective-provider
```

### 4. Priority User Fast Lane

**Scenario**: Give priority users better service

```yaml
Name: "Priority User Fast Lane"
Description: "Route priority users to fastest providers with extended timeouts"
Priority: 1
Conditions:
  - Type: metadata
    Field: user_tier
    Operator: equals
    Value: "premium"
Actions:
  - Type: route_to_provider
    Target: fastest-provider
  - Type: set_timeout
    Value: 90000
  - Type: enable_caching
    Value: 1800
```

### 5. Time-Based Routing

**Scenario**: Route to different providers based on time of day

```yaml
Name: "Off-Hours Routing"
Description: "Use cost-effective providers during off-peak hours"
Priority: 15
Conditions:
  - Type: time
    Field: hour
    Operator: in
    Value: [22, 23, 0, 1, 2, 3, 4, 5]
Actions:
  - Type: route_to_provider
    Target: cost-effective-provider
  - Type: set_timeout
    Value: 120000
```

## Rule Management

### Testing Rules

Before deploying rules, always test them:

1. Navigate to the **Testing & Validation** tab
2. Configure test parameters that match your rule conditions
3. Run the test and verify the expected provider is selected
4. Check the evaluation timeline for debugging information

### Editing Rules

To modify an existing rule:

1. Click the rule name in the rules list
2. Make your changes in the rule builder
3. Click **Save Changes**
4. Test the modified rule to ensure it works as expected

### Disabling Rules

To temporarily disable a rule:

1. Click the rule name to edit it
2. Toggle the **Enabled** switch to off
3. Click **Save Changes**

### Deleting Rules

To permanently remove a rule:

1. Click the **Delete** button next to the rule in the list
2. Confirm the deletion in the dialog

### Rule Priority Management

To reorder rule priorities:

1. Use the priority input field when editing rules
2. Lower numbers = higher priority
3. Save changes and test to ensure proper order

## Best Practices

### Rule Design

1. **Start Simple**: Begin with basic rules and add complexity gradually
2. **Single Purpose**: Each rule should have one clear objective
3. **Descriptive Names**: Use names that clearly indicate the rule's purpose
4. **Document Logic**: Use the description field to explain complex rules

### Condition Guidelines

1. **Be Specific**: More specific conditions lead to predictable behavior
2. **Use Appropriate Operators**: Choose the most efficient operator for your use case
3. **Order Conditions**: Put most selective conditions first for better performance
4. **Avoid Overlaps**: Ensure rules don't conflict with each other

### Action Best Practices

1. **Plan Fallbacks**: Always provide fallback options in actions
2. **Test Thoroughly**: Use the testing interface to validate rule behavior
3. **Monitor Performance**: Check that rules don't add significant latency
4. **Regular Review**: Periodically review rules for continued relevance

### Maintenance

1. **Regular Testing**: Test rules periodically to ensure they still work
2. **Performance Monitoring**: Monitor rule evaluation performance
3. **Documentation Updates**: Keep rule descriptions current
4. **Cleanup**: Remove obsolete rules to maintain system performance

## Troubleshooting

### Rule Not Matching

**Symptoms**: Expected rule doesn't apply to requests

**Solutions**:
1. Check rule priority (lower priority rules might match first)
2. Verify all conditions are correct
3. Use the testing interface to debug condition evaluation
4. Ensure the rule is enabled

### Performance Issues

**Symptoms**: Slow request routing

**Solutions**:
1. Reduce the number of active rules
2. Simplify complex conditions
3. Optimize condition order (most selective first)
4. Consider caching strategies

### Conflicting Rules

**Symptoms**: Unexpected routing behavior

**Solutions**:
1. Review rule priorities and reorder if necessary
2. Make rules more specific to avoid conflicts
3. Use the evaluation timeline to understand rule selection
4. Consider combining related rules

### Provider Not Available

**Symptoms**: Rules route to unavailable providers

**Solutions**:
1. Check provider health status
2. Update rules to use available providers
3. Implement proper fallback chains
4. Test with current provider availability

## Advanced Topics

### Regular Expression Conditions

For complex pattern matching, use regex operators:

```yaml
Condition:
  Type: metadata
  Field: user_email
  Operator: regex
  Value: "^[a-zA-Z0-9._%+-]+@company\\.com$"
```

### Multi-Value Conditions

Use the `in` and `not_in` operators for multiple values:

```yaml
Condition:
  Type: model
  Field: name
  Operator: in
  Value: ["gpt-3.5-turbo", "gpt-4", "gpt-4-turbo"]
```

### Complex Metadata Routing

Route based on complex metadata patterns:

```yaml
Condition:
  Type: metadata
  Field: request_source
  Operator: equals
  Value: "mobile_app"
```

For more advanced patterns and examples, see the [Examples](./examples/) directory.