# ADR 0001: Ship a stateless Responses API subset

- Status: Accepted
- Date: 2026-07-23
- Issue: #1158
- OpenAI contract: `openai/openai-openapi` commit
  `5c044be3bf3a42854e99e34616564eeb2124a317`

## Context

OpenAI's Responses API combines model inference, stored response state, hosted
tools, and a distinct streaming protocol. Conduit already has mature
multi-provider routing, billing, and usage accounting around chat completions,
but it does not have a response-state store or portable implementations of
OpenAI-hosted tools.

Silently accepting stateful or hosted-tool fields while translating them to
chat completions would produce behavior that looks successful but is not
compatible. The first release therefore needs an explicit, enforceable
boundary.

## Decision

Conduit will expose `POST /v1/responses` as a stateless, text-only compatibility
subset translated onto the existing chat-completion execution path for every
provider.

### Supported request surface

The first release supports:

- `model` (required);
- `input` as either a string or an array of text messages with `user`,
  `assistant`, `system`, or `developer` roles;
- `instructions` as a string;
- `store`, which must be explicitly `false`;
- `stream`;
- `max_output_tokens`, `temperature`, and `top_p`;
- string-to-string `metadata`;
- `user`.

String input becomes one user message. Instructions become a developer message
before the input messages. `max_output_tokens` maps to
`max_completion_tokens`. The other sampling fields map directly.

The response uses the OpenAI `response` object shape. Text is emitted as one
completed assistant message containing an `output_text` content part, and chat
usage is converted to Responses usage. The public identifier uses the
`resp_` prefix and is never persisted.

Streaming is supported from the first release and uses named Responses SSE
events. The minimum successful sequence is:

1. `response.created`
2. `response.in_progress`
3. `response.output_item.added`
4. `response.content_part.added`
5. zero or more `response.output_text.delta`
6. `response.output_text.done`
7. `response.content_part.done`
8. `response.output_item.done`
9. `response.completed`

Every event includes an increasing `sequence_number`. The stream does not use
the Chat Completions `[DONE]` sentinel.

### Explicitly unsupported

Requests are rejected with an OpenAI-shaped `400 invalid_request_error` when
they request behavior outside the subset, including:

- omitted or `true` `store` (the upstream default is stateful);
- `previous_response_id` or `conversation`;
- built-in, function, MCP, computer, image-generation, or other tools;
- non-text input parts, item references, tool outputs, or generated items;
- non-string instructions;
- more than one output (`n`);
- audio or image output modalities;
- background execution, truncation, include expansions, reasoning controls,
  text/JSON format controls, prompt caching controls, or service tiers.

Unknown request properties are also rejected. This is intentional: accepting a
new field without implementing its semantics would be silent degradation.

### Provider mapping

All providers use translation onto Conduit's routed chat-completion path in the
first release. Native provider Responses passthrough is deferred because it
would create provider-dependent semantics and bypass established routing,
failover, prompt-caching, and accounting behavior. A future ADR may add native
passthrough after capability discovery can guarantee an equivalent contract.

### Billing and accounting

Responses requests use the same admission reservation, selected-route cost
calculation, usage tracking, and finalization as the translated chat request.
The externally reported operation remains `responses`; the billable token and
provider-cost inputs are those of the underlying chat execution. Streaming
disconnects and provider failures follow the same commit/refund rules as chat
completions.

No storage charge exists because response objects and conversation state are
not retained.

### SDK and WebAdmin

The generated Gateway OpenAPI client includes the endpoint and its typed
contracts. Official Python and JavaScript SDK smoke tests cover non-streaming
and streaming requests with `store: false`. WebAdmin gains no new screen; it
consumes the generated contract and may opt into the endpoint later.

### Conformance

The P1 conformance baseline is extended with `POST /v1/responses`. The subset's
supported fields and response/event shapes are tested against the pinned
OpenAI specification. Contract tests also assert that every unsupported
recognized field produces an explicit error.

## Consequences

Many common text-generation clients can adopt Responses without Conduit
introducing persistence or provider-specific behavior. Clients that rely on
OpenAI's default `store: true`, response retrieval, conversations, multimodal
items, structured output, or tools receive an immediate actionable error.

Stateful responses require a separate design for ownership, encryption,
retention, cleanup, deletion, and billing. Tool support requires a separate
mapping design for Conduit functions and streaming tool events. Neither can be
added by widening DTOs alone.
