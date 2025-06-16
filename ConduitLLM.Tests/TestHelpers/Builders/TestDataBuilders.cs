using System;
using System.Collections.Generic;
using System.Linq;

using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Tests.TestHelpers.Builders
{
    /// <summary>
    /// Fluent builders for creating test data with sensible defaults
    /// </summary>
    public static class TestDataBuilders
    {
        /// <summary>
        /// Create a new ChatCompletionRequest builder
        /// </summary>
        public static ChatCompletionRequestBuilder ChatRequest() => new ChatCompletionRequestBuilder();

        /// <summary>
        /// Create a new Message builder
        /// </summary>
        public static MessageBuilder Message() => new MessageBuilder();

        /// <summary>
        /// Create a new ImageGenerationRequest builder
        /// </summary>
        public static ImageGenerationRequestBuilder ImageRequest() => new ImageGenerationRequestBuilder();

        /// <summary>
        /// Create a new EmbeddingRequest builder
        /// </summary>
        public static EmbeddingRequestBuilder EmbeddingRequest() => new EmbeddingRequestBuilder();

        /// <summary>
        /// Create a new AudioTranscriptionRequest builder
        /// </summary>
        public static AudioTranscriptionRequestBuilder AudioTranscriptionRequest() => new AudioTranscriptionRequestBuilder();
    }

    public class ChatCompletionRequestBuilder
    {
        private readonly ChatCompletionRequest _request;

        public ChatCompletionRequestBuilder()
        {
            _request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message> { TestDataBuilders.Message().AsUser("Hello!").Build() },
                Temperature = 0.7,
                MaxTokens = 100,
                TopP = 1.0,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
                Stream = false
            };
        }

        public ChatCompletionRequestBuilder WithModel(string model)
        {
            _request.Model = model;
            return this;
        }

        public ChatCompletionRequestBuilder WithMessages(params Message[] messages)
        {
            _request.Messages = messages.ToList();
            return this;
        }

        public ChatCompletionRequestBuilder WithMessage(string role, string content)
        {
            _request.Messages = new List<Message> { new Message { Role = role, Content = content } };
            return this;
        }

        public ChatCompletionRequestBuilder AddMessage(string role, string content)
        {
            _request.Messages.Add(new Message { Role = role, Content = content });
            return this;
        }

        public ChatCompletionRequestBuilder WithSystemPrompt(string prompt)
        {
            _request.Messages.Insert(0, new Message { Role = MessageRole.System, Content = prompt });
            return this;
        }

        public ChatCompletionRequestBuilder WithTemperature(double temperature)
        {
            _request.Temperature = temperature;
            return this;
        }

        public ChatCompletionRequestBuilder WithMaxTokens(int maxTokens)
        {
            _request.MaxTokens = maxTokens;
            return this;
        }

        public ChatCompletionRequestBuilder WithTopP(double topP)
        {
            _request.TopP = topP;
            return this;
        }

        public ChatCompletionRequestBuilder WithTools(params Tool[] tools)
        {
            _request.Tools = tools.ToList();
            return this;
        }

        public ChatCompletionRequestBuilder WithStreaming(bool stream = true)
        {
            _request.Stream = stream;
            return this;
        }

        public ChatCompletionRequestBuilder WithStop(params string[] stopSequences)
        {
            _request.Stop = stopSequences.ToList();
            return this;
        }

        // Removed WithMetadata as ChatCompletionRequest doesn't have a Metadata property

        public ChatCompletionRequest Build() => _request;
    }

    public class MessageBuilder
    {
        private readonly Message _message;

        public MessageBuilder()
        {
            _message = new Message
            {
                Role = MessageRole.User,
                Content = "Default message content"
            };
        }

        public MessageBuilder AsUser(string content)
        {
            _message.Role = MessageRole.User;
            _message.Content = content;
            return this;
        }

        public MessageBuilder AsAssistant(string content)
        {
            _message.Role = MessageRole.Assistant;
            _message.Content = content;
            return this;
        }

        public MessageBuilder AsSystem(string content)
        {
            _message.Role = MessageRole.System;
            _message.Content = content;
            return this;
        }

        public MessageBuilder AsTool(string content, string toolCallId)
        {
            _message.Role = MessageRole.Tool;
            _message.Content = content;
            _message.ToolCallId = toolCallId;
            return this;
        }

        public MessageBuilder WithName(string name)
        {
            _message.Name = name;
            return this;
        }

        public MessageBuilder WithToolCalls(params ToolCall[] toolCalls)
        {
            _message.ToolCalls = toolCalls.ToList();
            return this;
        }

        public Message Build() => _message;
    }

    public class ImageGenerationRequestBuilder
    {
        private readonly ImageGenerationRequest _request;

        public ImageGenerationRequestBuilder()
        {
            _request = new ImageGenerationRequest
            {
                Prompt = "A beautiful sunset",
                Model = "dall-e-3",
                Size = "1024x1024",
                Quality = "standard",
                N = 1,
                ResponseFormat = "url"
            };
        }

        public ImageGenerationRequestBuilder WithPrompt(string prompt)
        {
            _request.Prompt = prompt;
            return this;
        }

        public ImageGenerationRequestBuilder WithModel(string model)
        {
            _request.Model = model;
            return this;
        }

        public ImageGenerationRequestBuilder WithSize(string size)
        {
            _request.Size = size;
            return this;
        }

        public ImageGenerationRequestBuilder WithQuality(string quality)
        {
            _request.Quality = quality;
            return this;
        }

        public ImageGenerationRequestBuilder WithCount(int n)
        {
            _request.N = n;
            return this;
        }

        public ImageGenerationRequestBuilder WithResponseFormat(string format)
        {
            _request.ResponseFormat = format;
            return this;
        }

        public ImageGenerationRequest Build() => _request;
    }

    public class EmbeddingRequestBuilder
    {
        private readonly EmbeddingRequest _request;

        public EmbeddingRequestBuilder()
        {
            _request = new EmbeddingRequest
            {
                Input = "Default embedding text",
                Model = "text-embedding-ada-002",
                EncodingFormat = "float"
            };
        }

        public EmbeddingRequestBuilder WithInput(string input)
        {
            _request.Input = input;
            return this;
        }

        public EmbeddingRequestBuilder WithInputs(params string[] inputs)
        {
            _request.Input = inputs;
            return this;
        }

        public EmbeddingRequestBuilder WithModel(string model)
        {
            _request.Model = model;
            return this;
        }

        public EmbeddingRequestBuilder WithEncodingFormat(string format)
        {
            _request.EncodingFormat = format;
            return this;
        }

        public EmbeddingRequestBuilder WithUser(string user)
        {
            _request.User = user;
            return this;
        }

        public EmbeddingRequest Build() => _request;
    }

    public class AudioTranscriptionRequestBuilder
    {
        private readonly AudioTranscriptionRequest _request;

        public AudioTranscriptionRequestBuilder()
        {
            _request = new AudioTranscriptionRequest
            {
                AudioData = Array.Empty<byte>(),
                FileName = "audio.mp3",
                Model = "whisper-1",
                Language = "en",
                ResponseFormat = TranscriptionFormat.Json,
                Temperature = 0
            };
        }

        public AudioTranscriptionRequestBuilder WithFile(byte[] audioData, string fileName)
        {
            _request.AudioData = audioData;
            _request.FileName = fileName;
            return this;
        }

        public AudioTranscriptionRequestBuilder WithModel(string model)
        {
            _request.Model = model;
            return this;
        }

        public AudioTranscriptionRequestBuilder WithLanguage(string language)
        {
            _request.Language = language;
            return this;
        }

        public AudioTranscriptionRequestBuilder WithPrompt(string prompt)
        {
            _request.Prompt = prompt;
            return this;
        }

        public AudioTranscriptionRequestBuilder WithResponseFormat(TranscriptionFormat format)
        {
            _request.ResponseFormat = format;
            return this;
        }

        public AudioTranscriptionRequestBuilder WithTemperature(double temperature)
        {
            _request.Temperature = temperature;
            return this;
        }

        public AudioTranscriptionRequest Build() => _request;
    }
}