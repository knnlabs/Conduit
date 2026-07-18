# Meta AI Model Pricing

**Last Updated**: 2026-07-17
**Source**: Meta Model API Documentation (https://ai.developer.meta.com/docs)

## Overview

Meta opened its Meta Model API to developers on July 9, 2026 (public preview, US-only at launch).
The API is self-serve, OpenAI-compatible (Chat Completions and Responses formats), and new accounts
receive $20 in free credits.

## Pricing

| Model | Input (per 1M tokens) | Output (per 1M tokens) |
|-------|----------------------|------------------------|
| Muse Spark 1.1 (`muse-spark-1.1`) | $1.25 | $4.25 |

## Model Capabilities

**Muse Spark 1.1** — multimodal reasoning model from Meta Superintelligence Labs:
- 1,048,576-token (1M) context window; 131,072 max output tokens per response
- Input modalities: text, image, video, PDF; output: text
- Reasoning is always on and cannot be disabled; reasoning tokens are not returned as text but
  bill as output tokens and count against `max_tokens`
- Tool calling (including parallel tool calls) and structured output
- Search grounding with citations
- Optimized for agentic tool calling, multi-step workflows, coding, and document processing

## API Access

- Base URL: `https://api.meta.ai/v1`
- Authentication: Bearer token (`Authorization: Bearer {MODEL_API_KEY}`)
- Model listing: `GET /v1/models`
- API keys: https://ai.developer.meta.com

## Token Counting

- The tokenizer is not publicly disclosed (no open-source package or published vocabulary).
- Exact counts are available server-side: `POST /v1/responses/input_tokens` (OpenAI-format) or
  `POST /v1/messages/count_tokens` (Anthropic-format) return the input-token count without
  generating a response.
- ConduitLLM uses the LLaMA3 tokenizer type as a client-side estimate.

## Notes

- Consumers get Muse Spark free in "Thinking" mode in the Meta AI app and on meta.ai; the pricing
  above applies to API usage only.
