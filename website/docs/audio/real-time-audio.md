---
sidebar_position: 4
title: Real-Time Audio
description: WebSocket proxy for real-time audio connections to provider APIs
---

# Real-Time Audio

Conduit provides a WebSocket proxy for real-time audio connections to supported providers. This allows applications to establish direct WebSocket connections through Conduit while maintaining virtual key authentication and provider routing.

## Overview

Real-time audio in Conduit works as a **WebSocket proxy**:
- Authenticates requests using virtual keys
- Routes connections to appropriate providers
- Proxies WebSocket messages bidirectionally
- Maintains connection state and error handling

## Supported Providers

Currently, real-time audio proxy supports providers that offer WebSocket-based real-time audio APIs.

## Connection

### WebSocket Endpoint

```
wss://api.conduit.yourdomain.com/v1/realtime/connect
```

### Connection Parameters

Connect with model and provider specified as query parameters:

```javascript
const websocket = new WebSocket(
  'wss://api.conduit.yourdomain.com/v1/realtime/connect?model=gpt-4o-realtime-preview&provider=openai',
  {
    headers: {
      'Authorization': 'Bearer condt_your_virtual_key'
    }
  }
);
```

### Authentication

Real-time audio connections require virtual key authentication via the `Authorization` header:

```javascript
const ws = new WebSocket(websocketUrl, {
  headers: {
    'Authorization': 'Bearer condt_your_virtual_key'
  }
});
```

## Basic Implementation

### Simple WebSocket Client

```javascript
class ConduitRealTimeAudio {
  constructor(apiKey, model = 'gpt-4o-realtime-preview', provider = 'openai') {
    this.apiKey = apiKey;
    this.model = model;
    this.provider = provider;
    this.ws = null;
    this.isConnected = false;
  }

  connect() {
    const url = `wss://api.conduit.yourdomain.com/v1/realtime/connect?model=${this.model}&provider=${this.provider}`;
    
    this.ws = new WebSocket(url, {
      headers: {
        'Authorization': `Bearer ${this.apiKey}`
      }
    });

    this.ws.on('open', () => {
      console.log('Connected to Conduit real-time audio proxy');
      this.isConnected = true;
    });

    this.ws.on('message', (data) => {
      this.handleMessage(data);
    });

    this.ws.on('close', (code, reason) => {
      console.log(`Connection closed: ${code} ${reason}`);
      this.isConnected = false;
    });

    this.ws.on('error', (error) => {
      console.error('WebSocket error:', error);
    });
  }

  handleMessage(data) {
    try {
      // Handle both text and binary messages
      if (data instanceof Buffer) {
        console.log('Received binary data:', data.length, 'bytes');
        this.onAudioData(data);
      } else {
        const message = JSON.parse(data);
        console.log('Received text message:', message);
        this.onTextMessage(message);
      }
    } catch (error) {
      console.error('Error handling message:', error);
    }
  }

  send(data) {
    if (this.ws && this.isConnected) {
      this.ws.send(data);
    } else {
      console.error('WebSocket not connected');
    }
  }

  sendText(message) {
    this.send(JSON.stringify(message));
  }

  sendAudio(audioBuffer) {
    this.send(audioBuffer);
  }

  onTextMessage(message) {
    // Override in subclass to handle provider-specific text messages
    console.log('Text message:', message);
  }

  onAudioData(audioBuffer) {
    // Override in subclass to handle audio data
    console.log('Audio data received:', audioBuffer.length, 'bytes');
  }

  disconnect() {
    if (this.ws) {
      this.ws.close();
      this.ws = null;
      this.isConnected = false;
    }
  }
}
```

### Usage Example

```javascript
// Create real-time audio client
const rtAudio = new ConduitRealTimeAudio('condt_your_virtual_key');

// Override message handlers
rtAudio.onTextMessage = (message) => {
  console.log('Provider message:', message);
  // Handle provider-specific messages
};

rtAudio.onAudioData = (audioBuffer) => {
  console.log('Received audio:', audioBuffer.length, 'bytes');
  // Process audio data
};

// Connect
rtAudio.connect();

// Send messages (format depends on provider)
rtAudio.sendText({
  type: 'configure',
  config: {
    // Provider-specific configuration
  }
});
```

## Provider-Specific Usage

### OpenAI Realtime API

When using OpenAI's Realtime API through the proxy:

```javascript
const openaiRealtime = new ConduitRealTimeAudio(
  'condt_your_virtual_key', 
  'gpt-4o-realtime-preview', 
  'openai'
);

openaiRealtime.onTextMessage = (message) => {
  // Handle OpenAI Realtime API events
  switch (message.type) {
    case 'session.created':
      console.log('Session created:', message.session);
      break;
    case 'conversation.item.input_audio_transcription.completed':
      console.log('Transcription:', message.transcript);
      break;
    case 'response.audio.delta':
      console.log('Audio response chunk received');
      break;
    // Handle other OpenAI events...
  }
};

openaiRealtime.connect();

