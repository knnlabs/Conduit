# Provider Error Tracking

## Overview

Conduit automatically tracks provider API errors and can disable API keys that consistently fail. This system helps maintain service reliability by detecting and isolating problematic credentials before they impact your users.

## How It Works

When API requests fail, Conduit:
1. Classifies the error type based on HTTP status code
2. Stores the error in Redis for tracking
3. Evaluates whether the key should be disabled
4. Publishes events for real-time dashboard updates

## Error Types

### Fatal Errors (Auto-Disable Keys)

These errors indicate fundamental issues that require intervention:

| Error Type | HTTP Status | Description | Disable Policy |
|------------|-------------|-------------|----------------|
| Invalid API Key | 401 | API key is invalid, revoked, or malformed | **Immediate** - disabled on first occurrence |
| Insufficient Balance | 402 | Account has no credits or quota exhausted | 2 occurrences within 5 minutes |
| Access Forbidden | 403 | Account lacks permission (not balance-related) | 3 occurrences within 10 minutes |

### Warning Errors (Tracked, No Auto-Disable)

These errors are typically transient and don't disable keys:

| Error Type | HTTP Status | Description | Alert Threshold |
|------------|-------------|-------------|-----------------|
| Rate Limit Exceeded | 429 | Too many requests to provider | 10 in 5 minutes |
| Model Not Found | 404 | Requested model doesn't exist | Not tracked |
| Service Unavailable | 503 | Provider experiencing issues | 5 in 10 minutes |

### Transient Errors (Minimal Tracking)

Network errors and timeouts are tracked minimally as they're usually temporary:
- Network connectivity issues
- Request timeouts
- Unknown/unclassified errors

## Managing Provider Errors

### Viewing Error Dashboard

Access provider errors through the Admin Panel:

1. Navigate to **Providers** in the sidebar
2. Look for error indicators on provider cards
3. Click a provider to see detailed error information

**Dashboard shows:**
- Total errors in the last 24 hours
- Fatal vs. warning error breakdown
- Number of disabled keys
- Errors grouped by provider

### Viewing Recent Errors

The recent errors view shows:
- Which key credential caused the error
- Error type and HTTP status code
- Error message from the provider
- Timestamp of occurrence
- Whether it was a fatal or warning error

### Managing Disabled Keys

When a key is disabled:

1. **Identify the Issue**
   - Check the error type (Invalid Key, Insufficient Balance, etc.)
   - Review the error message from the provider
   - Verify the key in your provider's dashboard

2. **Resolve the Problem**
   - For **Invalid API Key**: Generate a new key or check for typos
   - For **Insufficient Balance**: Add credits to your provider account
   - For **Access Forbidden**: Check API permissions and access level

3. **Re-enable the Key**
   - Navigate to the disabled key
   - Click **Clear Errors & Re-enable**
   - Confirm the action

### Manually Disabling Keys

You can manually disable a key for maintenance:

1. Select the provider key
2. Click **Disable Key**
3. Provide a reason (for audit purposes)
4. The key will stop receiving traffic immediately

## Error Retention

- **Fatal errors**: Persisted until manually cleared
- **Warnings**: Retained for 30 days (last 100 per key)
- **Recent error feed**: Last 1,000 errors across all providers

## Provider-Level Disabling

When all keys for a provider are disabled:
- The entire provider is marked as unavailable
- Requests will fail over to other providers (if configured)
- Provider shows "Disabled" status in dashboard

## Best Practices

### Monitoring

1. **Check the dashboard regularly** - Review error trends daily
2. **Set up alerts** - Use webhook integrations for error notifications
3. **Watch for patterns** - Sudden spikes may indicate provider issues

### Multiple Keys

1. **Use multiple API keys** - Distribute load and provide redundancy
2. **Different accounts** - Separate keys from different billing accounts
3. **Primary/Secondary** - Configure primary key with backup alternatives

### Error Prevention

1. **Monitor provider balance** - Keep accounts funded
2. **Rotate keys periodically** - Update credentials before they expire
3. **Test new keys** - Verify keys work before deploying to production

## Troubleshooting

### Key Won't Re-enable

**Symptoms:** Clicking "Clear Errors & Re-enable" doesn't work

**Solutions:**
- Ensure you've actually fixed the underlying issue
- Check the confirmation checkbox is selected
- Verify you have admin permissions
- Check browser console for errors

### Errors Not Appearing

**Symptoms:** Errors occur but don't show in dashboard

**Solutions:**
- Verify Redis is connected and healthy
- Check that the Admin API is running
- Ensure error tracking service is enabled
- Review Admin API logs for errors

### False Positive Disables

**Symptoms:** Keys disabled but actually valid

**Solutions:**
- Check if provider had temporary outage
- Review error timestamps for clustering
- Consider adjusting thresholds if needed
- Report patterns to Conduit team

### High Warning Count

**Symptoms:** Many rate limit warnings without issues

**Solutions:**
- This is informational, not actionable
- Consider distributing load across more keys
- Implement request rate limiting on your side
- Contact provider for higher rate limits

## API Reference

### View Error Statistics
```bash
GET /api/provider-errors/stats?hours=24
```

### View Recent Errors
```bash
GET /api/provider-errors/recent?limit=100
```

### View Specific Key Errors
```bash
GET /api/provider-errors/keys/{keyId}
```

### Clear Errors and Re-enable Key
```bash
POST /api/provider-errors/keys/{keyId}/clear
Content-Type: application/json

{
  "reenableKey": true,
  "confirmReenable": true,
  "reason": "Credits added to account"
}
```

### Manually Disable Key
```bash
POST /api/provider-errors/keys/{keyId}/disable
Content-Type: application/json

{
  "reason": "Scheduled maintenance"
}
```

## Related Documentation

- [Provider Architecture](../architecture/provider-system/provider-architecture.md)
- [Error Tracking Developer Guide](../development/error-tracking-architecture.md)
- [Error Tracking Runbook](../operations/error-tracking-runbook.md)
