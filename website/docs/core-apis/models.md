---
sidebar_position: 4
title: Models
description: Discover available models, capabilities, and pricing across all providers
---

# Models

The Models endpoint provides comprehensive information about available models across all providers in your Conduit deployment, including capabilities, pricing, context limits, and real-time availability status.

## Quick Start

### List All Available Models

```javascript
import OpenAI from 'openai';

const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'https://api.conduit.yourdomain.com/v1'
});

const models = await openai.models.list();

console.log('Available models:', models.data.length);

models.data.forEach(model => {
  console.log(`${model.id} (${model.owned_by})`);
  console.log(`  Capabilities: ${model.capabilities?.join(', ')}`);
  console.log(`  Context: ${model.context_length} tokens`);
  console.log(`  Status: ${model.status}`);
});
```

### Get Specific Model Details

```javascript
const modelDetails = await openai.models.retrieve('gpt-4');

console.log('Model:', modelDetails.id);
console.log('Provider:', modelDetails.owned_by);
console.log('Description:', modelDetails.description);
console.log('Capabilities:', modelDetails.capabilities);
console.log('Pricing:', modelDetails.pricing);
console.log('Context Length:', modelDetails.context_length);
console.log('Max Output:', modelDetails.max_output_tokens);
```

## Model Response Format

### Standard Model Object

```json
{
  "id": "gpt-4",
  "object": "model",
  "created": 1687882411,
  "owned_by": "openai",
  "status": "available",
  "description": "GPT-4 is a large multimodal model that can solve difficult problems with greater accuracy than previous models.",
  "capabilities": [
    "chat_completions",
    "function_calling", 
    "vision",
    "streaming"
  ],
  "context_length": 8192,
  "max_output_tokens": 4096,
  "pricing": {
    "input_cost_per_token": 0.00003,
    "output_cost_per_token": 0.00006,
    "currency": "USD",
    "per_unit": 1
  },
  "limits": {
    "requests_per_minute": 10000,
    "tokens_per_minute": 300000
  },
  "multimodal": {
    "supports_images": true,
    "supports_audio": false,
    "max_image_size": "20MB",
    "supported_formats": ["png", "jpg", "gif", "webp"]
  },
  "provider_specific": {
    "temperature_range": [0.0, 2.0],
    "supports_system_messages": true,
    "supports_tools": true
  }
}
```

## Model Categories and Capabilities

### Chat Completion Models

```javascript
// Filter models by capability
const chatModels = models.data.filter(model => 
  model.capabilities?.includes('chat_completions')
);

console.log('Chat Completion Models:');
chatModels.forEach(model => {
  console.log(`- ${model.id}: ${model.context_length} context, $${model.pricing?.input_cost_per_token * 1000}/1K tokens`);
});
```

**Major Chat Models:**

| Provider | Model | Context | Cost (per 1K tokens) | Capabilities |
|----------|-------|---------|---------------------|-------------|
| **OpenAI** | gpt-4o | 128K | $5.00 / $15.00 | Vision, function calling, streaming |
| **OpenAI** | gpt-4-turbo | 128K | $10.00 / $30.00 | Vision, function calling, JSON mode |
| **OpenAI** | gpt-3.5-turbo | 16K | $0.50 / $1.50 | Function calling, streaming |
| **Anthropic** | claude-3-opus | 200K | $15.00 / $75.00 | Vision, tool use, large context |
| **Anthropic** | claude-3-sonnet | 200K | $3.00 / $15.00 | Vision, tool use, balanced |
| **Google** | gemini-pro | 32K | $0.50 / $1.50 | Multimodal, function calling |
| **Cohere** | command-r-plus | 128K | $3.00 / $15.00 | RAG optimized, tool use |

### Embedding Models

```javascript
const embeddingModels = models.data.filter(model => 
  model.capabilities?.includes('embeddings')
);

console.log('Embedding Models:');
embeddingModels.forEach(model => {
  const dimensions = model.provider_specific?.dimensions || 'Variable';
  console.log(`- ${model.id}: ${dimensions} dimensions, $${model.pricing?.input_cost_per_token * 1000}/1K tokens`);
});
```

**Major Embedding Models:**

