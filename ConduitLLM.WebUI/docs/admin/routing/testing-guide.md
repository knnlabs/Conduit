# Testing Guide

## Testing and Validating Routing Rules

This guide explains how to use the testing interface to validate routing rules and debug routing behavior.

## Accessing the Testing Interface

1. Navigate to **Routing Settings** in the admin dashboard
2. Click on the **Testing & Validation** tab
3. Use the comprehensive testing interface to validate your routing configuration

## Testing Interface Overview

The testing interface provides a complete environment for validating routing rules with:

- **Test Form**: Configure test parameters and request details
- **Real-time Evaluation**: Execute tests and see immediate results
- **Matched Rules Display**: See which rules matched and why
- **Provider Selection Analysis**: Understand provider selection reasoning
- **Evaluation Timeline**: Step-by-step debugging information
- **Test History**: Save and replay previous tests

## Test Form Configuration

### Basic Test Parameters

**Model Selection:**
- Choose from available models in the dropdown
- Affects model-based routing rules
- Examples: "gpt-4", "claude-3-opus", "gpt-3.5-turbo"

**Region:**
- Select user or provider region
- Tests geographic routing rules
- Examples: "us-east-1", "eu-west-1", "ap-southeast-1"

**Cost Threshold:**
- Set maximum acceptable cost per token
- Tests cost-based routing rules
- Format: Decimal value (e.g., 0.001 for $0.001 per token)

### Advanced Parameters

**Custom Metadata Fields:**
- Add key-value pairs for metadata-based routing
- Examples:
  - `user_tier: "premium"`
  - `priority: "high"`
  - `organization: "enterprise"`

**HTTP Headers:**
- Simulate request headers for header-based routing
- Examples:
  - `x-api-version: "v2"`
  - `authorization: "Bearer token"`
  - `x-request-id: "test-123"`

**Time Simulation:**
- Test time-based routing rules
- Options: Current time or custom date/time
- Useful for testing off-hours routing

## Running Tests

### Execute a Test

1. Configure your test parameters in the form
2. Click **Run Test** to execute the routing evaluation
3. Review the results in the multiple result panels

### Test Results Overview

The results are displayed in several sections:

**Quick Summary:**
- Overall test success/failure status
- Number of matched rules
- Number of applied rules
- Selected provider name
- Total evaluation time

## Understanding Test Results

### Matched Rules Display

Shows detailed information about rule evaluation:

**Rule Status Indicators:**
- **Applied** (Green): Rule matched and its actions were executed
- **Matched** (Blue): Rule matched but wasn't applied (lower priority rule already applied)
- **Not Matched** (Gray): Rule conditions were not satisfied

**Condition Evaluation Table:**
- **Result**: Visual indicator (✓/✗) showing if condition matched
- **Field**: The condition type and field being evaluated
- **Operator**: The comparison operator used
- **Expected**: The value the condition expected
- **Actual**: The actual value from the test request
- **Reason**: Explanation of why the condition matched or failed

**Rule Actions:**
- Lists all actions defined for the rule
- Indicates whether actions were applied or not
- Shows action parameters and targets

### Provider Selection Analysis

Provides detailed reasoning for provider selection:

**Selected Provider Information:**
- Provider name and type
- Priority level
- Enabled/disabled status
- Provider endpoint (if applicable)

**Routing Decision Details:**
- **Strategy**: How the provider was selected (priority, rule-based, fallback)
- **Reason**: Detailed explanation of the selection
- **Processing Time**: How long the selection took
- **Fallback Used**: Whether fallback logic was employed

**Fallback Chain:**
- Shows the complete fallback chain
- Lists providers in order of preference
- Indicates provider types and priorities
- Visual flow showing the fallback progression

### Rule Evaluation Timeline

Step-by-step debugging information:

**Performance Breakdown:**
- Visual representation of time spent in each evaluation category
- Percentage breakdown of total evaluation time
- Color-coded performance indicators (green = fast, yellow = moderate, red = slow)