// Send OpenAI-format messages
openaiRealtime.sendText({
  type: 'session.update',
  session: {
    model: 'gpt-4o-realtime-preview',
    modalities: ['text', 'audio'],
    voice: 'alloy'
  }
});
```

## Error Handling

### Connection Errors

```javascript
class ResilientRealTimeAudio extends ConduitRealTimeAudio {
  constructor(apiKey, model, provider, options = {}) {
    super(apiKey, model, provider);
    this.reconnectAttempts = 0;
    this.maxReconnectAttempts = options.maxReconnectAttempts || 5;
    this.reconnectDelay = options.reconnectDelay || 1000;
  }

  connect() {
    super.connect();

    this.ws.on('close', (code, reason) => {
      console.log(`Connection closed: ${code} ${reason}`);
      this.isConnected = false;
      
      if (this.shouldReconnect(code)) {
        this.attemptReconnect();
      }
    });

    this.ws.on('error', (error) => {
      console.error('WebSocket error:', error);
      if (!this.isConnected) {
        this.attemptReconnect();
      }
    });
  }

  shouldReconnect(closeCode) {
    // Don't reconnect for auth errors or intentional closes
    return closeCode !== 1000 && closeCode !== 1002 && closeCode !== 4001;
  }

  attemptReconnect() {
    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      console.error('Max reconnection attempts reached');
      return;
    }

    this.reconnectAttempts++;
    const delay = this.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1);
    
    console.log(`Attempting reconnection ${this.reconnectAttempts}/${this.maxReconnectAttempts} in ${delay}ms`);
    
    setTimeout(() => {
      this.connect();
    }, delay);
  }

  onConnectionSuccess() {
    this.reconnectAttempts = 0;
    console.log('Reconnection successful');
  }
}
```

### Message Validation

```javascript
class ValidatedRealTimeAudio extends ConduitRealTimeAudio {
  sendText(message) {
    if (!this.validateMessage(message)) {
      console.error('Invalid message format:', message);
      return;
    }
    super.sendText(message);
  }

  validateMessage(message) {
    if (typeof message !== 'object' || message === null) {
      return false;
    }

    if (!message.type || typeof message.type !== 'string') {
      return false;
    }

    // Add provider-specific validation
    return true;
  }

  handleMessage(data) {
    try {
      super.handleMessage(data);
    } catch (error) {
      console.error('Message handling error:', error);
      // Don't crash the connection on message errors
    }
  }
}
```

## Monitoring and Debugging

### Connection Monitoring

```javascript
class MonitoredRealTimeAudio extends ConduitRealTimeAudio {
  constructor(apiKey, model, provider) {
    super(apiKey, model, provider);
    this.connectionStats = {
      messagesReceived: 0,
      messagesSent: 0,
      bytesReceived: 0,
      bytesSent: 0,
      connectionTime: null,
      lastActivity: null
    };
  }

  connect() {
    this.connectionStats.connectionTime = Date.now();
    super.connect();
  }

  handleMessage(data) {
    this.connectionStats.messagesReceived++;
    this.connectionStats.lastActivity = Date.now();
    
    if (data instanceof Buffer) {
      this.connectionStats.bytesReceived += data.length;
    }
    
    super.handleMessage(data);
  }

  send(data) {
    this.connectionStats.messagesSent++;
    this.connectionStats.lastActivity = Date.now();
    
    if (data instanceof Buffer) {
      this.connectionStats.bytesSent += data.length;
    }
    
    super.send(data);
  }

  getStats() {
    const now = Date.now();
    return {
      ...this.connectionStats,
      connectionDuration: this.connectionStats.connectionTime ? now - this.connectionStats.connectionTime : 0,
      timeSinceLastActivity: this.connectionStats.lastActivity ? now - this.connectionStats.lastActivity : null,
      isHealthy: this.isConnected && (now - (this.connectionStats.lastActivity || now)) < 30000
    };
  }
}

// Usage
const monitored = new MonitoredRealTimeAudio('condt_your_virtual_key');
monitored.connect();

// Check stats periodically
setInterval(() => {
  console.log('Connection stats:', monitored.getStats());
}, 10000);
```

## Limitations

### Current Implementation

- **Proxy only**: Conduit acts as a WebSocket proxy, not a full implementation
- **Provider-dependent**: Features depend on the underlying provider's WebSocket API
- **No session management**: Session state is maintained by the provider, not Conduit
- **Limited routing**: Basic model/provider routing, no advanced load balancing

### Best Practices

1. **Handle disconnections**: Implement reconnection logic for production use
2. **Validate messages**: Check message formats before sending
3. **Monitor connections**: Track connection health and performance
4. **Error recovery**: Implement graceful error handling
5. **Rate limiting**: Respect provider rate limits and connection limits

## Next Steps

- **Speech-to-Text**: Use [transcription services](speech-to-text) for audio processing
- **Text-to-Speech**: Implement [voice synthesis](text-to-speech) for responses
- **Audio Providers**: Learn about [available providers](providers)
- **Integration Examples**: See complete [client patterns](../clients/overview)