| Provider | Model | Dimensions | Context | Cost (per 1K tokens) |
|----------|-------|------------|---------|---------------------|
| **OpenAI** | text-embedding-3-large | 3072 | 8191 | $0.13 |
| **OpenAI** | text-embedding-3-small | 1536 | 8191 | $0.02 |
| **OpenAI** | text-embedding-ada-002 | 1536 | 8191 | $0.10 |
| **Cohere** | embed-english-v3.0 | 1024 | 512 | $0.10 |
| **Google** | textembedding-gecko | 768 | 3072 | $0.025 |

### Audio Models

```javascript
const audioModels = models.data.filter(model => 
  model.capabilities?.some(cap => cap.includes('audio'))
);

console.log('Audio Models:');
audioModels.forEach(model => {
  console.log(`- ${model.id}: ${model.capabilities.filter(cap => cap.includes('audio')).join(', ')}`);
});
```

**Audio Capabilities:**

| Provider | Model | Capabilities | Languages | Cost |
|----------|-------|-------------|-----------|------|
| **OpenAI** | whisper-1 | speech_to_text | 99+ languages | $0.006/minute |
| **OpenAI** | tts-1 | text_to_speech | Optimized for English | $15/1M chars |
| **OpenAI** | tts-1-hd | text_to_speech | High quality | $15/1M chars |
| **ElevenLabs** | eleven-turbo-v2 | text_to_speech | 29+ languages | $0.18/1K chars |
| **Deepgram** | nova-2 | speech_to_text | Multiple languages | $0.0043/minute |

### Image Generation Models

```javascript
const imageModels = models.data.filter(model => 
  model.capabilities?.includes('image_generation')
);

console.log('Image Generation Models:');
imageModels.forEach(model => {
  const resolutions = model.provider_specific?.supported_sizes || [];
  console.log(`- ${model.id}: ${resolutions.join(', ')}`);
});
```

**Image Generation Capabilities:**

| Provider | Model | Max Resolution | Features | Cost per Image |
|----------|-------|----------------|----------|----------------|
| **OpenAI** | dall-e-3 | 1024x1024 | HD quality, style control | $0.04-0.08 |
| **OpenAI** | dall-e-2 | 1024x1024 | Standard quality | $0.02 |
| **MiniMax** | minimax-image | 1024x1024 | Fast generation | $0.01-0.04 |
| **Replicate** | Various | Up to 2048x2048 | Model variety | $0.001-0.10 |

## Advanced Model Filtering

### Filter by Capabilities

```javascript
class ModelDiscovery {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
    this.models = null;
  }

  async loadModels() {
    if (!this.models) {
      const response = await this.openai.models.list();
      this.models = response.data;
    }
    return this.models;
  }

  async findModelsByCapability(capability) {
    const models = await this.loadModels();
    return models.filter(model => 
      model.capabilities?.includes(capability)
    );
  }

  async findModelsByProvider(provider) {
    const models = await this.loadModels();
    return models.filter(model => 
      model.owned_by.toLowerCase() === provider.toLowerCase()
    );
  }

  async findModelsByPriceRange(maxInputCost, maxOutputCost = null) {
    const models = await this.loadModels();
    return models.filter(model => {
      const pricing = model.pricing;
      if (!pricing) return false;
      
      const inputOk = pricing.input_cost_per_token <= maxInputCost;
      const outputOk = !maxOutputCost || 
        pricing.output_cost_per_token <= maxOutputCost;
      
      return inputOk && outputOk;
    });
  }

  async findModelsByContextLength(minContext) {
    const models = await this.loadModels();
    return models.filter(model => 
      model.context_length >= minContext
    );
  }

  async findBestModelsForTask(task) {
    const models = await this.loadModels();
    
    const taskRequirements = {
      'chat': { capabilities: ['chat_completions'] },
      'reasoning': { 
        capabilities: ['chat_completions'], 
        providers: ['openai', 'anthropic'],
        minContext: 8000 
      },
      'code': { 
        capabilities: ['chat_completions'], 
        providers: ['openai', 'anthropic', 'cohere'],
        minContext: 16000 
      },
      'search': { capabilities: ['embeddings'] },
      'transcription': { capabilities: ['speech_to_text'] },
      'voice': { capabilities: ['text_to_speech'] },
      'images': { capabilities: ['image_generation'] },
      'vision': { 
        capabilities: ['chat_completions'],
        multimodal: true 
      }
    };

    const requirements = taskRequirements[task];
    if (!requirements) return [];

    let filtered = models;

    // Filter by capabilities
    if (requirements.capabilities) {
      filtered = filtered.filter(model =>
        requirements.capabilities.every(cap =>
          model.capabilities?.includes(cap)
        )
      );
    }

    // Filter by providers
    if (requirements.providers) {
      filtered = filtered.filter(model =>
        requirements.providers.includes(model.owned_by.toLowerCase())
      );
    }

    // Filter by context length
    if (requirements.minContext) {
      filtered = filtered.filter(model =>
        model.context_length >= requirements.minContext
      );
    }

    // Filter by multimodal support
    if (requirements.multimodal) {
      filtered = filtered.filter(model =>
        model.multimodal?.supports_images
      );
    }

    // Sort by cost efficiency (input cost per token)
    return filtered.sort((a, b) => {
      const costA = a.pricing?.input_cost_per_token || Infinity;
      const costB = b.pricing?.input_cost_per_token || Infinity;
      return costA - costB;
    });
  }
}

// Usage examples
const discovery = new ModelDiscovery('condt_your_virtual_key');

// Find vision-capable models
const visionModels = await discovery.findBestModelsForTask('vision');
console.log('Vision Models:', visionModels.map(m => m.id));

// Find cheap embedding models
const cheapEmbeddings = await discovery.findModelsByPriceRange(0.001);
console.log('Affordable Embeddings:', cheapEmbeddings.map(m => m.id));

// Find large context models
const largeContext = await discovery.findModelsByContextLength(100000);
console.log('Large Context Models:', largeContext.map(m => `${m.id} (${m.context_length})`));
```

