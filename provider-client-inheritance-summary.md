# Provider Client Inheritance Summary

## Inheritance Hierarchy

```
ILLMClient
  └── BaseLLMClient (abstract)
      ├── OpenAICompatibleClient (abstract)
      │   ├── OpenAIClient
      │   ├── OpenRouterClient
      │   ├── AzureOpenAIClient
      │   ├── FireworksClient
      │   ├── GroqClient
      │   ├── MistralClient
      │   └── OpenAICompatibleGenericClient
      │
      ├── CustomProviderClient (abstract)
      │   ├── CohereClient
      │   ├── GeminiClient
      │   ├── OllamaClient
      │   ├── ReplicateClient
      │   └── VertexAIClient
      │
      └── Direct BaseLLMClient Implementations
          ├── AnthropicClient
          ├── AWSTranscribeClient (+ IAudioTranscriptionClient, ITextToSpeechClient)
          ├── BedrockClient
          ├── ElevenLabsClient (+ ILLMClient, ITextToSpeechClient, IRealtimeAudioClient)
          ├── GoogleCloudAudioClient (+ IAudioTranscriptionClient, ITextToSpeechClient)
          ├── HuggingFaceClient
          ├── MiniMaxClient
          ├── SageMakerClient
          └── UltravoxClient (+ ILLMClient, IRealtimeAudioClient)
```

## Summary by Inheritance Type

### OpenAICompatibleClient Inheritors (7 providers)
These providers implement APIs compatible with the OpenAI format:
- **OpenAIClient** - The standard OpenAI implementation
- **OpenRouterClient** - Routes to multiple models via OpenRouter
- **AzureOpenAIClient** - Microsoft's Azure-hosted OpenAI
- **FireworksClient** - Fireworks AI's fast inference service
- **GroqClient** - Groq's ultra-fast LPU inference
- **MistralClient** - Mistral AI's models
- **OpenAICompatibleGenericClient** - Generic client for any OpenAI-compatible API

### CustomProviderClient Inheritors (5 providers)
These providers have unique APIs that differ from OpenAI's format:
- **CohereClient** - Cohere's proprietary API
- **GeminiClient** - Google's Gemini models
- **OllamaClient** - Local Ollama server
- **ReplicateClient** - Replicate's model hosting platform
- **VertexAIClient** - Google Cloud's Vertex AI

### Direct BaseLLMClient Implementations (10 providers)
These providers implement their own unique APIs directly:
- **AnthropicClient** - Anthropic's Claude models
- **AWSTranscribeClient** - AWS Transcribe service (audio)
- **BedrockClient** - AWS Bedrock for multiple model providers
- **ElevenLabsClient** - ElevenLabs voice synthesis
- **GoogleCloudAudioClient** - Google Cloud Speech/TTS services
- **HuggingFaceClient** - Hugging Face's inference API
- **MiniMaxClient** - MiniMax's LLM service
- **SageMakerClient** - AWS SageMaker endpoints
- **UltravoxClient** - Ultravox real-time audio

## Key Observations

1. **OpenAI Compatibility is Common**: 7 out of 22 providers use OpenAI-compatible APIs, showing the influence of OpenAI's API design as a de facto standard.

2. **Three-Tier Architecture**: 
   - `BaseLLMClient` provides core functionality
   - `OpenAICompatibleClient` and `CustomProviderClient` provide specialized base classes
   - Individual provider clients implement provider-specific logic

3. **Multi-Interface Support**: Several clients implement additional interfaces beyond ILLMClient:
   - Audio transcription: AWSTranscribeClient, GoogleCloudAudioClient
   - Text-to-speech: AWSTranscribeClient, ElevenLabsClient, GoogleCloudAudioClient
   - Real-time audio: ElevenLabsClient, UltravoxClient

4. **Custom vs Direct Implementation**: 
   - CustomProviderClient is used for providers with complex, non-OpenAI APIs that still share some common patterns
   - Direct BaseLLMClient implementation is used for highly specialized providers or those with simpler APIs