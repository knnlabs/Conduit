# Cohere Model Pricing

**Last Updated**: 2025-07-19  
**Source**: Cohere API Pricing Documentation

## Generative Models

### Command A
**Most efficient and performant model specializing in agentic AI, multilingual, and human evaluations for real-life use cases**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard | $2.50 | $10.00 |

### Command R+
**Powerful, scalable large language model (LLM) purpose-built to excel at real-world enterprise use cases**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard | $2.50 | $10.00 |

### Command R
**Generative model optimized for long context tasks such as retrieval-augmented generation (RAG) and using external APIs and tools**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard | $0.15 | $0.60 |

### Command R (Fine-tuned Model)
**Fine-tuned version of Command R with custom training**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) | Training (per 1M tokens) |
|-------------|----------------------|------------------------|--------------------------|
| Standard | $0.30 | $1.20 | $3.00 |

### Command R7B
**Smallest generative model optimized for top-tier speed, efficiency, and quality to build powerful AI applications**

| Pricing Type | Input (per 1M tokens) | Output (per 1M tokens) |
|-------------|----------------------|------------------------|
| Standard | $0.0375 | $0.15 |

## Retrieval Models

### Rerank 3.5
**Provides a powerful semantic boost to the search quality of any keyword or vector search system without requiring any overhaul or replacement**

| Model | Cost |
|-------|------|
| Rerank 3.5 | $2.00 per 1K searches |

**Note**: A single search unit is a query with up to 100 documents to be ranked. Documents longer than 500 tokens when including the length of the search query will be split up into multiple chunks, where each chunk counts as a singular document.

## Embedding Models

### Embed 4
**Latest embedding model for high-quality text embeddings**

| Pricing Type | Text Cost (per 1M tokens) | Image Cost (per 1M image tokens) |
|-------------|---------------------------|----------------------------------|
| Standard | $0.12 | $0.47 |

## Notes

1. **Pricing Currency**: All prices are in USD
2. **Token Counting**: Varies by language and model
3. **Model Versions**: 
   - The pricing above is applicable to the most recent versions of the Command R series of models
   - Command R7B, Command R 08-2024, Command R+ 08-2024
   - Previous versions (Command R 03-2024 and Command R+ 04-2024) are charged differently for input and output tokens
4. **Fine-tuning**: Available for Command R model with additional training costs
5. **Rerank Pricing**: Based on search units, not tokens
6. **Legacy Models**: Pricing for legacy models (Rerank 2, Command Light, and Classify) available through FAQ

## CSV Import Format

When importing to Conduit, use the following CSV columns:
- Model Pattern (e.g., "command-a", "command-r-plus", "command-r")
- Provider (always "Cohere")
- Model Type (chat/embedding/rerank)
- Input Cost (per 1K tokens) - divide the per-1M price by 1000
- Output Cost (per 1K tokens) - divide the per-1M price by 1000
- Embedding Cost (per 1K tokens) - for embedding models
- Rerank Cost (per search) - for rerank models
- Training Cost (per 1K tokens) - for fine-tuned models
- Priority (typically 10)
- Active (Yes/No)
- Description

Note: For Rerank models, the cost is per search unit (not per token), which should be handled separately in the import process.