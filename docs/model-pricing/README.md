# Model Pricing

Conduit tracks costs for every LLM request using a flexible pricing system that supports per-token, per-image, per-video, tiered, and other billing models.

## Managing Model Data & Pricing

Model definitions and pricing are managed via SQL scripts generated from JSON configuration files:

- **[Provider Models SQL Generator](../../scripts/db/providers/README.md)** — Canonical tool for adding/updating models and costs
- **[Updating Models Guide](../../scripts/db/providers/UPDATING-MODELS.md)** — Step-by-step guide for updating provider model JSON files

Supported providers: Cerebras, Groq, SambaNova, OpenRouter (300+ models via API fetch), and Replicate.

## Understanding the Pricing System

- **[Model Cost Configuration](./model-costs.md)** — How ModelCost, ModelCostMapping, and cost storage work
- **[Polymorphic Pricing](./polymorphic-pricing.md)** — Architecture for the 8 pricing models (standard, per-video, tiered, per-image, etc.)
- **[Pricing Quick Reference](./pricing-quick-reference.md)** — Enum values and configuration templates
- **[WebAdmin Pricing Guide](./webui-pricing-guide.md)** — Configuring pricing through the WebAdmin UI
