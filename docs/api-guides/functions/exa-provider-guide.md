# Exa.ai Provider Configuration Guide

This guide covers setting up and using Exa.ai as a function provider in Conduit.

## Table of Contents
1. [Overview](#overview)
2. [Getting Started](#getting-started)
3. [Search Types](#search-types)
4. [Pricing Configuration](#pricing-configuration)
5. [API Parameters](#api-parameters)
6. [Usage Examples](#usage-examples)
7. [Cost Optimization](#cost-optimization)

## Overview

Exa.ai provides neural and keyword search capabilities for finding relevant web content. Unlike traditional search engines, Exa is optimized for:

- **AI-powered search** with semantic understanding
- **Content extraction** with text, highlights, and summaries
- **Date filtering** for recent/historical content
- **Domain filtering** for specific sources

### Exa Capabilities

- **Neural Search:** Uses AI to understand query intent and meaning
- **Keyword Search:** Traditional boolean keyword matching (cheaper)
- **Autoprompt:** Automatically optimizes queries for better results
- **Content Extraction:** Text, highlights, and AI-generated summaries
- **Find Similar:** Discover content similar to a given URL

## Getting Started

### 1. Get Exa API Key

Sign up at [exa.ai](https://exa.ai) and get your API key.

### 2. Create Function Configuration

```typescript
import { FetchConduitAdminClient, FunctionProviderType, FunctionPurpose } from '@knn_labs/conduit-admin-client';

const adminClient = new FetchConduitAdminClient({
  baseUrl: process.env.CONDUIT_ADMIN_URL,
  masterKey: process.env.CONDUIT_MASTER_KEY
});

const config = await adminClient.functionConfigurations.create({
  configurationName: 'Exa Production Search',
  providerType: FunctionProviderType.Exa,
  purpose: FunctionPurpose.Search,
  description: 'Exa neural search for production',
  timeoutSeconds: 30,
  isEnabled: true
});
```

### 3. Add API Credential

```typescript
const credential = await adminClient.functionCredentials.create({
  functionConfigurationId: config.id,
  credentialName: 'Exa Production Key',
  apiKey: process.env.EXA_API_KEY,
  isPrimary: true,
  isEnabled: true
});

// Test credential
const test = await adminClient.functionCredentials.testCredential({
  credentialId: credential.id
});

if (test.success) {
  console.log('✓ Exa credential working');
} else {
  console.error('✗ Credential test failed:', test.message);
}
```

### 4. Configure Pricing

See [Pricing Configuration](#pricing-configuration) section below.

## Search Types

### Neural Search

AI-powered semantic search that understands query intent.

**When to use:**
- Finding conceptually related content
- Questions and research queries
- Broad topic exploration

**Characteristics:**
- Higher cost per result
- Better quality for semantic queries
- Autoprompt support

```typescript
const result = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'how does quantum computing work',
    numResults: 10,
    searchType: 'neural',
    useAutoprompt: true
  }
});
```

### Keyword Search

Traditional boolean keyword matching.

**When to use:**
- Exact phrase matching
- Technical terms or codes
- Site-specific searches
- Cost-sensitive queries

**Characteristics:**
- Lower cost per result (~4x cheaper than neural)
- Precise matching
- Boolean operators supported

```typescript
const result = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'site:github.com "typescript" AND "react"',
    numResults: 20,
    searchType: 'keyword'
  }
});
```

### Auto Search Type

Let Conduit choose based on query:

```typescript
const result = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'best practices for API design',
    numResults: 10,
    searchType: 'auto' // Chooses neural/keyword automatically
  }
});
```

## Pricing Configuration

Exa uses tiered hybrid pricing based on:
1. Search type (neural vs keyword)
2. Number of results
3. Content extraction options

### Current Exa Pricing (2024)

**Neural Search:**
- 1-25 results: $0.002 per result
- 26+ results: $0.001 per result

**Keyword Search:**
- 1-25 results: $0.0005 per result
- 26+ results: $0.0002 per result

**Content Extraction (per page):**
- Text: $0.001
- Highlights: $0.002
- Summary: $0.003

### Configure in Conduit

```typescript
const exaCost = await adminClient.functionCosts.create({
  costName: 'Exa Standard Pricing 2024',
  providerType: FunctionProviderType.Exa,
  purpose: FunctionPurpose.Search,
  pricingModel: FunctionPricingModel.Hybrid,
  pricingConfiguration: JSON.stringify({
    pricingModel: 6,
    neuralSearchCosts: {
      tier1: {
        minResults: 1,
        maxResults: 25,
        costPerResult: 0.002
      },
      tier2: {
        minResults: 26,
        costPerResult: 0.001
      }
    },
    keywordSearchCosts: {
      tier1: {
        minResults: 1,
        maxResults: 25,
        costPerResult: 0.0005
      },
      tier2: {
        minResults: 26,
        costPerResult: 0.0002
      }
    },
    contentExtractionCosts: {
      text: 0.001,
      highlights: 0.002,
      summary: 0.003
    }
  }),
  isActive: true,
  priority: 10,
  effectiveDate: new Date().toISOString(),
  description: 'Exa official pricing - updated Jan 2024'
});
```

### Cost Calculation Example

**Query:** 30 neural search results with text extraction on 10 pages

```
Neural Search:
  - Tier 1 (1-25):  25 × $0.002 = $0.050
  - Tier 2 (26-30):  5 × $0.001 = $0.005
  Search Total: $0.055

Content Extraction:
  - Text: 10 pages × $0.001 = $0.010
  Content Total: $0.010

Total Cost: $0.065
```

## API Parameters

### Basic Parameters

```typescript
interface ExaSearchParams {
  // Required
  query: string;                    // Search query
  numResults?: number;              // 1-100, default: 10

  // Search Type
  searchType?: 'neural' | 'keyword' | 'auto'; // default: 'neural'
  useAutoprompt?: boolean;          // Optimize query (neural only), default: false

  // Filters
  includeDomains?: string[];        // Whitelist domains
  excludeDomains?: string[];        // Blacklist domains
  startPublishedDate?: string;      // ISO 8601 date
  endPublishedDate?: string;        // ISO 8601 date

  // Content
  contents?: {
    text?: boolean;                 // Extract full text
    highlights?: boolean;           // Extract highlights
    summary?: boolean;              // Generate AI summary
  };

  // Advanced
  category?: string;                // Content category filter
  livecrawl?: 'always' | 'fallback' | 'never'; // Crawl mode
}
```

### Content Extraction

```typescript
// No content (just URLs and titles)
{
  query: 'AI news',
  numResults: 10
}

// With full text
{
  query: 'AI breakthroughs 2024',
  numResults: 5,
  contents: {
    text: true
  }
}

// With highlights (key passages)
{
  query: 'machine learning best practices',
  numResults: 10,
  contents: {
    highlights: true
  }
}

// With AI summary
{
  query: 'quantum computing explained',
  numResults: 3,
  contents: {
    summary: true
  }
}

// All content types
{
  query: 'deep learning papers',
  numResults: 5,
  contents: {
    text: true,
    highlights: true,
    summary: true
  }
}
```

### Domain Filtering

```typescript
// Only search GitHub
{
  query: 'react hooks',
  numResults: 20,
  includeDomains: ['github.com']
}

// Search news sites
{
  query: 'AI regulation',
  numResults: 15,
  includeDomains: [
    'techcrunch.com',
    'theverge.com',
    'arstechnica.com'
  ]
}

// Exclude specific domains
{
  query: 'python tutorials',
  numResults: 20,
  excludeDomains: ['stackoverflow.com']
}
```

### Date Filtering

```typescript
// Recent content only (last 30 days)
{
  query: 'AI news',
  numResults: 20,
  startPublishedDate: new Date(Date.now() - 30*24*60*60*1000).toISOString()
}

// Specific date range
{
  query: 'COVID-19 research',
  numResults: 50,
  startPublishedDate: '2020-01-01T00:00:00.000Z',
  endPublishedDate: '2020-12-31T23:59:59.999Z'
}

// Historical content before date
{
  query: 'pre-transformer NLP',
  numResults: 10,
  endPublishedDate: '2017-01-01T00:00:00.000Z'
}
```

## Usage Examples

### Example 1: Research Query

Find recent academic papers on a topic:

```typescript
const research = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'transformer architecture improvements',
    numResults: 20,
    searchType: 'neural',
    useAutoprompt: true,
    includeDomains: ['arxiv.org', 'aclweb.org', 'openreview.net'],
    startPublishedDate: '2023-01-01T00:00:00.000Z',
    contents: {
      text: true,
      summary: true
    }
  }
});

// Process results
for (const result of research.result.results) {
  console.log('Title:', result.title);
  console.log('URL:', result.url);
  console.log('Published:', result.publishedDate);
  console.log('Summary:', result.summary);
  console.log('---');
}

console.log('Cost: $' + research.actualCost.toFixed(6));
```

### Example 2: News Monitoring

Track news on specific topics:

```typescript
const news = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'AI regulation policy',
    numResults: 30,
    searchType: 'neural',
    includeDomains: [
      'reuters.com',
      'apnews.com',
      'bbc.com',
      'theguardian.com'
    ],
    startPublishedDate: new Date(Date.now() - 7*24*60*60*1000).toISOString(), // Last 7 days
    contents: {
      highlights: true
    }
  }
});

// Group by date
const byDate = news.result.results.reduce((acc, r) => {
  const date = r.publishedDate.split('T')[0];
  if (!acc[date]) acc[date] = [];
  acc[date].push(r);
  return acc;
}, {});

console.log('News by date:', byDate);
```

### Example 3: Technical Documentation Search

Find specific technical docs:

```typescript
const docs = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'site:docs.microsoft.com azure functions typescript',
    numResults: 15,
    searchType: 'keyword', // Cheaper for exact matches
    contents: {
      text: true,
      highlights: true
    }
  }
});

// Extract code examples from highlights
for (const result of docs.result.results) {
  const codeBlocks = result.highlights.filter(h =>
    h.includes('```') || h.includes('const ') || h.includes('function ')
  );

  if (codeBlocks.length > 0) {
    console.log('Code examples from:', result.url);
    console.log(codeBlocks);
  }
}
```

### Example 4: Competitor Analysis

Monitor competitor mentions:

```typescript
const mentions = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: '"CompanyX" AND ("new product" OR "announcement" OR "release")',
    numResults: 50,
    searchType: 'keyword',
    startPublishedDate: new Date(Date.now() - 30*24*60*60*1000).toISOString(),
    contents: {
      highlights: true
    }
  }
});

console.log('Found', mentions.result.results.length, 'mentions');
console.log('Cost: $' + mentions.actualCost.toFixed(6));
```

### Example 5: Batch Processing

Process multiple queries efficiently:

```typescript
const queries = [
  'AI safety research',
  'machine learning interpretability',
  'neural network pruning',
  'few-shot learning'
];

const results = await Promise.all(
  queries.map(query =>
    coreClient.functions.execute({
      functionConfigurationId: config.id,
      parameters: {
        query,
        numResults: 10,
        searchType: 'neural',
        startPublishedDate: '2023-01-01T00:00:00.000Z',
        contents: { highlights: true }
      }
    })
  )
);

const totalCost = results.reduce((sum, r) => sum + (r.actualCost ?? 0), 0);
const totalResults = results.reduce((sum, r) => sum + r.result.results.length, 0);

console.log('Total queries:', queries.length);
console.log('Total results:', totalResults);
console.log('Total cost:', totalCost);
console.log('Average cost per query:', totalCost / queries.length);
```

## Cost Optimization

### 1. Choose Appropriate Search Type

```typescript
// Use keyword for exact matches (4x cheaper)
const exact = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'site:github.com "error TS2345"',
    numResults: 10,
    searchType: 'keyword' // Cheaper
  }
});

// Use neural for semantic understanding
const semantic = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'how to debug TypeScript type errors',
    numResults: 10,
    searchType: 'neural' // More expensive but better quality
  }
});
```

### 2. Request Only Needed Content

```typescript
// Expensive: Full content extraction
const expensive = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'AI news',
    numResults: 10,
    contents: {
      text: true,      // +$0.001 per page
      highlights: true, // +$0.002 per page
      summary: true     // +$0.003 per page
    }
  }
});
// Cost: ~$0.080 (search + content)

