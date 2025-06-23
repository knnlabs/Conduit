# Timeout Configuration Guide

This guide explains how to configure operation-specific timeouts in Conduit to prevent conflicting timeout policies across different layers.

## Overview

Conduit now uses an operation-aware timeout system that allows you to configure different timeouts for different types of operations. This solves the issue of multiple conflicting timeout layers that previously made long-running operations impossible.

## Configuration Methods

### 1. Configuration File (Recommended)

Create or update `appsettings.json` or `appsettings.Timeouts.json`:

```json
{
  "ConduitLLM": {
    "Timeouts": {
      "chat": {
        "Seconds": 30,
        "Enabled": true,
        "Description": "Timeout for chat completion operations"
      },
      "image-generation": {
        "Seconds": 120,
        "Enabled": true,
        "Description": "Timeout for image generation operations"
      },
      "video-generation": {
        "Seconds": 600,
        "Enabled": true,
        "Description": "Timeout for synchronous video generation"
      },
      "video-polling": {
        "Seconds": 900,
        "Enabled": true,
        "Description": "Timeout for video generation polling"
      }
    }
  }
}
```

### 2. Environment Variables

You can also configure timeouts using environment variables:

```bash
# Set chat timeout to 45 seconds
export CONDUITLLM__TIMEOUTS__CHAT__SECONDS=45

# Set video generation timeout to 15 minutes
export CONDUITLLM__TIMEOUTS__VIDEO_GENERATION__SECONDS=900

# Disable timeout for streaming operations
export CONDUITLLM__TIMEOUTS__STREAMING__ENABLED=false

# Configure video polling timeout (used by MiniMax)
export CONDUITLLM__TIMEOUTS__VIDEO_POLLING__SECONDS=1200
```

### 3. Docker Compose

In `docker-compose.yml`:

```yaml
services:
  api:
    environment:
      - CONDUITLLM__TIMEOUTS__CHAT__SECONDS=30
      - CONDUITLLM__TIMEOUTS__IMAGE_GENERATION__SECONDS=120
      - CONDUITLLM__TIMEOUTS__VIDEO_GENERATION__SECONDS=600
      - CONDUITLLM__TIMEOUTS__VIDEO_POLLING__SECONDS=900
```

## Operation Types

The following operation types are supported:

| Operation Type | Default Timeout | Description |
|----------------|-----------------|-------------|
| `chat` | 30 seconds | Chat completion requests |
| `completion` | 60 seconds | Text completion requests |
| `image-generation` | 120 seconds | Image generation requests |
| `video-generation` | 10 minutes | Video generation sync requests |
| `video-polling` | 15 minutes | Video generation polling |
| `polling` | 5 minutes | General polling operations |
| `health-check` | 5 seconds | Health check endpoints |
| `model-discovery` | 10 seconds | Model discovery operations |
| `streaming` | No timeout | Streaming operations (disabled) |
| `websocket` | No timeout | WebSocket connections (disabled) |

## Timeout Diagnostics

When timeout diagnostics are enabled, the following information is logged:

1. **Request Start**: Operation type and configured timeout
2. **Request Completion**: Duration and status code
3. **Timeout Warnings**: When requests exceed configured timeouts
4. **Response Headers**: Timeout information in `X-Operation-Type`, `X-Timeout-Seconds`, and `X-Timeout-Applied` headers

Enable diagnostics:

```json
{
  "ConduitLLM": {
    "EnableDiagnostics": true
  }
}
```

## Migration from Hard-Coded Timeouts

The following hard-coded timeouts have been replaced:

1. **HTTP Client Factory** (was 100 seconds) → Now uses operation-specific timeouts
2. **WebUI HTTP Client** (was 60 seconds) → Now uses operation-specific timeouts
3. **Sync Video Endpoint** (was 120 seconds) → Now uses `video-generation` timeout
4. **MiniMax Polling** (was ~10 minutes) → Now uses `video-polling` timeout

## Best Practices

1. **Set Realistic Timeouts**: Configure timeouts based on actual operation durations
2. **Use Async for Long Operations**: For operations > 2 minutes, use async endpoints
3. **Monitor Timeout Logs**: Watch for timeout warnings to adjust configurations
4. **Test Changes**: Test timeout changes with actual workloads before production

## Troubleshooting

### Requests Still Timing Out

1. Check all timeout layers are using the operation-aware provider
2. Verify configuration is loaded correctly (check startup logs)
3. Use timeout diagnostics to identify which layer is timing out

### Configuration Not Applied

1. Ensure environment variable names are correct (use double underscores)
2. Check configuration file is in the correct location
3. Verify JSON syntax if using configuration files

### Finding Timeout Source

Enable diagnostics and check:
- Response headers for applied timeout
- Logs for timeout warnings with operation type
- Which layer logged the timeout error

## Example: Configuring for Video Generation

For video generation that takes 5-10 minutes:

```bash
# Set sync endpoint timeout to 12 minutes
export CONDUITLLM__TIMEOUTS__VIDEO_GENERATION__SECONDS=720

# Set polling timeout to 15 minutes
export CONDUITLLM__TIMEOUTS__VIDEO_POLLING__SECONDS=900

# Ensure HTTP client timeout is long enough
export CONDUITLLM__HTTPTIMEOUT__TIMEOUTSECONDS=900
```

This ensures all layers have sufficient timeout for long-running video operations.