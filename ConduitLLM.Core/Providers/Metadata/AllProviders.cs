using System.Collections.Generic;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Providers.Metadata
{
    /// <summary>
    /// Provider metadata for Google Vertex AI.
    /// </summary>
    public class VertexAIProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.VertexAI;
        public override string DisplayName => "Google Vertex AI";
        public override string DefaultBaseUrl => "https://{region}-aiplatform.googleapis.com/v1";

        public VertexAIProviderMetadata()
        {
            Capabilities.Features.VisionInput = true;
            Capabilities.Features.FunctionCalling = true;
            Capabilities.Features.Embeddings = true;
            
            AuthRequirements.RequiresApiKey = false;
            AuthRequirements.SupportsOAuth = true;
            AuthRequirements.CustomFields = new List<AuthField>
            {
                new AuthField { Name = "projectId", DisplayName = "Project ID", Required = true, Type = AuthFieldType.Text },
                new AuthField { Name = "region", DisplayName = "Region", Required = true, Type = AuthFieldType.Text }
            };
            
            ConfigurationHints.DocumentationUrl = "https://cloud.google.com/vertex-ai/docs";
            ConfigurationHints.RequiresSpecialSetup = true;
            ConfigurationHints.SetupInstructions = "Requires Google Cloud project setup and authentication";
        }
    }

    /// <summary>
    /// Provider metadata for Cohere.
    /// </summary>
    public class CohereProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Cohere;
        public override string DisplayName => "Cohere";
        public override string DefaultBaseUrl => "https://api.cohere.ai/v1";

        public CohereProviderMetadata()
        {
            Capabilities.Features.Embeddings = true;
            Capabilities.ChatParameters.Tools = true;
            
            AuthRequirements.ApiKeyHeaderName = "Authorization";
            ConfigurationHints.DocumentationUrl = "https://docs.cohere.com/";
        }
    }

    /// <summary>
    /// Provider metadata for Mistral AI.
    /// </summary>
    public class MistralProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Mistral;
        public override string DisplayName => "Mistral AI";
        public override string DefaultBaseUrl => "https://api.mistral.ai/v1";

        public MistralProviderMetadata()
        {
            Capabilities.Features.Embeddings = true;
            Capabilities.ChatParameters.Tools = true;
            Capabilities.ChatParameters.ResponseFormat = true;
            
            ConfigurationHints.DocumentationUrl = "https://docs.mistral.ai/";
        }
    }

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
    /// Provider metadata for Ollama.
    /// </summary>
    public class OllamaProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Ollama;
        public override string DisplayName => "Ollama";
        public override string DefaultBaseUrl => "http://localhost:11434/api";

        public OllamaProviderMetadata()
        {
            Capabilities.Features.Embeddings = true;
            
            AuthRequirements.RequiresApiKey = false;
            ConfigurationHints.RequiresSpecialSetup = true;
            ConfigurationHints.SetupInstructions = "Install and run Ollama locally";
            ConfigurationHints.DocumentationUrl = "https://ollama.ai/";
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
            Capabilities.Features.AudioTranscription = true;
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
    /// Provider metadata for AWS Bedrock.
    /// </summary>
    public class BedrockProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Bedrock;
        public override string DisplayName => "AWS Bedrock";
        public override string DefaultBaseUrl => "https://bedrock-runtime.{region}.amazonaws.com";

        public BedrockProviderMetadata()
        {
            Capabilities.Features.Embeddings = true;
            Capabilities.Features.ImageGeneration = true;
            
            AuthRequirements.RequiresApiKey = false;
            AuthRequirements.SupportsOAuth = true;
            AuthRequirements.CustomFields = new List<AuthField>
            {
                new AuthField { Name = "region", DisplayName = "AWS Region", Required = true, Type = AuthFieldType.Text },
                new AuthField { Name = "accessKeyId", DisplayName = "Access Key ID", Required = true, Type = AuthFieldType.Text },
                new AuthField { Name = "secretAccessKey", DisplayName = "Secret Access Key", Required = true, Type = AuthFieldType.Password }
            };
            
            ConfigurationHints.RequiresSpecialSetup = true;
            ConfigurationHints.DocumentationUrl = "https://docs.aws.amazon.com/bedrock/";
        }
    }

    /// <summary>
    /// Provider metadata for Hugging Face.
    /// </summary>
    public class HuggingFaceProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.HuggingFace;
        public override string DisplayName => "Hugging Face";
        public override string DefaultBaseUrl => "https://api-inference.huggingface.co/models";

        public HuggingFaceProviderMetadata()
        {
            Capabilities.Features.Embeddings = true;
            Capabilities.Features.ImageGeneration = true;
            
            ConfigurationHints.DocumentationUrl = "https://huggingface.co/docs/api-inference";
        }
    }

    /// <summary>
    /// Provider metadata for AWS SageMaker.
    /// </summary>
    public class SageMakerProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.SageMaker;
        public override string DisplayName => "AWS SageMaker";
        public override string DefaultBaseUrl => "https://runtime.sagemaker.{region}.amazonaws.com";

        public SageMakerProviderMetadata()
        {
            AuthRequirements.RequiresApiKey = false;
            AuthRequirements.SupportsOAuth = true;
            AuthRequirements.CustomFields = new List<AuthField>
            {
                new AuthField { Name = "endpointName", DisplayName = "Endpoint Name", Required = true, Type = AuthFieldType.Text },
                new AuthField { Name = "region", DisplayName = "AWS Region", Required = true, Type = AuthFieldType.Text }
            };
            
            ConfigurationHints.RequiresSpecialSetup = true;
            ConfigurationHints.DocumentationUrl = "https://docs.aws.amazon.com/sagemaker/";
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
            Capabilities.ChatParameters = new ChatParameterSupport
            {
                Temperature = true,
                MaxTokens = true,
                TopP = true,
                TopK = true,
                Stop = true,
                Tools = true
            };
            
            ConfigurationHints.DocumentationUrl = "https://openrouter.ai/docs";
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Model Routing",
                Description = "OpenRouter automatically routes to different providers",
                Severity = TipSeverity.Info
            });
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
            Capabilities.Features.TextToSpeech = true;
            Capabilities.Features.AudioTranscription = true;
            
            ConfigurationHints.DocumentationUrl = "https://api.minimax.chat/document/introduction";
        }
    }

    /// <summary>
    /// Provider metadata for Ultravox.
    /// </summary>
    public class UltravoxProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.Ultravox;
        public override string DisplayName => "Ultravox";
        public override string DefaultBaseUrl => "https://api.ultravox.ai/v1";

        public UltravoxProviderMetadata()
        {
            Capabilities.Features.AudioTranscription = true;
            Capabilities.Features.TextToSpeech = true;
            
            ConfigurationHints.DocumentationUrl = "https://docs.ultravox.ai/";
        }
    }

    /// <summary>
    /// Provider metadata for ElevenLabs.
    /// </summary>
    public class ElevenLabsProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.ElevenLabs;
        public override string DisplayName => "ElevenLabs";
        public override string DefaultBaseUrl => "https://api.elevenlabs.io/v1";

        public ElevenLabsProviderMetadata()
        {
            Capabilities.Features.TextToSpeech = true;
            Capabilities.Features.Streaming = false;
            Capabilities.ChatParameters = new ChatParameterSupport(); // Audio provider, limited chat params
            
            AuthRequirements.ApiKeyHeaderName = "xi-api-key";
            ConfigurationHints.DocumentationUrl = "https://docs.elevenlabs.io/";
        }
    }

    /// <summary>
    /// Provider metadata for Google Cloud (Audio).
    /// </summary>
    public class GoogleCloudProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.GoogleCloud;
        public override string DisplayName => "Google Cloud";
        public override string DefaultBaseUrl => "https://texttospeech.googleapis.com/v1";

        public GoogleCloudProviderMetadata()
        {
            Capabilities.Features.TextToSpeech = true;
            Capabilities.Features.AudioTranscription = true;
            Capabilities.Features.Streaming = false;
            Capabilities.ChatParameters = new ChatParameterSupport(); // Audio provider
            
            AuthRequirements.RequiresApiKey = false;
            AuthRequirements.SupportsOAuth = true;
            ConfigurationHints.RequiresSpecialSetup = true;
            ConfigurationHints.DocumentationUrl = "https://cloud.google.com/text-to-speech/docs";
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
    /// Provider metadata for AWS Transcribe.
    /// </summary>
    public class AWSTranscribeProviderMetadata : BaseProviderMetadata
    {
        public override ProviderType ProviderType => ProviderType.AWSTranscribe;
        public override string DisplayName => "AWS Transcribe";
        public override string DefaultBaseUrl => "https://transcribe.{region}.amazonaws.com";

        public AWSTranscribeProviderMetadata()
        {
            // AWS Transcribe is an audio-only provider
            Capabilities.Features.AudioTranscription = true;
            Capabilities.Features.TextToSpeech = true;
            Capabilities.Features.Streaming = false;
            Capabilities.ChatParameters = new ChatParameterSupport(); // Audio provider, no chat params
            
            // AWS uses IAM authentication
            AuthRequirements.RequiresApiKey = false;
            AuthRequirements.SupportsOAuth = true;
            AuthRequirements.CustomFields = new List<AuthField>
            {
                new AuthField { Name = "region", DisplayName = "AWS Region", Required = true, Type = AuthFieldType.Text },
                new AuthField { Name = "accessKeyId", DisplayName = "Access Key ID", Required = true, Type = AuthFieldType.Text },
                new AuthField { Name = "secretAccessKey", DisplayName = "Secret Access Key", Required = true, Type = AuthFieldType.Password }
            };
            
            ConfigurationHints.RequiresSpecialSetup = true;
            ConfigurationHints.SetupInstructions = "Requires AWS account with Transcribe and Polly permissions";
            ConfigurationHints.DocumentationUrl = "https://docs.aws.amazon.com/transcribe/";
            ConfigurationHints.Tips.Add(new ConfigurationTip
            {
                Title = "Service Requirements",
                Description = "Uses AWS Transcribe for speech-to-text and AWS Polly for text-to-speech",
                Severity = TipSeverity.Info
            });
        }
    }
}