### Cost Comparison Tool

```javascript
class ModelCostComparison {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
  }

  async compareModelsForWorkload(workload) {
    const models = await this.openai.models.list();
    const chatModels = models.data.filter(m => 
      m.capabilities?.includes('chat_completions')
    );

    const estimates = chatModels.map(model => {
      const pricing = model.pricing;
      if (!pricing) return null;

      const inputCost = workload.inputTokens * pricing.input_cost_per_token;
      const outputCost = workload.outputTokens * pricing.output_cost_per_token;
      const totalCost = (inputCost + outputCost) * workload.requestsPerDay * 30; // Monthly

      return {
        model: model.id,
        provider: model.owned_by,
        context: model.context_length,
        dailyCost: (inputCost + outputCost) * workload.requestsPerDay,
        monthlyCost: totalCost,
        costPerRequest: inputCost + outputCost,
        costBreakdown: {
          input: inputCost * workload.requestsPerDay * 30,
          output: outputCost * workload.requestsPerDay * 30
        }
      };
    }).filter(Boolean);

    return estimates.sort((a, b) => a.monthlyCost - b.monthlyCost);
  }

  formatCostComparison(estimates) {
    console.log('\n📊 Model Cost Comparison (Monthly)\n');
    console.log('Rank | Model | Provider | Context | Monthly Cost | Per Request');
    console.log('-----|-------|----------|---------|--------------|------------');

    estimates.forEach((estimate, index) => {
      const rank = (index + 1).toString().padStart(4);
      const model = estimate.model.padEnd(20);
      const provider = estimate.provider.padEnd(10);
      const context = (estimate.context + 'K').padEnd(8);
      const monthly = ('$' + estimate.monthlyCost.toFixed(2)).padStart(11);
      const perRequest = ('$' + estimate.costPerRequest.toFixed(4)).padStart(10);

      console.log(`${rank} | ${model} | ${provider} | ${context} | ${monthly} | ${perRequest}`);
    });
  }
}

// Usage example
const costComparison = new ModelCostComparison('condt_your_virtual_key');

const workload = {
  inputTokens: 1000,    // Average input per request
  outputTokens: 500,    // Average output per request
  requestsPerDay: 1000  // Daily request volume
};

const estimates = await costComparison.compareModelsForWorkload(workload);
costComparison.formatCostComparison(estimates.slice(0, 10)); // Top 10 cheapest
```

## Model Status and Health

### Check Model Availability

