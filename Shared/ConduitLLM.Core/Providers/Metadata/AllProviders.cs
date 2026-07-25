using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Providers;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Providers.Metadata
{
    /// <summary>
    /// Provider metadata for Groq.
    /// </summary>
    public class GroqProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Groq;
        public override string DisplayName => "Groq";
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

        public GroqProviderMetadata()
        {
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
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

        public ReplicateProviderMetadata()
        {
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
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

        public FireworksProviderMetadata()
        {
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
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

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
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

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
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

        public CerebrasProviderMetadata()
        {
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
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

        public DeepInfraProviderMetadata()
        {
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
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

        public OpenRouterProviderMetadata()
        {
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
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

        public CloudflareProviderMetadata()
        {
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

    /// <summary>
    /// Provider metadata for Azure OpenAI Service.
    /// </summary>
    public class AzureProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Azure;
        public override string DisplayName => "Azure OpenAI";
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

        public AzureProviderMetadata()
        {
            AuthRequirements.ApiKeyHeaderName = "api-key";
            ConfigurationHints.DocumentationUrl = "https://learn.microsoft.com/azure/ai-services/openai/";
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Models Are Addressed By Deployment",
                Description = "Azure routes to a deployment, not a model name. Set each model mapping's provider model ID to the deployment name you created on the resource.",
                Severity = TipSeverity.Warning
            });
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Resource Name Builds The Endpoint",
                Description = "Enter the Azure OpenAI resource name to derive https://<resource>.openai.azure.com; set a custom API endpoint only for a private or custom domain.",
                Severity = TipSeverity.Info
            });
        }
    }

    /// <summary>
    /// Provider metadata for Meta AI (Meta Model API).
    /// </summary>
    public class MetaProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Meta;
        public override string DisplayName => "Meta AI";
        public override string DefaultBaseUrl => ProviderAdapterDefaultsRegistry.GetRequired(ProviderType).DefaultBaseUrl;

        public MetaProviderMetadata()
        {
            ConfigurationHints.DocumentationUrl = "https://ai.developer.meta.com/docs";
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Massive Context Window",
                Description = "Muse Spark models support a 1M-token context window with image, video, and PDF input",
                Severity = TipSeverity.Info
            });
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Public Preview",
                Description = "The Meta Model API is in public preview and currently US-only",
                Severity = TipSeverity.Info
            });
        }
    }
}