// Cheaper: Just URLs and titles
const cheaper = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'AI news',
    numResults: 10
    // No content extraction
  }
});
// Cost: ~$0.020 (search only)
```

### 3. Optimize Result Count

```typescript
// Stay in tier 1 for lower cost per result
const tier1 = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'AI research',
    numResults: 25, // Max tier 1
    searchType: 'neural'
  }
});
// Cost: 25 × $0.002 = $0.050

// Tier 2 has better per-result pricing
const tier2 = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'AI research',
    numResults: 50,
    searchType: 'neural'
  }
});
// Cost: (25 × $0.002) + (25 × $0.001) = $0.075
```

### 4. Use Domain Filters

Narrow search scope to reduce irrelevant results:

```typescript
// Broad search (may need more results to find relevant ones)
const broad = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'react hooks tutorial',
    numResults: 50 // Need more to find quality results
  }
});

// Targeted search (quality results with fewer items)
const targeted = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'react hooks tutorial',
    numResults: 10, // Fewer results needed
    includeDomains: ['reactjs.org', 'github.com', 'dev.to']
  }
});
```

### 5. Cache Results

Implement client-side caching for repeated queries:

```typescript
const cache = new Map();

async function searchWithCache(query, params) {
  const cacheKey = JSON.stringify({ query, ...params });

  if (cache.has(cacheKey)) {
    console.log('Cache hit - no cost');
    return cache.get(cacheKey);
  }

  const result = await coreClient.functions.execute({
    functionConfigurationId: config.id,
    parameters: { query, ...params }
  });

  cache.set(cacheKey, result);
  return result;
}
```

## Best Practices

### 1. Test Queries in Development

Use separate dev configuration with logging:

```typescript
const devConfig = await adminClient.functionConfigurations.create({
  configurationName: 'Exa Development',
  providerType: FunctionProviderType.Exa,
  purpose: FunctionPurpose.Search,
  timeoutSeconds: 60,
  metadata: JSON.stringify({ environment: 'development' })
});