```javascript
async function checkModelHealth() {
  const models = await openai.models.list();
  
  const healthReport = {
    total: models.data.length,
    available: 0,
    unavailable: 0,
    limited: 0,
    byProvider: {}
  };

  models.data.forEach(model => {
    // Count by status
    switch (model.status) {
      case 'available':
        healthReport.available++;
        break;
      case 'unavailable':
        healthReport.unavailable++;
        break;
      case 'limited':
        healthReport.limited++;
        break;
    }

    // Group by provider
    const provider = model.owned_by;
    if (!healthReport.byProvider[provider]) {
      healthReport.byProvider[provider] = {
        total: 0,
        available: 0,
        unavailable: 0,
        limited: 0
      };
    }
    
    healthReport.byProvider[provider].total++;
    healthReport.byProvider[provider][model.status]++;
  });

  return healthReport;
}

// Generate health report
const health = await checkModelHealth();

console.log('🟢 Model Health Report');
console.log(`Total Models: ${health.total}`);
console.log(`Available: ${health.available}`);
console.log(`Unavailable: ${health.unavailable}`);
console.log(`Limited: ${health.limited}`);

console.log('\n📡 By Provider:');
Object.entries(health.byProvider).forEach(([provider, stats]) => {
  const availability = (stats.available / stats.total * 100).toFixed(1);
  console.log(`${provider}: ${stats.available}/${stats.total} (${availability}% available)`);
});
```

### Model Recommendation Engine

```javascript
class ModelRecommendationEngine {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
  }

  async recommendModels(requirements) {
    const models = await this.openai.models.list();
    let candidates = models.data.filter(m => m.status === 'available');

    // Apply filters based on requirements
    if (requirements.capability) {
      candidates = candidates.filter(m => 
        m.capabilities?.includes(requirements.capability)
      );
    }

    if (requirements.maxCostPerToken) {
      candidates = candidates.filter(m => 
        m.pricing?.input_cost_per_token <= requirements.maxCostPerToken
      );
    }

    if (requirements.minContextLength) {
      candidates = candidates.filter(m => 
        m.context_length >= requirements.minContextLength
      );
    }

    if (requirements.multimodal) {
      candidates = candidates.filter(m => 
        m.multimodal?.supports_images
      );
    }

    if (requirements.preferredProviders) {
      candidates = candidates.filter(m => 
        requirements.preferredProviders.includes(m.owned_by.toLowerCase())
      );
    }

    // Score and rank candidates
    const scored = candidates.map(model => ({
      ...model,
      score: this.calculateScore(model, requirements)
    }));

    return scored
      .sort((a, b) => b.score - a.score)
      .slice(0, requirements.maxResults || 5);
  }

  calculateScore(model, requirements) {
    let score = 100; // Base score

    // Cost efficiency (lower cost = higher score)
    if (model.pricing?.input_cost_per_token) {
      const costFactor = 1 / (model.pricing.input_cost_per_token * 10000);
      score += Math.min(costFactor, 50); // Cap cost bonus
    }

    // Context length bonus
    if (requirements.minContextLength) {
      const contextBonus = Math.min(
        (model.context_length / requirements.minContextLength - 1) * 20,
        30
      );
      score += contextBonus;
    }

    // Provider preference
    if (requirements.preferredProviders?.includes(model.owned_by.toLowerCase())) {
      score += 25;
    }

    // Capability richness
    const capabilityBonus = (model.capabilities?.length || 0) * 5;
    score += Math.min(capabilityBonus, 25);

    // Quality indicators
    if (model.id.includes('turbo') || model.id.includes('gpt-4')) {
      score += 15; // OpenAI quality bonus
    }
    
    if (model.id.includes('claude-3')) {
      score += 15; // Anthropic quality bonus
    }

    // Penalize if limited availability
    if (model.status === 'limited') {
      score -= 20;
    }

    return Math.round(score);
  }

  explainRecommendation(model, requirements) {
    const explanation = [`Recommended: ${model.id}`];
    
    explanation.push(`Provider: ${model.owned_by}`);
    explanation.push(`Score: ${model.score}/100`);
    
    if (model.pricing) {
      const monthlyCost = model.pricing.input_cost_per_token * 1000 * 
        (requirements.estimatedTokensPerMonth || 1000000);
      explanation.push(`Estimated monthly cost: $${monthlyCost.toFixed(2)}`);
    }

    explanation.push(`Context: ${model.context_length.toLocaleString()} tokens`);
    explanation.push(`Capabilities: ${model.capabilities?.join(', ')}`);

    return explanation.join('\n');
  }
}

// Usage examples
const recommender = new ModelRecommendationEngine('condt_your_virtual_key');

// Find best chat model for coding tasks
const codingModels = await recommender.recommendModels({
  capability: 'chat_completions',
  minContextLength: 16000,
  maxCostPerToken: 0.00005,
  preferredProviders: ['openai', 'anthropic'],
  estimatedTokensPerMonth: 2000000,
  maxResults: 3
});

console.log('🤖 Best Models for Coding:');
codingModels.forEach(model => {
  console.log('\n' + recommender.explainRecommendation(model, {
    estimatedTokensPerMonth: 2000000
  }));
});

// Find best embedding model for search
const embeddingModels = await recommender.recommendModels({
  capability: 'embeddings',
  maxCostPerToken: 0.0001,
  maxResults: 3
});

console.log('\n🔍 Best Embedding Models:');
embeddingModels.forEach(model => {
  console.log(`${model.id}: $${(model.pricing?.input_cost_per_token * 1000).toFixed(3)}/1K tokens`);
});
```

