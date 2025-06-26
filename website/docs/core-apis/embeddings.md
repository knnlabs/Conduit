---
sidebar_position: 3
title: Embeddings
description: Generate vector embeddings for semantic search, RAG, and machine learning applications
---

# Embeddings

Conduit's Embeddings API generates high-dimensional vector representations of text, enabling semantic search, retrieval-augmented generation (RAG), clustering, and similarity analysis across multiple providers.

## Quick Start

### Basic Text Embeddings

```javascript
import OpenAI from 'openai';

const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'https://api.conduit.yourdomain.com/v1'
});

const response = await openai.embeddings.create({
  model: 'text-embedding-3-large',
  input: 'The quick brown fox jumps over the lazy dog',
  encoding_format: 'float'
});

console.log('Embedding dimensions:', response.data[0].embedding.length);
console.log('First few values:', response.data[0].embedding.slice(0, 5));
```

### Batch Text Embeddings

```javascript
const texts = [
  'Artificial intelligence is transforming technology',
  'Machine learning enables computers to learn from data',
  'Deep learning uses neural networks with multiple layers',
  'Natural language processing helps computers understand text'
];

const response = await openai.embeddings.create({
  model: 'text-embedding-3-large',
  input: texts,
  encoding_format: 'float'
});

// Process each embedding
response.data.forEach((embedding, index) => {
  console.log(`Text ${index + 1}: ${texts[index]}`);
  console.log(`Embedding length: ${embedding.embedding.length}`);
});
```

## Supported Models and Providers

### OpenAI Embeddings

| Model | Dimensions | Max Tokens | Cost (per 1K tokens) | Best For |
|-------|------------|------------|---------------------|----------|
| **text-embedding-3-large** | 3072 | 8191 | $0.13 | Highest quality, maximum performance |
| **text-embedding-3-small** | 1536 | 8191 | $0.02 | Good balance of quality and cost |
| **text-embedding-ada-002** | 1536 | 8191 | $0.10 | Legacy model, still reliable |

```javascript
// High-quality embeddings for critical applications
const largeEmbedding = await openai.embeddings.create({
  model: 'text-embedding-3-large',
  input: 'Complex technical documentation requiring precise semantic understanding',
  dimensions: 3072 // Full dimensionality
});

// Cost-effective embeddings for large-scale processing
const smallEmbedding = await openai.embeddings.create({
  model: 'text-embedding-3-small', 
  input: 'General purpose text for similarity search',
  dimensions: 1536
});
```

### Azure OpenAI Embeddings

| Model | Dimensions | Max Tokens | Cost (per 1K tokens) | Deployment |
|-------|------------|------------|---------------------|------------|
| **text-embedding-3-large** | 3072 | 8191 | $0.13 | Azure deployment required |
| **text-embedding-3-small** | 1536 | 8191 | $0.02 | Azure deployment required |
| **text-embedding-ada-002** | 1536 | 8191 | $0.10 | Azure deployment required |

```javascript
const azureEmbedding = await openai.embeddings.create({
  model: 'azure-text-embedding-3-large',
  input: 'Text to embed using Azure OpenAI',
  deployment_name: 'your-embedding-deployment' // Azure-specific parameter
});
```

### Google Vertex AI Embeddings

| Model | Dimensions | Max Tokens | Cost (per 1K tokens) | Best For |
|-------|------------|------------|---------------------|----------|
| **textembedding-gecko** | 768 | 3072 | $0.025 | Multilingual support |
| **textembedding-gecko-multilingual** | 768 | 3072 | $0.025 | 100+ languages |

```javascript
const googleEmbedding = await openai.embeddings.create({
  model: 'textembedding-gecko',
  input: 'Text to embed using Google Vertex AI',
  task_type: 'RETRIEVAL_DOCUMENT' // Google-specific task optimization
});
```

### Cohere Embeddings

| Model | Dimensions | Max Tokens | Cost (per 1K tokens) | Best For |
|-------|------------|------------|---------------------|----------|
| **embed-english-v3.0** | 1024 | 512 | $0.10 | English text optimization |
| **embed-multilingual-v3.0** | 1024 | 512 | $0.10 | 100+ languages |
| **embed-english-light-v3.0** | 384 | 512 | $0.05 | Fast, lightweight processing |

