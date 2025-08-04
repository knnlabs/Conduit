# Video Generation Troubleshooting Guide

## Common Issues and Solutions

### Progress Not Updating

#### Symptoms
- Progress bar stays at 0%
- No status messages appear
- Video generation completes without progress updates

#### Diagnostic Steps

1. **Check SignalR Connection Status**
   ```typescript
   // In browser console
   const client = window.__conduitClient;
   console.log('SignalR connected:', client.signalR?.isConnected());
   ```

2. **Verify WebSocket Support**
   ```javascript
   // Check if WebSockets are available
   console.log('WebSocket support:', 'WebSocket' in window);
   ```

3. **Check Browser Network Tab**
   - Look for WebSocket connection to `/hubs/video-generation`
   - Status should be 101 (Switching Protocols)
   - Check for any connection errors

4. **Verify Task ID**
   ```typescript
   // Ensure task ID matches between generation and progress
   console.log('Task ID:', taskId);
   ```

#### Solutions

1. **Enable Fallback to Polling**
   ```typescript
   const { generateVideo } = useVideoGeneration({
     useProgressTracking: true,
     fallbackToPolling: true  // Enable automatic fallback
   });
   ```

2. **Check Firewall/Proxy Settings**
   - Ensure WebSocket connections are allowed
   - Port 443 (HTTPS/WSS) should be open
   - Proxy must support WebSocket upgrade

3. **Force Polling Mode**
   ```typescript
   // Disable SignalR temporarily
   const client = new ConduitCoreClient({
     apiKey: 'your-key',
     signalR: { enabled: false }
   });
   ```

### Duplicate Progress Events

#### Symptoms
- Same progress percentage shown multiple times
- Console shows duplicate events
- Progress appears to jump backwards

#### Cause
This is normal behavior when both SignalR and polling are active. The SDK automatically deduplicates events within a 500ms window.

#### Solutions

1. **Trust the SDK Deduplication**
   - No action needed - this is handled automatically
   - Progress will never go backwards

2. **Debug Deduplication**
   ```typescript
   // Enable debug logging to see deduplication
   const client = new ConduitCoreClient({
     apiKey: 'your-key',
     debug: true
   });
   ```

### Progress Jumping or Irregular Updates

#### Symptoms
- Progress jumps from 20% to 80%
- Updates arrive in bursts
- Long periods without updates

#### Causes
1. **Network Latency**: Updates queued during disconnection
2. **Provider Behavior**: Some providers send infrequent updates
3. **SignalR Reconnection**: Bulk updates after reconnect

#### Solutions

1. **Smooth Progress Display**
   ```typescript
   // Use animation for smooth transitions
   <Progress
     value={progress}
     animate
     transitionDuration={500}
   />
   ```

2. **Monitor Connection State**
   ```typescript
   const { signalRConnected } = useVideoGeneration();
   if (!signalRConnected) {
     console.warn('SignalR disconnected - using polling');
   }
   ```

### Generation Fails Immediately

#### Symptoms
- Task fails with no progress
- Error: "Insufficient GPU resources"
- Error: "Model not available"

#### Diagnostic Steps

1. **Check Model Availability**
   ```typescript
   const capabilities = client.videos.getModelCapabilities('minimax-video-01');
   console.log('Model capabilities:', capabilities);
   ```

2. **Verify Request Parameters**
   ```typescript
   // Ensure parameters are within limits
   {
     duration: 6,        // Max for minimax-video-01
     size: '1280x720',   // Supported resolution
     fps: 30            // Supported frame rate
   }
   ```

3. **Check Provider Status**
   - Admin panel → Providers → Check health status
   - Verify API keys are valid
   - Check rate limits

### SignalR Connection Errors

#### Error: "Failed to negotiate with the server"

**Causes:**
- Server doesn't support SignalR
- Authentication failure
- CORS issues

**Solutions:**
1. Verify server URL includes SignalR support
2. Check virtual key is valid
3. Ensure CORS allows your domain

#### Error: "Connection closed with error"

**Causes:**
- Network interruption
- Server restart
- Token expiration

**Solutions:**
1. SDK automatically reconnects - wait a moment
2. Check network connectivity
3. Refresh virtual key if expired

### Performance Issues

#### Slow Progress Updates

1. **Check Network Latency**
   ```bash
   # Ping API server
   ping api.conduit.ai
   ```

2. **Monitor Browser Performance**
   - Open DevTools → Performance tab
   - Look for long tasks during updates

3. **Optimize React Renders**
   ```typescript
   // Memoize progress component
   const ProgressBar = React.memo(({ progress }) => (
     <Progress value={progress} />
   ));
   ```

#### High Memory Usage

1. **Clean Up Completed Tasks**
   ```typescript
   // Remove old tasks from state
   const cleanupOldTasks = () => {
     setTasks(tasks => tasks.filter(
       task => task.status !== 'completed' || 
       Date.now() - task.completedAt < 3600000
     ));
   };
   ```

2. **Limit Queue Size**
   ```typescript
   const MAX_QUEUE_SIZE = 50;
   if (taskQueue.length >= MAX_QUEUE_SIZE) {
     taskQueue.shift(); // Remove oldest
   }
   ```

## Debug Mode

Enable comprehensive debugging:

```typescript
// Enable all debug features
const client = new ConduitCoreClient({
  apiKey: 'your-key',
  debug: true,
  signalR: {
    logLevel: 'Debug'
  }
});

// Track all events
window.__videoProgressEvents = [];
const { generateVideo } = useVideoGeneration({
  onProgress: (progress) => {
    window.__videoProgressEvents.push({
      time: Date.now(),
      ...progress
    });
  }
});
```

## Common Error Messages

### "Task not found"
- Task ID is invalid or expired
- Task belongs to different virtual key
- Task was cancelled or cleaned up

### "WebSocket connection failed"
- Firewall blocking WebSocket
- Proxy doesn't support WebSocket
- Server doesn't have SignalR enabled

### "Progress tracking timed out"
- Task took longer than timeout (default 30 min)
- Network disconnection prevented updates
- Server stopped sending progress

## Best Practices

1. **Always Handle Errors**
   ```typescript
   const { generateVideo, error } = useVideoGeneration();
   
   if (error) {
     // Show user-friendly error message
     showNotification({ 
       title: 'Generation Failed',
       message: getErrorMessage(error),
       color: 'red' 
     });
   }
   ```

2. **Provide Fallback UI**
   ```typescript
   {!signalRConnected && (
     <Alert>
       Using polling for progress updates. 
       Updates may be less frequent.
     </Alert>
   )}
   ```

3. **Test Different Scenarios**
   - Test with SignalR disabled
   - Test with slow network
   - Test with connection interruptions
   - Test with multiple concurrent tasks

## Getting Help

If issues persist:

1. **Collect Debug Information**
   - Browser console logs
   - Network tab HAR file
   - Task IDs and timestamps
   - Error messages and stack traces

2. **Check Status Page**
   - Visit status.conduit.ai for service health
   - Check for ongoing incidents

3. **Contact Support**
   - Include debug information
   - Describe steps to reproduce
   - Note any recent changes