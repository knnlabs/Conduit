# ADR 0002: Registry-driven structured provider settings

- Status: Proposed
- Date: 2026-07-23
- Epic: #1177

## Context

Some providers require **structured, provider-scoping identifiers in addition to
the API key**. Conduit models only a free-text `Provider.BaseUrl`
(`Shared/ConduitLLM.Configuration/Entities/Provider.cs`) plus an unused
per-key `Organization`, so operators are expected to encode those identifiers by
hand-crafting a URL. That approach is already failing in three concrete ways:

- **Cloudflare Workers AI** needs an **account ID** in the URL path. The default
  base URL is a template,
  `https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1`
  (`ProviderAdapterDefaultsRegistry.cs`), and nothing substitutes
  `{account_id}`. When the endpoint field is left blank, `ResolveBaseUrl`
  returns the literal template, the request is sent to
  `.../accounts/%7Baccount_id%7D/ai/v1/models`, and Cloudflare answers `405`.
  WebAdmin has no Account ID field, so there is no correct way to supply it
  except knowing Cloudflare's URL grammar.
- **OpenAI `Organization`** is stored on `ProviderKeyCredential` and collected by
  the form, but **no provider client reads it** — it is silently dropped.
- **Azure OpenAI** needs a resource endpoint, a deployment name, and an
  api-version. It has no `ProviderType`; `OpenAIClient` detects it by matching
  the string `"azure"` and overloads `BaseUrl`, the model ID, and a hardcoded
  api-version constant.

The pattern recurs for providers we are likely to add (AWS Bedrock: region plus
two secrets; Google Vertex: project plus location plus a service-account
credential).

Two independent problems compound this. First, `BaseUrl` **structurally cannot
express** identifiers that are not URL components — an OpenAI organization is a
header, a Bedrock region is part of request signing. Second, provider
configuration is described in two hand-mirrored places, `ProviderConfigurationRegistry`
(C#) and `PROVIDER_CONFIG_REQUIREMENTS` (TypeScript), which drift.

## Decision

Introduce **registry-driven structured provider settings**.

### Settings schema (single source of truth)

Each `ProviderType` declares its settings as data in the C# provider registry.
Every setting has:

- `key` (stable identifier, e.g. `account_id`);
- `label`, `helpText`;
- `required` and, where useful, a `validationRegex`;
- `secret` (whether the value is sensitive);
- a **binding** that states how the value is applied at request time, one of:
  `UrlPathToken` (fills a `{token}` in the base URL), `Header`, `QueryParam`, or
  `AuthScope`.

This declaration is the one source consumed by validation, the request-time
resolver, and the WebAdmin form. WebAdmin renders its fields from the schema
(served over the Admin API or generated), so the C#/TypeScript mirror is
retired.

### Storage

Non-secret setting values are stored in a new nullable **JSONB `Settings`**
column on `Provider` (a string-to-string map). Secret-valued settings are **not**
placed in this bag; they remain in the credential store with the same protection
as the API key. Phase 1 covers non-secret settings only.

### Resolution

A resolver composes the effective base URL, headers, query parameters, and auth
from `Settings` against the registry, replacing the raw pass-through in
`ResolveBaseUrl`. If a resolved URL still contains an unsubstituted `{token}`,
the resolver raises an explicit configuration error instead of issuing the
request. Test Connection surfaces missing required settings as actionable
messages, and `405` is classified rather than reported as an unknown error.

### Migration

Following the expand/contract policy (ADR-002), the `Settings` column is added
nullable, existing Cloudflare account IDs are backfilled by parsing current
`BaseUrl` values, and the resolver reads `Settings` first while falling back to
raw `BaseUrl` during a dual-read window. Removing the `BaseUrl` fallback is a
later contract step.

### Phasing

1. Mechanism plus Cloudflare `account_id` end-to-end, dynamic WebAdmin fields,
   backfill, and the improved Test Connection errors. This resolves the live bug.
2. Bind OpenAI `organization` and add `project` as header settings; retire the
   orphaned field.
3. Promote Azure OpenAI to a first-class `ProviderType` with resource,
   deployment, and api-version settings.
4. Multi-secret credentials for Bedrock and Vertex.

Issue breakdown: #1178–#1187 under epic #1177.

## Consequences

Cloudflare is configured by entering an API token and a labeled, validated
Account ID; no operator needs to know the URL grammar, and a blank or malformed
identifier produces an actionable error rather than a `405`. Structured
identifiers gain a real home, so the OpenAI organization is actually sent and
Azure loses its string-matching special cases. New providers add settings as
data instead of new bespoke columns or client-specific hacks, and the settings
schema drives both backend validation and the WebAdmin form from one definition.

Costs and boundaries: JSONB values are less strongly typed than columns, so the
registry's `validationRegex` and required-ness carry validation weight. Secret
material is deliberately excluded from the `Settings` bag; multi-secret
providers (Phase 4) require a separate credential design and are not solved by
this ADR. During the dual-read window both `Settings` and raw `BaseUrl` are
honored for Cloudflare, and the contract step that removes the fallback ships in
a later release.