```javascript
const cohereEmbedding = await openai.embeddings.create({
  model: 'embed-english-v3.0',
  input: 'Text to embed using Cohere',
  input_type: 'search_document', // Cohere-specific optimization
  truncate: 'END' // Handle long texts
});
```

## Request Parameters

### Core Parameters

```javascript
const response = await openai.embeddings.create({
  // Required parameters
  model: 'text-embedding-3-large',     // Embedding model
  input: 'Text to embed',              // String or array of strings
  
  // Optional parameters
  encoding_format: 'float',            // 'float' or 'base64'
  dimensions: 1536,                    // Reduce dimensionality (if supported)
  user: 'user-123',                    // User tracking
  
  // Provider-specific parameters
  task_type: 'RETRIEVAL_DOCUMENT',     // Google: task optimization
  input_type: 'search_document',       // Cohere: input type
  truncate: 'END'                      // Cohere: truncation strategy
});
```

### Dimensionality Reduction

```javascript
// OpenAI supports dimensionality reduction for efficiency
const reducedEmbedding = await openai.embeddings.create({
  model: 'text-embedding-3-large',
  input: 'Text to embed with reduced dimensions',
  dimensions: 1024 // Reduce from 3072 to 1024 dimensions
});

// Benefits: Smaller storage, faster similarity calculations
// Trade-off: Slight reduction in semantic precision
```

### Encoding Formats

```javascript
// Float format (default) - for direct computation
const floatEmbedding = await openai.embeddings.create({
  model: 'text-embedding-3-small',
  input: 'Text for float encoding',
  encoding_format: 'float'
});

// Base64 format - for compact transmission/storage
const base64Embedding = await openai.embeddings.create({
  model: 'text-embedding-3-small', 
  input: 'Text for base64 encoding',
  encoding_format: 'base64'
});

// Convert base64 back to float array
function base64ToFloat(base64String) {
  const binaryString = atob(base64String);
  const floatArray = new Float32Array(binaryString.length / 4);
  
  for (let i = 0; i < floatArray.length; i++) {
    const offset = i * 4;
    const bytes = [
      binaryString.charCodeAt(offset),
      binaryString.charCodeAt(offset + 1), 
      binaryString.charCodeAt(offset + 2),
      binaryString.charCodeAt(offset + 3)
    ];
    
    const dataView = new DataView(new ArrayBuffer(4));
    bytes.forEach((byte, index) => dataView.setUint8(index, byte));
    floatArray[i] = dataView.getFloat32(0, true);
  }
  
  return Array.from(floatArray);
}
```

## Semantic Search Implementation

### Vector Similarity Search

```javascript
class SemanticSearch {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
    this.documents = [];
    this.embeddings = [];
  }

  async addDocument(text, metadata = {}) {
    // Generate embedding for the document
    const response = await this.openai.embeddings.create({
      model: 'text-embedding-3-large',
      input: text
    });

    const embedding = response.data[0].embedding;
    
    this.documents.push({ text, metadata });
    this.embeddings.push(embedding);
    
    return this.documents.length - 1; // Return document index
  }

  async search(query, topK = 5) {
    // Generate embedding for the query
    const response = await this.openai.embeddings.create({
      model: 'text-embedding-3-large',
      input: query
    });

    const queryEmbedding = response.data[0].embedding;
    
    // Calculate similarities
    const similarities = this.embeddings.map((docEmbedding, index) => ({
      index,
      similarity: this.cosineSimilarity(queryEmbedding, docEmbedding),
      document: this.documents[index]
    }));

    // Sort by similarity and return top K
    return similarities
      .sort((a, b) => b.similarity - a.similarity)
      .slice(0, topK);
  }

  cosineSimilarity(a, b) {
    const dotProduct = a.reduce((sum, val, i) => sum + val * b[i], 0);
    const magnitudeA = Math.sqrt(a.reduce((sum, val) => sum + val * val, 0));
    const magnitudeB = Math.sqrt(b.reduce((sum, val) => sum + val * val, 0));
    
    return dotProduct / (magnitudeA * magnitudeB);
  }
}

// Usage example
const search = new SemanticSearch('condt_your_virtual_key');

// Add documents to search index
await search.addDocument(
  'Machine learning is a subset of artificial intelligence',
  { category: 'AI', source: 'textbook' }
);

await search.addDocument(
  'Deep learning uses neural networks with multiple layers',
  { category: 'AI', source: 'research paper' }
);

await search.addDocument(
  'Python is a popular programming language for data science',
  { category: 'Programming', source: 'tutorial' }
);

// Search for similar documents
const results = await search.search('What is artificial intelligence?', 3);

results.forEach((result, index) => {
  console.log(`${index + 1}. Similarity: ${result.similarity.toFixed(3)}`);
  console.log(`   Text: ${result.document.text}`);
  console.log(`   Category: ${result.document.metadata.category}`);
});
```

