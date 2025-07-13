# Troubleshooting Guide

## Common Issues and Solutions

This guide helps diagnose and resolve common routing system issues.

## Rule-Related Issues

### Issue: Rule Not Matching Expected Requests

**Symptoms:**
- Expected routing rule doesn't apply to test requests
- Requests routing to wrong providers
- Rule shows as "Not Matched" in testing interface

**Possible Causes:**
1. **Higher Priority Rule Matching First**: Another rule with lower priority number (higher priority) is matching first
2. **Condition Logic Error**: One or more conditions are not being satisfied
3. **Data Type Mismatch**: Condition value doesn't match actual request data type
4. **Rule Disabled**: Rule is accidentally disabled

**Diagnostic Steps:**
1. **Check Rule Priority Order**:
   ```yaml
   # List all rules by priority
   Active Rules:
   - Priority 1: "EU Compliance" (Enabled)
   - Priority 10: "Cost Control" (Enabled) 
   - Priority 15: "Model Routing" (Enabled) ← Your rule
   ```

2. **Use Testing Interface**:
   - Navigate to Testing & Validation tab
   - Configure test parameters to match expected scenario
   - Review condition evaluation table
   - Check which conditions are failing

3. **Verify Condition Values**:
   ```yaml
   # Example condition debug
   Condition: model.name equals "gpt-4"
   Expected: "gpt-4"
   Actual: "gpt-3.5-turbo"  ← Mismatch found
   Result: ✗ Not Matched
   ```

**Solutions:**
1. **Adjust Rule Priority**: Lower the priority number to increase priority
2. **Fix Condition Logic**: Correct condition operators or values
3. **Use Broader Conditions**: Consider using `contains` or `in` operators
4. **Enable Rule**: Check that rule is enabled

### Issue: Rules Conflicting With Each Other

**Symptoms:**
- Inconsistent routing behavior
- Different results for similar requests
- Unexpected provider selection

**Possible Causes:**
1. **Overlapping Conditions**: Multiple rules match the same requests
2. **Priority Conflicts**: Rules with similar priorities causing race conditions
3. **Action Conflicts**: Rules applying contradictory actions

**Diagnostic Steps:**
1. **Identify Overlapping Rules**:
   ```yaml
   # Check for rules with similar conditions
   Rule A: model.name equals "gpt-4" AND region equals "us-east-1"
   Rule B: model.name equals "gpt-4" AND cost > 0.001
   # Both could match GPT-4 requests
   ```

2. **Review Rule Evaluation Timeline**: Use the timeline view to see which rules are evaluated and in what order

3. **Test Edge Cases**: Create test scenarios that might trigger multiple rules

**Solutions:**
1. **Make Rules More Specific**: Add additional conditions to differentiate rules
2. **Adjust Priorities**: Ensure proper priority order for business logic
3. **Combine Related Rules**: Merge similar rules with multiple actions
4. **Use Exclusion Logic**: Add `not_equals` conditions to prevent overlap

### Issue: Rule Performance Problems

**Symptoms:**
- Slow rule evaluation times (>50ms)
- High CPU usage during routing
- Timeouts in rule processing

**Possible Causes:**
1. **Complex Regex Conditions**: Regular expressions taking too long to evaluate
2. **Too Many Rules**: Large number of active rules slowing evaluation
3. **Inefficient Condition Order**: Complex conditions evaluated before simple ones
4. **Resource Constraints**: Insufficient server resources

**Diagnostic Steps:**
1. **Check Evaluation Timeline**:
   ```yaml
   Performance Breakdown:
   - Rule Evaluation: 85ms (90%) ← Problem area
   - Provider Selection: 8ms (8%)
   - Action Execution: 2ms (2%)
   Total: 95ms (Above 50ms threshold)
   ```

2. **Identify Slow Rules**: Look for rules taking >10ms to evaluate
3. **Profile Regex Patterns**: Test complex regex conditions separately

