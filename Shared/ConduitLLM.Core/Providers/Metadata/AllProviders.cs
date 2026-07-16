using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Providers.Metadata
{
    // TODO: Provider metadata, including default base URLs and capabilities,
    // should eventually be migrated to database configuration for full
    // data-driven architecture. These classes provide reasonable defaults
    // but should not be the source of truth for provider configuration.



    /// <summary>
    /// Provider metadata for Groq.
    /// </summary>
    public class GroqProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Groq;
        public override string DisplayName => "Groq";
        public override string DefaultBaseUrl => "https://api.groq.com/openai/v1";

        public GroqProviderMetadata()
        {
            Capabilities.ChatParameters.Tools = true;
            ConfigurationHints.DocumentationUrl = "https://console.groq.com/docs";
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Ultra-Fast Inference",
                Description = "Groq specializes in extremely fast inference speeds",
                Severity = TipSeverity.Info
            });
        }
    }


    /// <summary>
    /// Provider metadata for Replicate.
    /// </summary>
    public class ReplicateProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Replicate;
        public override string DisplayName => "Replicate";
        public override string DefaultBaseUrl => "https://api.replicate.com/v1";

        public ReplicateProviderMetadata()
        {
            Capabilities.Features.ImageGeneration = true;
            Capabilities.Features.VisionInput = true;
            
            AuthRequirements.ApiKeyHeaderName = "Authorization";
            ConfigurationHints.DocumentationUrl = "https://replicate.com/docs";
        }
    }

    /// <summary>
    /// Provider metadata for Fireworks AI.
    /// </summary>
    public class FireworksProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Fireworks;
        public override string DisplayName => "Fireworks AI";
        public override string DefaultBaseUrl => "https://api.fireworks.ai/inference/v1";

        public FireworksProviderMetadata()
        {
            Capabilities.ChatParameters.Tools = true;
            ConfigurationHints.DocumentationUrl = "https://readme.fireworks.ai/";
        }
    }





    /// <summary>
    /// Provider metadata for OpenAI-compatible endpoints.
    /// </summary>
    public class OpenAICompatibleProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.OpenAICompatible;
        public override string DisplayName => "OpenAI Compatible";
        public override string DefaultBaseUrl => "http://localhost:8080/v1";

        public OpenAICompatibleProviderMetadata()
        {
            AuthRequirements.CustomFields = new List<AuthField>
            {
                CreateUrlField("baseUrl", "API Base URL", true, "The base URL of your OpenAI-compatible endpoint")
            };
            
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Compatibility",
                Description = "Use this for any service that implements the OpenAI API specification",
                Severity = TipSeverity.Info
            });
        }
    }

    /// <summary>
    /// Provider metadata for MiniMax.
    /// </summary>
    public class MiniMaxProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.MiniMax;
        public override string DisplayName => "MiniMax";
        public override string DefaultBaseUrl => "https://api.minimax.chat/v1";

        public MiniMaxProviderMetadata()
        {
            
            ConfigurationHints.DocumentationUrl = "https://api.minimax.chat/document/introduction";
        }
    }



    /// <summary>
    /// Provider metadata for Cerebras.
    /// </summary>
    public class CerebrasProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Cerebras;
        public override string DisplayName => "Cerebras";
        public override string DefaultBaseUrl => "https://api.cerebras.ai/v1";

        public CerebrasProviderMetadata()
        {
            Capabilities.ChatParameters.Tools = true;
            
            ConfigurationHints.DocumentationUrl = "https://inference-docs.cerebras.ai/";
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "High-Performance Inference",
                Description = "Cerebras offers extremely fast inference for supported models",
                Severity = TipSeverity.Info
            });
        }
    }

    /// <summary>
    /// Provider metadata for DeepInfra.
    /// </summary>
    public class DeepInfraProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.DeepInfra;
        public override string DisplayName => "DeepInfra";
        public override string DefaultBaseUrl => "https://api.deepinfra.com/v1/openai";

        public DeepInfraProviderMetadata()
        {
            // DeepInfra supports full OpenAI-compatible features
            Capabilities.Features.Streaming = true;
            Capabilities.Features.ImageGeneration = true;
            Capabilities.Features.Embeddings = true;
            Capabilities.Features.VisionInput = true; // Multimodal support
            
            // Chat parameters support
            Capabilities.ChatParameters.Tools = true;
            Capabilities.ChatParameters.ResponseFormat = true;
            Capabilities.ChatParameters.Seed = true;
            
            ConfigurationHints.DocumentationUrl = "https://deepinfra.com/docs/openai_api";
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Advanced Reasoning Models",
                Description = "DeepInfra offers cutting-edge reasoning and coding models with extensive context windows",
                Severity = TipSeverity.Info
            });
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Model Versioning",
                Description = "You can specify model versions using MODEL_NAME:VERSION format",
                Severity = TipSeverity.Info
            });
        }
    }

    /// <summary>
    /// Provider metadata for OpenRouter.
    /// </summary>
    public class OpenRouterProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.OpenRouter;
        public override string DisplayName => "OpenRouter";
        public override string DefaultBaseUrl => "https://openrouter.ai/api/v1";

        public OpenRouterProviderMetadata()
        {
            // OpenRouter supports chat completions with features dependent on routed model
            Capabilities.Features.Streaming = true;
            Capabilities.Features.VisionInput = true;

            // Chat parameters support (dependent on routed model)
            Capabilities.ChatParameters.Tools = true;
            Capabilities.ChatParameters.ResponseFormat = true;
            Capabilities.ChatParameters.Seed = true;

            ConfigurationHints.DocumentationUrl = "https://openrouter.ai/docs";
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Multi-Provider Router",
                Description = "OpenRouter routes requests to 100+ models from providers like OpenAI, Anthropic, Google, and Meta through a single API",
                Severity = TipSeverity.Info
            });
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Model Naming",
                Description = "OpenRouter models use provider/model-name format (e.g., openai/gpt-4o, anthropic/claude-3.5-sonnet)",
                Severity = TipSeverity.Info
            });
        }
    }

    /// <summary>
    /// Provider metadata for Cloudflare Workers AI.
    /// </summary>
    public class CloudflareProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Cloudflare;
        public override string DisplayName => "Cloudflare Workers AI";
        public override string DefaultBaseUrl => "https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1";

        public CloudflareProviderMetadata()
        {
            // Cloudflare Workers AI supports OpenAI-compatible features
            Capabilities.Features.Streaming = true;
            Capabilities.Features.Embeddings = true;
            Capabilities.Features.ImageGeneration = true;

            // Chat parameters support
            Capabilities.ChatParameters.Tools = true;
            Capabilities.ChatParameters.ResponseFormat = true;

            AuthRequirements.CustomFields = new List<AuthField>
            {
                CreateUrlField("baseUrl", "API Base URL (must include account ID)", true,
                    "Format: https://api.cloudflare.com/client/v4/accounts/{ACCOUNT_ID}/ai/v1")
            };

            ConfigurationHints.DocumentationUrl = "https://developers.cloudflare.com/workers-ai/";
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Account ID Required",
                Description = "The base URL must include your Cloudflare account ID. Find it in the Cloudflare dashboard.",
                Severity = TipSeverity.Warning
            });
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Model Naming",
                Description = "Cloudflare models use the @cf/provider/model-name format (e.g., @cf/meta/llama-3.3-70b-instruct-fp8-fast)",
                Severity = TipSeverity.Info
            });
        }
    }
}