### RAG (Retrieval-Augmented Generation)

```javascript
class RAGSystem {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
    this.knowledgeBase = new SemanticSearch(apiKey);
  }

  async addKnowledge(documents) {
    for (const doc of documents) {
      await this.knowledgeBase.addDocument(doc.content, doc.metadata);
    }
  }

  async generateAnswer(question, topK = 3) {
    // Retrieve relevant documents
    const relevantDocs = await this.knowledgeBase.search(question, topK);
    
    // Construct context from retrieved documents
    const context = relevantDocs
      .map((doc, index) => `[${index + 1}] ${doc.document.text}`)
      .join('\n\n');

    // Generate answer using retrieved context
    const completion = await this.openai.chat.completions.create({
      model: 'gpt-4',
      messages: [
        {
          role: 'system',
          content: `You are a helpful assistant. Use the following context to answer the user's question. If the answer cannot be found in the context, say so clearly.

Context:
${context}`
        },
        {
          role: 'user',
          content: question
        }
      ],
      max_tokens: 500,
      temperature: 0.1
    });

    return {
      answer: completion.choices[0].message.content,
      sources: relevantDocs.map(doc => ({
        text: doc.document.text,
        similarity: doc.similarity,
        metadata: doc.document.metadata
      }))
    };
  }
}

// Usage example
const rag = new RAGSystem('condt_your_virtual_key');

// Add knowledge base
await rag.addKnowledge([
  {
    content: 'The capital of France is Paris. Paris is known for the Eiffel Tower and the Louvre Museum.',
    metadata: { topic: 'geography', country: 'France' }
  },
  {
    content: 'Machine learning algorithms can be supervised, unsupervised, or reinforcement learning.',
    metadata: { topic: 'technology', subject: 'AI' }
  },
  {
    content: 'Photosynthesis is the process by which plants convert sunlight into energy.',
    metadata: { topic: 'science', subject: 'biology' }
  }
]);

// Ask questions and get context-aware answers
const result = await rag.generateAnswer('What is the capital of France?');
console.log('Answer:', result.answer);
console.log('Sources:', result.sources);
```

## Advanced Use Cases

### Document Clustering

```javascript
class DocumentClustering {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
  }

  async clusterDocuments(documents, numClusters = 3) {
    // Generate embeddings for all documents
    const embeddings = [];
    
    for (const doc of documents) {
      const response = await this.openai.embeddings.create({
        model: 'text-embedding-3-small',
        input: doc.text
      });
      
      embeddings.push({
        text: doc.text,
        embedding: response.data[0].embedding,
        metadata: doc.metadata || {}
      });
    }

    // Simple K-means clustering implementation
    const clusters = this.kMeansClustering(embeddings, numClusters);
    
    return clusters;
  }

  kMeansClustering(embeddings, k) {
    const dimensions = embeddings[0].embedding.length;
    
    // Initialize random centroids
    let centroids = Array.from({ length: k }, () => 
      Array.from({ length: dimensions }, () => Math.random() - 0.5)
    );

    let assignments = new Array(embeddings.length);
    let converged = false;
    let iterations = 0;
    const maxIterations = 100;

    while (!converged && iterations < maxIterations) {
      // Assign each point to nearest centroid
      const newAssignments = embeddings.map((emb, index) => {
        let minDistance = Infinity;
        let closestCentroid = 0;

        centroids.forEach((centroid, centroidIndex) => {
          const distance = this.euclideanDistance(emb.embedding, centroid);
          if (distance < minDistance) {
            minDistance = distance;
            closestCentroid = centroidIndex;
          }
        });

        return closestCentroid;
      });

      // Check for convergence
      converged = assignments.every((assignment, index) => 
        assignment === newAssignments[index]
      );
      
      assignments = newAssignments;

      // Update centroids
      if (!converged) {
        centroids = centroids.map((_, centroidIndex) => {
          const clusterPoints = embeddings.filter((_, pointIndex) => 
            assignments[pointIndex] === centroidIndex
          );

          if (clusterPoints.length === 0) return centroids[centroidIndex];

          return Array.from({ length: dimensions }, (_, dim) => {
            const sum = clusterPoints.reduce((acc, point) => 
              acc + point.embedding[dim], 0
            );
            return sum / clusterPoints.length;
          });
        });
      }

      iterations++;
    }

    // Group documents by cluster
    const clusters = Array.from({ length: k }, () => []);
    embeddings.forEach((emb, index) => {
      clusters[assignments[index]].push({
        text: emb.text,
        metadata: emb.metadata
      });
    });

    return clusters;
  }

  euclideanDistance(a, b) {
    return Math.sqrt(a.reduce((sum, val, i) => sum + Math.pow(val - b[i], 2), 0));
  }
}

// Usage example
const clustering = new DocumentClustering('condt_your_virtual_key');

const documents = [
  { text: 'Machine learning models require training data' },
  { text: 'Deep neural networks have multiple layers' },
  { text: 'Natural language processing analyzes text' },
  { text: 'Computer vision processes images' },
  { text: 'The weather is sunny today' },
  { text: 'Rain is expected tomorrow' },
  { text: 'Clouds are forming in the sky' },
  { text: 'Basketball is a popular sport' },
  { text: 'Football has many fans worldwide' },
  { text: 'Tennis requires good hand-eye coordination' }
];

const clusters = await clustering.clusterDocuments(documents, 3);

clusters.forEach((cluster, index) => {
  console.log(`\nCluster ${index + 1}:`);
  cluster.forEach(doc => {
    console.log(`  - ${doc.text}`);
  });
});
```