**Detailed Execution Steps:**
- **Step Number**: Order of execution
- **Action**: What was evaluated or executed
- **Duration**: Time taken for each step
- **Success/Failure**: Whether the step completed successfully
- **Details**: Additional context and information
- **Timestamp**: When the step occurred

**Execution Summary Table:**
- Categorized view of all steps
- Success rate per category
- Total and average duration per category
- Performance analysis

## Test Scenarios

### Basic Rule Testing

**Scenario**: Test a simple model-based routing rule

```yaml
Test Configuration:
  Model: "gpt-4"
  Region: "us-east-1"
  Cost Threshold: 0.002

Expected Result:
  Rule: "Route GPT-4 to Premium Provider"
  Provider: "openai-premium"
  Reason: "Model-based routing rule applied"
```

### Geographic Routing Testing

**Scenario**: Verify EU data compliance routing

```yaml
Test Configuration:
  Model: "gpt-3.5-turbo"
  Region: "eu-west-1"
  Metadata:
    user_region: "eu-west-1"

Expected Result:
  Rule: "EU Data Compliance"
  Provider: "azure-eu-west"
  Reason: "Geographic compliance rule applied"
```

### Cost Threshold Testing

**Scenario**: Test cost-based routing limits

```yaml
Test Configuration:
  Model: "gpt-4"
  Cost Threshold: 0.0005
  Metadata:
    monthly_usage: 2000

Expected Result:
  Rule: "Cost Control for High Usage"
  Provider: "cost-effective-provider"
  Reason: "Cost threshold rule applied"
```

### Priority User Testing

**Scenario**: Verify premium user routing

```yaml
Test Configuration:
  Model: "claude-3-opus"
  Metadata:
    user_tier: "premium"
    priority: "high"

Expected Result:
  Rule: "Priority User Fast Lane"
  Provider: "fastest-provider"
  Actions: [route_to_provider, set_timeout, enable_caching]
```

### Fallback Chain Testing

**Scenario**: Test provider fallback behavior

```yaml
Test Configuration:
  Model: "gpt-3.5-turbo"
  Simulate: primary_provider_down

Expected Result:
  Provider: "secondary-provider"
  Fallback Used: true
  Reason: "Primary provider unavailable, used fallback"
```

## Test History Management

### Saving Tests

The testing interface automatically saves test configurations and results:

- **Automatic Saving**: Tests are saved to local storage automatically
- **Test Naming**: Tests are named based on configuration or can be custom named
- **Quick Reload**: Previously saved tests can be reloaded with one click

### Managing Test History

**View History:**
- Access saved tests from the Test History panel
- See test configuration summary and results
- Sort by date, name, or result status

**Reload Tests:**
- Click any saved test to reload its configuration
- Modify parameters and re-run if needed
- Compare results across different test runs

**Export Tests:**
- Export test history to JSON format
- Share test configurations with team members
- Import tests from other environments

### Organizing Tests

**Test Categories:**
- Basic functionality tests
- Regression tests for rule changes
- Performance validation tests
- Edge case and error condition tests

**Best Practices:**
- Name tests descriptively
- Save representative test cases for each rule
- Create test suites for comprehensive validation
- Regular cleanup of obsolete tests

## Debugging Common Issues

### Rule Not Matching

**Symptoms**: Expected rule doesn't match in tests

**Debugging Steps:**
1. Check the condition evaluation table
2. Verify each condition's expected vs actual values
3. Ensure all conditions are satisfied (AND logic)
4. Check rule priority order

**Example Debug Output:**
```
Condition: model.name equals "gpt-4"
Expected: "gpt-4"
Actual: "gpt-3.5-turbo"
Result: ✗ Not Matched
Reason: Model name does not match expected value
```

### Wrong Provider Selected

**Symptoms**: Unexpected provider chosen

**Debugging Steps:**
1. Review the provider selection analysis
2. Check if a higher priority rule matched first
3. Verify provider availability and health status
4. Look at the complete fallback chain

**Example Debug Output:**
```
Strategy: rule-based
Reason: Higher priority rule "Cost Control" matched first
Applied Rule: "Cost Control for High Usage"
Selected Provider: "cost-effective-provider"
```

### Performance Issues