## Real-Time Model Updates

### Model Change Notifications

```javascript
// Using SignalR for real-time model updates
import { HubConnectionBuilder } from '@microsoft/signalr';

class ModelStatusMonitor {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.connection = new HubConnectionBuilder()
      .withUrl('https://api.conduit.yourdomain.com/hubs/navigation-state', {
        accessTokenFactory: () => apiKey
      })
      .build();
    
    this.setupEventHandlers();
  }

  setupEventHandlers() {
    this.connection.on('ModelStatusChanged', (data) => {
      console.log(`Model ${data.modelId} status changed: ${data.oldStatus} → ${data.newStatus}`);
      this.onModelStatusChange(data);
    });

    this.connection.on('NewModelAdded', (data) => {
      console.log(`New model available: ${data.modelId} (${data.provider})`);
      this.onNewModel(data);
    });

    this.connection.on('ModelRemoved', (data) => {
      console.log(`Model removed: ${data.modelId}`);
      this.onModelRemoved(data);
    });

    this.connection.on('ProviderHealthChanged', (data) => {
      console.log(`Provider ${data.provider} health: ${data.status}`);
      this.onProviderHealthChange(data);
    });
  }

  async start() {
    try {
      await this.connection.start();
      console.log('🔄 Connected to model status updates');
    } catch (error) {
      console.error('Failed to connect:', error);
    }
  }

  onModelStatusChange(data) {
    if (data.newStatus === 'unavailable') {
      this.handleModelUnavailable(data.modelId);
    } else if (data.newStatus === 'available' && data.oldStatus === 'unavailable') {
      this.handleModelRestored(data.modelId);
    }
  }

  onNewModel(data) {
    // Automatically evaluate new models for existing use cases
    this.evaluateNewModel(data);
  }

  handleModelUnavailable(modelId) {
    // Implement fallback logic
    console.log(`⚠️  Model ${modelId} unavailable - switching to fallback`);
  }

  handleModelRestored(modelId) {
    console.log(`✅ Model ${modelId} restored`);
  }

  async evaluateNewModel(modelData) {
    // Check if new model might be better for current use cases
    if (modelData.capabilities?.includes('chat_completions')) {
      console.log(`🆕 New chat model available: ${modelData.modelId}`);
      // Could trigger re-evaluation of model recommendations
    }
  }
}

// Usage
const monitor = new ModelStatusMonitor('condt_your_virtual_key');
await monitor.start();
```

## Integration Patterns

### Dynamic Model Selection