### Semantic Deduplication

```javascript
class SemanticDeduplication {
  constructor(apiKey, similarityThreshold = 0.9) {
    this.apiKey = apiKey;
    this.similarityThreshold = similarityThreshold;
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
  }

  async deduplicateDocuments(documents) {
    const embeddings = [];
    
    // Generate embeddings
    for (const doc of documents) {
      const response = await this.openai.embeddings.create({
        model: 'text-embedding-3-small',
        input: doc.text
      });
      
      embeddings.push({
        ...doc,
        embedding: response.data[0].embedding
      });
    }

    const unique = [];
    const duplicates = [];

    for (let i = 0; i < embeddings.length; i++) {
      let isDuplicate = false;
      
      for (let j = 0; j < unique.length; j++) {
        const similarity = this.cosineSimilarity(
          embeddings[i].embedding,
          unique[j].embedding
        );
        
        if (similarity >= this.similarityThreshold) {
          duplicates.push({
            original: unique[j],
            duplicate: embeddings[i],
            similarity: similarity
          });
          isDuplicate = true;
          break;
        }
      }
      
      if (!isDuplicate) {
        unique.push(embeddings[i]);
      }
    }

    return {
      unique: unique.map(doc => ({ text: doc.text, metadata: doc.metadata })),
      duplicates: duplicates
    };
  }

  cosineSimilarity(a, b) {
    const dotProduct = a.reduce((sum, val, i) => sum + val * b[i], 0);
    const magnitudeA = Math.sqrt(a.reduce((sum, val) => sum + val * val, 0));
    const magnitudeB = Math.sqrt(b.reduce((sum, val) => sum + val * val, 0));
    
    return dotProduct / (magnitudeA * magnitudeB);
  }
}

// Usage example
const deduplication = new SemanticDeduplication('condt_your_virtual_key', 0.85);

const documents = [
  { text: 'Machine learning is a subset of artificial intelligence' },
  { text: 'ML is part of the broader field of AI' }, // Similar to above
  { text: 'Deep learning uses neural networks' },
  { text: 'The weather is sunny today' },
  { text: 'It is a bright and sunny day' }, // Similar to above
  { text: 'Python is a programming language' }
];

const result = await deduplication.deduplicateDocuments(documents);

console.log('Unique documents:');
result.unique.forEach((doc, index) => {
  console.log(`${index + 1}. ${doc.text}`);
});

console.log('\nDuplicates found:');
result.duplicates.forEach((dup, index) => {
  console.log(`${index + 1}. Similarity: ${dup.similarity.toFixed(3)}`);
  console.log(`   Original: ${dup.original.text}`);
  console.log(`   Duplicate: ${dup.duplicate.text}`);
});
```

## Performance Optimization

### Batch Processing for Large Datasets