**Solutions:**
1. **Optimize Regex**: Simplify complex regular expressions
2. **Reorder Conditions**: Put simpler conditions first
3. **Reduce Rule Count**: Combine or remove unnecessary rules
4. **Use Caching**: Cache condition results when possible

## Provider-Related Issues

### Issue: Provider Not Being Selected

**Symptoms:**
- Expected provider never receives requests
- Provider shows as available but isn't used
- Fallback providers being used instead of primary

**Possible Causes:**
1. **Low Priority**: Provider has lower priority than others
2. **Provider Disabled**: Provider is marked as disabled
3. **Health Check Failing**: Provider failing health checks
4. **Rule Override**: Routing rules bypassing provider priority

**Diagnostic Steps:**
1. **Check Provider Priority**:
   ```yaml
   Provider Priority List:
   1. OpenAI Primary (Enabled, Healthy)
   2. Azure Secondary (Enabled, Healthy) 
   10. Your Provider (Enabled, Degraded) ← Lower priority
   ```

2. **Verify Provider Status**: Check enabled state and health status
3. **Review Routing Rules**: Look for rules that might route away from provider
4. **Test Provider Directly**: Use testing interface to check provider selection

**Solutions:**
1. **Increase Priority**: Assign a lower priority number
2. **Enable Provider**: Ensure provider is not disabled
3. **Fix Health Issues**: Resolve provider health check problems
4. **Update Rules**: Modify rules that might be bypassing the provider

### Issue: Provider Health Check Problems

**Symptoms:**
- Provider marked as unhealthy despite being functional
- Inconsistent health status reports
- False positive health failures

**Possible Causes:**
1. **Incorrect Health Check URL**: Wrong endpoint configured
2. **Network Issues**: Connectivity problems to health check endpoint
3. **Timeout Too Short**: Health check timeout insufficient
4. **Authentication Issues**: Health endpoint requiring authentication

**Diagnostic Steps:**
1. **Test Health Endpoint Manually**:
   ```bash
   curl -i https://provider.example.com/health
   # Check response code and timing
   ```

2. **Check Network Connectivity**: Verify network path to provider
3. **Review Health Check Logs**: Look for specific error messages
4. **Validate Configuration**: Ensure health check settings are correct

**Solutions:**
1. **Fix Health Check URL**: Correct the endpoint configuration
2. **Increase Timeout**: Allow more time for health check response
3. **Add Authentication**: Configure necessary credentials
4. **Simplify Health Check**: Use a simpler endpoint if available

## Performance Issues

### Issue: Slow Routing Performance

**Symptoms:**
- High latency in request routing
- User complaints about slow response times
- Performance alerts from monitoring

**Possible Causes:**
1. **Complex Rules**: Too many or too complex routing rules
2. **Provider Latency**: Slow response from selected providers
3. **Network Issues**: Connectivity problems to providers
4. **Resource Constraints**: Insufficient routing server resources

**Diagnostic Steps:**
1. **Measure Routing Overhead**:
   ```yaml
   Routing Performance:
   - Rule Evaluation: 45ms
   - Provider Selection: 15ms
   - Network Latency: 200ms ← High latency
   - Provider Response: 500ms ← Slow provider
   ```

2. **Check Provider Performance**: Monitor provider response times
3. **Profile Rule Evaluation**: Identify slow rules or conditions
4. **Monitor System Resources**: Check CPU, memory, network usage

**Solutions:**
1. **Optimize Rules**: Simplify or reduce number of rules
2. **Choose Faster Providers**: Route to providers with better performance
3. **Add Regional Providers**: Reduce network latency with closer providers
4. **Scale Infrastructure**: Add more routing server capacity

### Issue: High Error Rates

**Symptoms:**
- Increased request failures
- Provider timeout errors
- Circuit breaker activations

