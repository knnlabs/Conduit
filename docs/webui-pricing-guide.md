# WebUI Pricing Configuration Guide

## Quick Start

This guide helps you configure pricing for AI models using the Conduit WebUI.

## Accessing Model Pricing

1. Navigate to **Settings** → **Model Costs** in the WebUI
2. Click **Add Pricing** to create new pricing configuration
3. Or click the menu (⋮) next to existing pricing to edit

## Creating a New Pricing Configuration

### Step 1: Basic Information

![Basic Setup](./images/pricing-basic.png)

1. **Cost Name**: Enter a descriptive name (e.g., "GPT-4 Turbo Standard Pricing")
2. **Model Selection**: Choose which models this pricing applies to
3. **Model Type**: Select the type (Chat, Embedding, Image, Audio, Video)

### Step 2: Choose Pricing Model

The system supports 8 different pricing models:

#### Standard (Per Token) - Most Common
Best for: OpenAI, Anthropic, Google models

**When to use**: Traditional LLM pricing based on tokens

**Fields to configure**:
- Input Cost (per million tokens)
- Output Cost (per million tokens)
- Cached Input Cost (optional)
- Embedding Cost (for embedding models)

**Example**:
```
Input Cost: $10.00 per million tokens
Output Cost: $30.00 per million tokens
```

---

#### Per Video (Flat Rate)
Best for: MiniMax video generation

**When to use**: Fixed prices for specific video configurations

**Configuration format**:
```json
{
  "rates": {
    "512p_6": 0.10,   // 512p resolution, 6 seconds = $0.10
    "768p_6": 0.28,   // 768p resolution, 6 seconds = $0.28
    "1080p_6": 0.49   // 1080p resolution, 6 seconds = $0.49
  }
}
```

**Important**: Must match exact resolution_duration format

---

#### Per Second Video
Best for: Replicate video models

**When to use**: Video charged by duration with resolution multipliers

**Configuration format**:
```json
{
  "baseRate": 0.09,  // $0.09 per second base
  "resolutionMultipliers": {
    "480p": 0.5,     // 480p costs 50% of base
    "720p": 1.0,     // 720p costs 100% of base
    "1080p": 1.5     // 1080p costs 150% of base
  }
}
```

---

#### Inference Steps
Best for: Fireworks AI, image generation models

**When to use**: Models that charge per denoising/inference step

**Configuration format**:
```json
{
  "costPerStep": 0.00035,  // $0.00035 per step
  "defaultSteps": 20,      // Default if not specified
  "modelSteps": {
    "model-fast": 10,      // Fast model uses 10 steps
    "model-quality": 30    // Quality model uses 30 steps
  }
}
```

---

#### Tiered Tokens
Best for: Models with context-based pricing

**When to use**: Different rates based on total context length

**Configuration format**:
```json
{
  "tiers": [
    {
      "maxContext": 200000,    // Up to 200k tokens
      "inputCost": 400,        // $0.40 per million
      "outputCost": 2200       // $2.20 per million
    },
    {
      "maxContext": null,      // Above 200k tokens
      "inputCost": 1300,       // $1.30 per million
      "outputCost": 2200       // $2.20 per million
    }
  ]
}
```

---

#### Per Image
Best for: DALL-E style image generation

**When to use**: Simple per-image pricing with multipliers

**Configuration format**:
```json
{
  "baseRate": 0.04,  // $0.04 base price
  "qualityMultipliers": {
    "standard": 1.0,  // Standard quality = 100% of base
    "hd": 1.5        // HD quality = 150% of base
  },
  "resolutionMultipliers": {
    "1024x1024": 1.0,  // 1024x1024 = 100% of base
    "1792x1024": 1.5   // 1792x1024 = 150% of base
  }
}
```

---

#### Per Minute Audio
Best for: Speech synthesis, transcription

**When to use**: Audio services charged by duration

**Configuration format**:
```json
{
  "ratePerMinute": 0.15  // $0.15 per minute
}
```

---

#### Per Thousand Characters
Best for: Text-to-speech services

**When to use**: TTS charged by character count

**Configuration format**:
```json
{
  "ratePerThousand": 0.015  // $0.015 per 1000 characters
}
```

### Step 3: Configure Additional Settings

#### Batch Processing
Enable if the provider offers batch API discounts:
- Toggle "Supports Batch Processing"
- Set multiplier (e.g., 0.5 = 50% discount)

#### Priority
Higher priority configurations are selected first when multiple match:
- Default: 0
- Higher numbers = higher priority

#### Status
- **Active**: Configuration is used for cost calculations
- **Inactive**: Configuration is ignored (useful for testing)

## Editing Existing Pricing

1. Find the pricing configuration in the table
2. Click the menu (⋮) → **Edit**
3. The edit modal shows the current configuration
4. Make changes and click **Save Changes**

### What You Can Edit
✅ Cost values and rates
✅ Pricing model and configuration
✅ Model associations
✅ Batch processing settings
✅ Priority and status

### What You Cannot Edit
❌ Model Type (create new configuration instead)
❌ Historical calculations (already processed)

## Common Scenarios