**Symptoms**: Slow rule evaluation times

**Debugging Steps:**
1. Review the evaluation timeline
2. Identify slow steps in the process
3. Check for complex conditions or actions
4. Look for performance bottlenecks

**Example Debug Output:**
```
Performance Breakdown:
- Rule Evaluation: 45ms (60%)
- Provider Selection: 20ms (27%)
- Action Execution: 10ms (13%)
Total: 75ms (Warning: Above 50ms threshold)
```

### Conflicting Rules

**Symptoms**: Inconsistent or unexpected routing behavior

**Debugging Steps:**
1. Test with different priorities
2. Review rule evaluation order
3. Check for overlapping conditions
4. Verify action conflicts

**Example Debug Output:**
```
Conflict Detected:
Rule 1 (Priority 10): Routes to "provider-a"
Rule 2 (Priority 20): Routes to "provider-b"
Both rules match the same request
Resolution: Rule 1 applied due to higher priority
```

## Performance Testing

### Evaluation Time Testing

**Target Performance**: Rule evaluation should complete under 10ms for typical configurations

**Test Approach:**
1. Create a representative set of rules (10-50 rules)
2. Run multiple test scenarios
3. Measure evaluation times
4. Identify performance bottlenecks

**Performance Metrics:**
- Average evaluation time
- 95th percentile evaluation time
- Maximum evaluation time
- Evaluation time by rule count

### Load Testing Simulation

**Test Concurrent Requests:**
1. Configure multiple test scenarios
2. Simulate concurrent request evaluation
3. Measure performance under load
4. Identify scalability limits

**Metrics to Monitor:**
- Requests per second capacity
- Memory usage during evaluation
- CPU utilization
- Response time degradation

### Stress Testing

**Test Edge Cases:**
1. Very large number of rules (100+)
2. Complex condition combinations
3. Deep fallback chains
4. High-frequency rule evaluation

**Failure Scenarios:**
- Provider unavailability
- Network timeouts
- Invalid configurations
- Resource exhaustion

## Automated Testing

### Test Automation Scripts

Create automated test suites for continuous validation:

```javascript
// Example automated test
const testSuite = [
  {
    name: "GPT-4 Premium Routing",
    config: { model: "gpt-4", region: "us-east-1" },
    expected: { provider: "openai-premium", rule: "Premium Model Routing" }
  },
  {
    name: "EU Compliance",
    config: { model: "gpt-3.5-turbo", region: "eu-west-1" },
    expected: { provider: "azure-eu-west", rule: "EU Data Compliance" }
  }
];
```

### Continuous Integration

Integrate routing tests into your CI/CD pipeline:

1. Run automated tests on rule changes
2. Validate performance regressions
3. Test against different configurations
4. Generate test reports

### Regression Testing

**When to Run Regression Tests:**
- After adding new rules
- When modifying existing rules
- After provider configuration changes
- Before production deployments

**Test Coverage:**
- All active routing rules
- Provider fallback scenarios
- Performance benchmarks
- Edge cases and error conditions

## Best Practices

### Test Design

1. **Comprehensive Coverage**: Test all routing rules and scenarios
2. **Realistic Data**: Use representative test data
3. **Edge Cases**: Include boundary conditions and error scenarios
4. **Performance Focus**: Always check evaluation performance

### Test Maintenance

1. **Regular Updates**: Keep tests current with rule changes
2. **Cleanup**: Remove obsolete tests regularly
3. **Documentation**: Document test purposes and expected outcomes
4. **Version Control**: Track test configurations in version control

### Debugging Methodology

1. **Systematic Approach**: Follow a consistent debugging process
2. **Isolate Variables**: Test one change at a time
3. **Use Timeline**: Leverage the evaluation timeline for insights
4. **Document Issues**: Record problems and solutions for future reference

### Performance Monitoring

1. **Baseline Metrics**: Establish performance baselines
2. **Regular Monitoring**: Check performance regularly
3. **Threshold Alerts**: Set up alerts for performance degradation
4. **Optimization**: Continuously optimize rule performance

For more testing examples and advanced scenarios, see the [Examples](./examples/) directory.