// Test different search types
const tests = [
  { searchType: 'neural', useAutoprompt: false },
  { searchType: 'neural', useAutoprompt: true },
  { searchType: 'keyword' }
];

for (const test of tests) {
  const result = await coreClient.functions.execute({
    functionConfigurationId: devConfig.id,
    parameters: {
      query: 'test query',
      numResults: 5,
      ...test
    }
  });

  console.log('Test:', test);
  console.log('Cost:', result.actualCost);
  console.log('Results:', result.result.results.length);
}
```

### 2. Monitor Costs

Track spending by query type:

```typescript
// Daily cost report
const executions = await adminClient.functionExecutions.getByConfiguration(
  config.id,
  { pageSize: 1000 }
);

const today = executions.items.filter(e =>
  new Date(e.createdAt) > new Date(Date.now() - 86400000)
);

const bySearchType = today.reduce((acc, e) => {
  const params = JSON.parse(e.requestJson);
  const type = params.searchType || 'neural';

  if (!acc[type]) acc[type] = { count: 0, cost: 0 };
  acc[type].count++;
  acc[type].cost += e.actualCost ?? 0;

  return acc;
}, {});

console.log('Search type costs (24h):', bySearchType);
```

### 3. Set Budget Alerts

Implement cost limits:

```typescript
const DAILY_BUDGET = 5.00; // $5 per day