### Scenario 1: Adding OpenAI GPT-4 Pricing

1. Click **Add Pricing**
2. Enter name: "GPT-4 Turbo"
3. Select models: gpt-4-turbo-preview, gpt-4-1106-preview
4. Model Type: Chat
5. Pricing Model: Standard (Per Token)
6. Input Cost: 10.00 (per million tokens)
7. Output Cost: 30.00 (per million tokens)
8. Enable batch processing with 0.5 multiplier
9. Click **Create Pricing**

### Scenario 2: Adding MiniMax Video Pricing

1. Click **Add Pricing**
2. Enter name: "MiniMax Video"
3. Select models: video-01, video-01-turbo
4. Model Type: Video
5. Pricing Model: Per Video (Flat Rate)
6. Enter configuration:
```json
{
  "rates": {
    "512p_6": 0.10,
    "768p_6": 0.28,
    "1080p_6": 0.49
  }
}
```
7. Click **Create Pricing**

### Scenario 3: Migrating from Old System

If you have existing costs without pricing models:

1. Edit each existing cost
2. It will default to "Standard (Per Token)"
3. Verify the token costs are correct
4. Save the configuration

## Understanding the Pricing Table

The table shows:
- **Cost Name**: Your descriptive name
- **Model Aliases**: Which models use this pricing
- **Providers**: Associated AI providers
- **Type**: Model type (Chat, Image, etc.)
- **Pricing Model**: Which calculation method (badge shows type)
- **Pricing**: Current rates
- **Batch**: Batch discount if enabled
- **Priority**: Selection priority
- **Status**: Active/Inactive

### Visual Indicators

Badges and icons help identify pricing types:
- 🔵 **Standard**: Default per-token pricing
- 🟣 **Per Video**: Flat rate video pricing
- 🔷 **Per Second**: Duration-based video
- 🟢 **Steps**: Inference step pricing
- 🟠 **Tiered**: Context-based tiers
- 🌸 **Per Image**: Image generation
- 🔷 **Per Minute**: Audio duration
- 🍃 **Per 1K Chars**: Character-based

## Best Practices

### DO ✅
- **Test configurations** with small amounts first
- **Use descriptive names** that include the provider and model
- **Document special configurations** in the description field
- **Set appropriate priorities** when multiple configs might match
- **Keep inactive configs** for testing before going live
- **Review JSON carefully** for non-standard pricing models

### DON'T ❌
- Don't delete active configurations without a replacement
- Don't use negative values or zero costs (unless intended)
- Don't forget to enable configurations after testing
- Don't mix token costs (per-token vs per-million-tokens)
- Don't ignore validation errors in JSON configurations

## Troubleshooting

### "Invalid JSON format"
Your pricing configuration has a JSON syntax error. Check for:
- Missing quotes around keys
- Trailing commas
- Unmatched brackets

Use a JSON validator: https://jsonlint.com

### "Configuration is required for this pricing model"
Non-standard pricing models need a configuration. Make sure to:
1. Select a pricing model first
2. Enter the required JSON configuration
3. Check the format matches the examples

### "At least one model must be selected"
Every pricing configuration needs associated models:
1. Click the model selector
2. Choose at least one model
3. You can add more models later

### Costs showing as $0
Check:
1. Is the configuration active?
2. Does the usage data match the pricing model?
3. Is the JSON configuration valid?
4. Are the model associations correct?

### Can't find my model
Make sure:
1. The model is registered in Model Mappings
2. The provider is configured
3. The model alias matches exactly

## Tips and Tricks

### Quick Duplication
To create similar pricing:
1. Find an existing configuration
2. Note its settings
3. Create new with similar values
4. Adjust as needed

### Testing New Pricing
1. Create configuration as "Inactive"
2. Test with sample calculations
3. Verify costs are correct
4. Set to "Active" when ready

### Bulk Updates
For multiple similar models:
1. Select all relevant models at once
2. Apply the same pricing configuration
3. Adjust individual models later if needed

### Organizing Configurations
Use naming conventions:
- Provider name first: "OpenAI - GPT-4"
- Include pricing type: "Anthropic Claude (Batch)"
- Add date for versions: "Gemini Pro (2024-01)"

## FAQ

**Q: Can I have multiple pricing configurations for the same model?**
A: Yes, use the Priority field to determine which one is selected. Higher priority wins.

**Q: How do I handle price changes?**
A: Edit the existing configuration. Changes apply to future calculations only.

**Q: What happens to old calculations when I change pricing?**
A: Historical calculations are preserved. Only new usage is affected.

**Q: Can I import/export pricing configurations?**
A: Yes, use the Export CSV and Import CSV buttons on the main page.

**Q: How do I disable a pricing temporarily?**
A: Set the Status to "Inactive" rather than deleting it.

**Q: What's the difference between cached and regular token costs?**
A: Cached tokens (prompt caching) are previously processed tokens offered at a discount by some providers.

## Need Help?

- Check the [main documentation](./polymorphic-pricing.md)
- Report issues on [GitHub](https://github.com/knnlabs/Conduit/issues)
- Review the [API documentation](./api-reference.md) for programmatic access