```javascript
class BatchEmbeddingProcessor {
  constructor(apiKey, batchSize = 100, concurrency = 5) {
    this.apiKey = apiKey;
    this.batchSize = batchSize;
    this.concurrency = concurrency;
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
  }

  async processLargeDataset(texts, model = 'text-embedding-3-small') {
    const results = [];
    const batches = this.createBatches(texts);
    
    console.log(`Processing ${texts.length} texts in ${batches.length} batches`);

    // Process batches with controlled concurrency
    for (let i = 0; i < batches.length; i += this.concurrency) {
      const batchGroup = batches.slice(i, i + this.concurrency);
      
      const batchPromises = batchGroup.map(async (batch, batchIndex) => {
        const actualBatchIndex = i + batchIndex;
        console.log(`Processing batch ${actualBatchIndex + 1}/${batches.length}`);
        
        try {
          const response = await this.openai.embeddings.create({
            model: model,
            input: batch
          });
          
          return response.data.map(item => item.embedding);
        } catch (error) {
          console.error(`Batch ${actualBatchIndex + 1} failed:`, error.message);
          throw error;
        }
      });

      const batchResults = await Promise.all(batchPromises);
      results.push(...batchResults.flat());
      
      // Rate limiting pause between batch groups
      if (i + this.concurrency < batches.length) {
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
    }

    return results;
  }

  createBatches(texts) {
    const batches = [];
    for (let i = 0; i < texts.length; i += this.batchSize) {
      batches.push(texts.slice(i, i + this.batchSize));
    }
    return batches;
  }

  async processWithRetry(texts, model, maxRetries = 3) {
    for (let attempt = 1; attempt <= maxRetries; attempt++) {
      try {
        return await this.processLargeDataset(texts, model);
      } catch (error) {
        console.log(`Attempt ${attempt} failed:`, error.message);
        
        if (attempt === maxRetries) {
          throw error;
        }
        
        // Exponential backoff
        const delay = Math.pow(2, attempt) * 1000;
        console.log(`Retrying in ${delay}ms...`);
        await new Promise(resolve => setTimeout(resolve, delay));
      }
    }
  }
}

// Usage for large datasets
const processor = new BatchEmbeddingProcessor('condt_your_virtual_key', 50, 3);

const largeDataset = Array.from({ length: 1000 }, (_, i) => 
  `This is document number ${i + 1} with some sample content.`
);

const embeddings = await processor.processWithRetry(
  largeDataset, 
  'text-embedding-3-small'
);

console.log(`Generated ${embeddings.length} embeddings`);
```

### Caching for Repeated Requests

```javascript
class EmbeddingCache {
  constructor(apiKey, ttlMinutes = 60) {
    this.apiKey = apiKey;
    this.cache = new Map();
    this.ttl = ttlMinutes * 60 * 1000; // Convert to milliseconds
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
  }

  getCacheKey(text, model) {
    return crypto.createHash('md5')
      .update(JSON.stringify({ text, model }))
      .digest('hex');
  }

  async getEmbedding(text, model = 'text-embedding-3-small') {
    const cacheKey = this.getCacheKey(text, model);
    const cached = this.cache.get(cacheKey);
    
    // Check if cached and not expired
    if (cached && Date.now() - cached.timestamp < this.ttl) {
      console.log('Using cached embedding');
      return cached.embedding;
    }

    // Generate new embedding
    console.log('Generating new embedding');
    const response = await this.openai.embeddings.create({
      model: model,
      input: text
    });

    const embedding = response.data[0].embedding;
    
    // Cache the result
    this.cache.set(cacheKey, {
      embedding: embedding,
      timestamp: Date.now()
    });

    return embedding;
  }

  async getBatchEmbeddings(texts, model = 'text-embedding-3-small') {
    const embeddings = [];
    const uncachedTexts = [];
    const uncachedIndices = [];

    // Check cache for each text
    for (let i = 0; i < texts.length; i++) {
      const cacheKey = this.getCacheKey(texts[i], model);
      const cached = this.cache.get(cacheKey);
      
      if (cached && Date.now() - cached.timestamp < this.ttl) {
        embeddings[i] = cached.embedding;
      } else {
        uncachedTexts.push(texts[i]);
        uncachedIndices.push(i);
      }
    }

    // Generate embeddings for uncached texts
    if (uncachedTexts.length > 0) {
      console.log(`Generating ${uncachedTexts.length} new embeddings`);
      
      const response = await this.openai.embeddings.create({
        model: model,
        input: uncachedTexts
      });

      // Cache new embeddings and fill results
      response.data.forEach((item, index) => {
        const originalIndex = uncachedIndices[index];
        const text = texts[originalIndex];
        const embedding = item.embedding;
        
        // Cache the result
        const cacheKey = this.getCacheKey(text, model);
        this.cache.set(cacheKey, {
          embedding: embedding,
          timestamp: Date.now()
        });
        
        embeddings[originalIndex] = embedding;
      });
    }

    console.log(`Cache hits: ${texts.length - uncachedTexts.length}/${texts.length}`);
    return embeddings;
  }

  clearExpiredCache() {
    const now = Date.now();
    for (const [key, value] of this.cache.entries()) {
      if (now - value.timestamp >= this.ttl) {
        this.cache.delete(key);
      }
    }
  }

  getCacheStats() {
    return {
      size: this.cache.size,
      memoryUsage: this.cache.size * 1536 * 4, // Approximate bytes for 1536-dim embeddings
      ttlMinutes: this.ttl / (60 * 1000)
    };
  }
}

// Usage example
const embeddingCache = new EmbeddingCache('condt_your_virtual_key', 30);

// These will be generated fresh
const embedding1 = await embeddingCache.getEmbedding('First text');
const embedding2 = await embeddingCache.getEmbedding('Second text');

// This will use cache
const embedding1Cached = await embeddingCache.getEmbedding('First text');

console.log('Cache stats:', embeddingCache.getCacheStats());
```