```javascript
class AdaptiveModelSelector {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
    this.modelCache = null;
    this.lastUpdate = null;
  }

  async getAvailableModels(forceRefresh = false) {
    const cacheAge = this.lastUpdate ? Date.now() - this.lastUpdate : Infinity;
    
    if (forceRefresh || !this.modelCache || cacheAge > 300000) { // 5 minutes
      const response = await this.openai.models.list();
      this.modelCache = response.data.filter(m => m.status === 'available');
      this.lastUpdate = Date.now();
    }
    
    return this.modelCache;
  }

  async selectBestModel(task, constraints = {}) {
    const models = await this.getAvailableModels();
    
    const suitable = models.filter(model => {
      // Check capability requirements
      if (task.requiredCapabilities) {
        const hasCapabilities = task.requiredCapabilities.every(cap =>
          model.capabilities?.includes(cap)
        );
        if (!hasCapabilities) return false;
      }

      // Check cost constraints
      if (constraints.maxCostPerToken) {
        const cost = model.pricing?.input_cost_per_token || 0;
        if (cost > constraints.maxCostPerToken) return false;
      }

      // Check context requirements
      if (task.contextLength) {
        if (model.context_length < task.contextLength) return false;
      }

      // Check provider preferences
      if (constraints.preferredProviders) {
        if (!constraints.preferredProviders.includes(model.owned_by)) return false;
      }

      return true;
    });

    if (suitable.length === 0) {
      throw new Error('No suitable models available for this task');
    }

    // Select based on strategy
    const strategy = constraints.selectionStrategy || 'balanced';
    
    switch (strategy) {
      case 'cheapest':
        return suitable.sort((a, b) => 
          (a.pricing?.input_cost_per_token || 0) - (b.pricing?.input_cost_per_token || 0)
        )[0];
        
      case 'fastest':
        // Prefer models known for speed
        const fastModels = suitable.filter(m => 
          m.id.includes('turbo') || m.id.includes('3.5') || m.id.includes('haiku')
        );
        return fastModels.length > 0 ? fastModels[0] : suitable[0];
        
      case 'highest_quality':
        // Prefer premium models
        const qualityOrder = ['gpt-4', 'claude-3-opus', 'claude-3-sonnet', 'gpt-3.5'];
        for (const prefix of qualityOrder) {
          const found = suitable.find(m => m.id.startsWith(prefix));
          if (found) return found;
        }
        return suitable[0];
        
      case 'balanced':
      default:
        // Balance cost and quality
        return suitable.sort((a, b) => {
          const scoreA = this.calculateBalancedScore(a);
          const scoreB = this.calculateBalancedScore(b);
          return scoreB - scoreA;
        })[0];
    }
  }

  calculateBalancedScore(model) {
    let score = 100;
    
    // Cost efficiency (inverse cost)
    const cost = model.pricing?.input_cost_per_token || 0;
    score += Math.max(0, 50 - (cost * 100000)); // Scale cost to reasonable range
    
    // Context length bonus
    score += Math.min(model.context_length / 1000, 50);
    
    // Capability richness
    score += (model.capabilities?.length || 0) * 5;
    
    // Quality heuristics
    if (model.id.includes('gpt-4')) score += 30;
    if (model.id.includes('claude-3')) score += 25;
    if (model.id.includes('turbo')) score += 10;
    
    return score;
  }

  async executeWithFallback(task, primaryModel = null) {
    const models = primaryModel 
      ? [primaryModel] 
      : [await this.selectBestModel(task)];
    
    // Add fallback models
    const allModels = await this.getAvailableModels();
    const fallbacks = allModels
      .filter(m => 
        !models.includes(m) && 
        task.requiredCapabilities?.every(cap => m.capabilities?.includes(cap))
      )
      .slice(0, 2); // Up to 2 fallbacks
    
    models.push(...fallbacks);

    for (const model of models) {
      try {
        console.log(`🎯 Trying model: ${model.id}`);
        
        const result = await this.executeTask(task, model);
        
        console.log(`✅ Success with ${model.id}`);
        return result;
        
      } catch (error) {
        console.log(`❌ Failed with ${model.id}: ${error.message}`);
        
        if (models.indexOf(model) === models.length - 1) {
          throw new Error(`All models failed. Last error: ${error.message}`);
        }
      }
    }
  }

  async executeTask(task, model) {
    switch (task.type) {
      case 'chat':
        return await this.openai.chat.completions.create({
          model: model.id,
          messages: task.messages,
          ...task.options
        });
        
      case 'embedding':
        return await this.openai.embeddings.create({
          model: model.id,
          input: task.input,
          ...task.options
        });
        
      default:
        throw new Error(`Unsupported task type: ${task.type}`);
    }
  }
}

// Usage example
const selector = new AdaptiveModelSelector('condt_your_virtual_key');

const chatTask = {
  type: 'chat',
  requiredCapabilities: ['chat_completions'],
  contextLength: 4000,
  messages: [
    { role: 'user', content: 'Explain quantum computing' }
  ],
  options: { max_tokens: 500 }
};

const result = await selector.executeWithFallback(chatTask);
console.log('Response:', result.choices[0].message.content);
```

## Next Steps

- **Chat Completions**: Use discovered models for [chat completions](chat-completions)
- **Embeddings**: Apply model selection to [embedding generation](embeddings)
- **Audio Platform**: Explore [audio model capabilities](../audio/overview)
- **Media Generation**: Discover [image and video models](../media/overview)
- **Integration Examples**: See complete [client patterns](../clients/overview)