**Possible Causes:**
1. **Provider Overload**: Providers at capacity limits
2. **Network Problems**: Connectivity issues
3. **Configuration Errors**: Incorrect provider settings
4. **Rate Limiting**: Hitting provider rate limits

**Diagnostic Steps:**
1. **Check Error Patterns**:
   ```yaml
   Error Analysis:
   - Provider A: 15% error rate (HTTP 500)
   - Provider B: 5% error rate (Timeout)
   - Provider C: 0% error rate (Healthy)
   ```

2. **Review Provider Logs**: Check for specific error messages
3. **Monitor Rate Limits**: Check if hitting request limits
4. **Test Provider Connectivity**: Verify network connectivity

**Solutions:**
1. **Distribute Load**: Use load balancing across multiple providers
2. **Implement Backoff**: Add exponential backoff for retries
3. **Fix Configuration**: Correct provider settings
4. **Increase Limits**: Negotiate higher rate limits with providers

## Testing Issues

### Issue: Test Results Don't Match Production

**Symptoms:**
- Testing interface shows different results than production
- Rules work in testing but not in real requests
- Inconsistent behavior between test and production

**Possible Causes:**
1. **Mock Data Differences**: Test data doesn't match production data
2. **Environment Differences**: Different configurations between test and production
3. **Timing Issues**: Race conditions not apparent in testing
4. **Cache Issues**: Cached routing decisions affecting results

**Diagnostic Steps:**
1. **Compare Test vs Production Data**:
   ```yaml
   Test Request:
   - model: "gpt-4"
   - region: "us-east-1"
   - metadata: {user_tier: "premium"}
   
   Production Request:
   - model: "gpt-4"
   - region: "us-east-1"  
   - metadata: {user_tier: "free"} ← Difference found
   ```

2. **Check Environment Configuration**: Verify same rules are active
3. **Review Production Logs**: Look for actual routing decisions
4. **Clear Caches**: Remove any cached routing data

**Solutions:**
1. **Use Real Data**: Test with actual production request patterns
2. **Sync Configurations**: Ensure test and production configs match
3. **Add Logging**: Increase logging detail for debugging
4. **Test Under Load**: Simulate production traffic patterns

## Configuration Issues

### Issue: Changes Not Taking Effect

**Symptoms:**
- Rule modifications don't affect routing
- Provider priority changes ignored
- New rules not being applied

**Possible Causes:**
1. **Configuration Not Saved**: Changes not properly saved
2. **Cache Issues**: Old configuration cached
3. **Deployment Problems**: New configuration not deployed
4. **Syntax Errors**: Invalid configuration preventing loading

**Diagnostic Steps:**
1. **Verify Save Status**: Check if changes were saved successfully
2. **Check Active Configuration**: Confirm which configuration is loaded
3. **Review Logs**: Look for configuration loading errors
4. **Test Configuration Syntax**: Validate configuration format

**Solutions:**
1. **Save Changes**: Ensure all changes are properly saved
2. **Clear Cache**: Force reload of configuration
3. **Redeploy**: Push configuration to production environment
4. **Fix Syntax**: Correct any configuration format errors

### Issue: Authentication/Authorization Problems

**Symptoms:**
- Unable to access routing configuration
- Permission denied errors
- Configuration changes rejected

**Possible Causes:**
1. **Insufficient Permissions**: User lacks admin privileges
2. **Session Expired**: Authentication session timed out
3. **Role Restrictions**: User role doesn't allow configuration changes
4. **Backend Issues**: Authentication service problems

**Diagnostic Steps:**
1. **Check User Permissions**: Verify admin access rights
2. **Test Authentication**: Try logging out and back in
3. **Review Audit Logs**: Look for permission-related errors
4. **Verify Backend Status**: Check authentication service health

**Solutions:**
1. **Request Permissions**: Get admin access from system administrator
2. **Refresh Session**: Log out and log back in
3. **Contact Admin**: Request proper role assignment
4. **Check Backend**: Verify authentication service is working