## Error Handling

### Common Embedding Errors

```javascript
async function robustEmbeddingGeneration(text, model = 'text-embedding-3-small') {
  try {
    const response = await openai.embeddings.create({
      model: model,
      input: text
    });
    
    return response.data[0].embedding;
  } catch (error) {
    switch (error.code) {
      case 'context_length_exceeded':
        console.log('Text too long, truncating...');
        const truncatedText = text.substring(0, 8000); // Safe limit
        return robustEmbeddingGeneration(truncatedText, model);
        
      case 'rate_limit_exceeded':
        console.log('Rate limited, waiting...');
        await new Promise(resolve => setTimeout(resolve, 60000)); // Wait 1 minute
        return robustEmbeddingGeneration(text, model);
        
      case 'insufficient_quota':
        console.log('Quota exceeded, switching to smaller model...');
        if (model === 'text-embedding-3-large') {
          return robustEmbeddingGeneration(text, 'text-embedding-3-small');
        }
        throw error;
        
      case 'model_not_available':
        console.log('Model unavailable, using fallback...');
        return robustEmbeddingGeneration(text, 'text-embedding-ada-002');
        
      default:
        console.error('Embedding generation failed:', error.message);
        throw error;
    }
  }
}
```

## Integration Examples

### Vector Database Integration

```javascript
// Example with Pinecone vector database
class PineconeIntegration {
  constructor(apiKey, pineconeConfig) {
    this.apiKey = apiKey;
    this.openai = new OpenAI({
      apiKey: apiKey,
      baseURL: 'https://api.conduit.yourdomain.com/v1'
    });
    this.pinecone = new Pinecone(pineconeConfig);
  }

  async indexDocument(id, text, metadata = {}) {
    // Generate embedding
    const response = await this.openai.embeddings.create({
      model: 'text-embedding-3-large',
      input: text
    });

    const embedding = response.data[0].embedding;

    // Store in Pinecone
    await this.pinecone.upsert([{
      id: id,
      values: embedding,
      metadata: { text, ...metadata }
    }]);

    return id;
  }

  async searchSimilar(query, topK = 10) {
    // Generate query embedding
    const response = await this.openai.embeddings.create({
      model: 'text-embedding-3-large',
      input: query
    });

    const queryEmbedding = response.data[0].embedding;

    // Search Pinecone
    const results = await this.pinecone.query({
      vector: queryEmbedding,
      topK: topK,
      includeMetadata: true
    });

    return results.matches;
  }
}
```

## Next Steps

- **Chat Completions**: Combine embeddings with [chat completions](chat-completions) for RAG
- **Models Endpoint**: Discover available [embedding models](models)  
- **Audio Platform**: Explore [speech-to-text embeddings](../audio/speech-to-text)
- **Integration Examples**: See complete [client patterns](../clients/overview)