async function executeWithBudgetCheck(params) {
  const executions = await adminClient.functionExecutions.getByConfiguration(
    config.id,
    { pageSize: 1000 }
  );

  const todaySpend = executions.items
    .filter(e => new Date(e.createdAt) > new Date(Date.now() - 86400000))
    .reduce((sum, e) => sum + (e.actualCost ?? 0), 0);

  if (todaySpend >= DAILY_BUDGET) {
    throw new Error(`Daily budget exceeded: $${todaySpend.toFixed(2)}`);
  }

  return await coreClient.functions.execute(params);
}
```

## Troubleshooting

### No Results Returned

- Try different search types (neural vs keyword)
- Remove strict domain filters
- Broaden date range
- Use autoprompt for neural searches

### High Costs

- Review content extraction settings
- Check if keyword search would work
- Reduce numResults
- Add domain filters to improve relevance

### Poor Result Quality

- Use neural search for semantic queries
- Enable autoprompt
- Add domain filters for reputable sources
- Increase numResults and filter client-side

## Resources

- [Exa.ai Documentation](https://docs.exa.ai)
- [Functions API Reference](./getting-started.md)
- [Cost Optimization Guide](../../../WebAdmin/docs/admin/routing/examples/cost-optimization.md)
- [WebAdmin Functions Management](../../../WebAdmin/docs/README.md)