## Monitoring and Alerting Issues

### Issue: Missing or Incorrect Alerts

**Symptoms:**
- No alerts for known problems
- False positive alerts
- Missing performance notifications

**Possible Causes:**
1. **Alert Configuration**: Incorrect alert thresholds or conditions
2. **Monitoring Gaps**: Metrics not being collected properly
3. **Notification Issues**: Alert delivery problems
4. **Threshold Problems**: Alert thresholds set incorrectly

**Diagnostic Steps:**
1. **Check Alert Configuration**:
   ```yaml
   Alert: High Error Rate
   Condition: error_rate > 5%
   Current Value: 3% ← Below threshold
   Status: Not Triggered
   ```

2. **Verify Metrics Collection**: Ensure metrics are being gathered
3. **Test Alert Delivery**: Check notification channels
4. **Review Thresholds**: Validate alert threshold values

**Solutions:**
1. **Adjust Thresholds**: Set appropriate alert levels
2. **Fix Metric Collection**: Resolve monitoring gaps
3. **Update Notifications**: Fix alert delivery configuration
4. **Test Alerts**: Verify alerts work as expected

## Emergency Procedures

### Complete Routing System Failure

**Immediate Actions:**
1. **Activate Emergency Fallback**: Switch to manual provider selection
2. **Disable Complex Rules**: Simplify routing to basic priority order
3. **Monitor System Health**: Watch for signs of recovery
4. **Communicate Status**: Inform stakeholders of issues

**Recovery Steps:**
1. **Identify Root Cause**: Determine what caused the failure
2. **Fix Core Issue**: Address the underlying problem
3. **Gradually Restore**: Slowly re-enable routing features
4. **Validate Functionality**: Test each component before full restoration

### Provider Mass Failure

**Immediate Actions:**
1. **Activate All Fallback Providers**: Enable all available backup providers
2. **Implement Rate Limiting**: Protect remaining provider capacity
3. **Adjust Timeouts**: Increase timeouts for overloaded providers
4. **Monitor Performance**: Watch for cascade failures

**Recovery Steps:**
1. **Assess Provider Status**: Determine which providers are recovering
2. **Gradual Re-enablement**: Slowly restore providers to service
3. **Load Balancing**: Distribute load carefully across recovered providers
4. **Performance Validation**: Ensure system stability before full restoration

## Getting Help

### Diagnostic Information to Collect

When reporting issues, gather:

1. **System Information**:
   - Routing system version
   - Configuration snapshot
   - Recent changes made

2. **Error Details**:
   - Specific error messages
   - Timestamps of issues
   - Affected request patterns

3. **Performance Data**:
   - Response time metrics
   - Error rate statistics
   - Resource utilization

4. **Test Results**:
   - Screenshots of testing interface
   - Test configurations that reproduce issues
   - Expected vs actual behavior

### Escalation Procedures

1. **Level 1 - Self Service**: Use this troubleshooting guide and testing interface
2. **Level 2 - System Admin**: Contact system administrator for configuration issues
3. **Level 3 - Technical Support**: Escalate to technical support for complex problems
4. **Level 4 - Engineering**: Engage engineering team for system-level issues

### Useful Commands and Tools

**Check System Status**:
```bash
# Check routing service health
curl -i http://localhost:5000/health

# Review recent logs
tail -f /var/log/conduit/routing.log

# Test provider connectivity
curl -i https://provider.example.com/health
```

**Configuration Validation**:
```bash
# Validate routing configuration
conduit-cli config validate

# Export current configuration
conduit-cli config export > routing-config.yaml

# Import configuration
conduit-cli config import routing-config.yaml
```

**Performance Monitoring**:
```bash
# Monitor routing performance
conduit-cli metrics routing-performance

# Check provider response times  
conduit-cli metrics provider-latency

# View error rates
conduit-cli metrics error-rates
```