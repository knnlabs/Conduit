using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.OpenAICompatible;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Models
{
    /// <summary>
    /// Tests to verify that ExtensionData is properly passed through from requests to providers.
    /// </summary>
    public class ExtensionDataPassThroughTests
    {
        [Fact]
        public void ChatCompletionRequest_ExtensionData_CanStoreArbitraryParameters()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message> { new Message { Role = "user", Content = "test" } },
                ExtensionData = new Dictionary<string, JsonElement>
                {
                    ["custom_param"] = JsonDocument.Parse("\"value\"").RootElement,
                    ["numeric_param"] = JsonDocument.Parse("42").RootElement,
                    ["bool_param"] = JsonDocument.Parse("true").RootElement,
                    ["array_param"] = JsonDocument.Parse("[1, 2, 3]").RootElement,
                    ["object_param"] = JsonDocument.Parse("{\"nested\": \"value\"}").RootElement
                }
            };

            // Assert
            Assert.NotNull(request.ExtensionData);
            Assert.Equal(5, request.ExtensionData.Count);
            Assert.Equal("value", request.ExtensionData["custom_param"].GetString());
            Assert.Equal(42, request.ExtensionData["numeric_param"].GetInt32());
            Assert.True(request.ExtensionData["bool_param"].GetBoolean());
            Assert.Equal(3, request.ExtensionData["array_param"].GetArrayLength());
            Assert.Equal("value", request.ExtensionData["object_param"].GetProperty("nested").GetString());
        }

        [Fact]
        public void ImageGenerationRequest_ExtensionData_CanStoreModelSpecificParameters()
        {
            // Arrange
            var request = new ImageGenerationRequest
            {
                Model = "stable-diffusion",
                Prompt = "a beautiful landscape",
                ExtensionData = new Dictionary<string, JsonElement>
                {
                    ["negative_prompt"] = JsonDocument.Parse("\"blurry, low quality\"").RootElement,
                    ["seed"] = JsonDocument.Parse("42").RootElement,
                    ["guidance_scale"] = JsonDocument.Parse("7.5").RootElement,
                    ["num_inference_steps"] = JsonDocument.Parse("50").RootElement,
                    ["sampler"] = JsonDocument.Parse("\"DPM++ 2M Karras\"").RootElement
                }
            };

            // Assert
            Assert.NotNull(request.ExtensionData);
            Assert.Equal(5, request.ExtensionData.Count);
            Assert.Equal("blurry, low quality", request.ExtensionData["negative_prompt"].GetString());
            Assert.Equal(42, request.ExtensionData["seed"].GetInt32());
            Assert.Equal(7.5, request.ExtensionData["guidance_scale"].GetDouble());
            Assert.Equal(50, request.ExtensionData["num_inference_steps"].GetInt32());
            Assert.Equal("DPM++ 2M Karras", request.ExtensionData["sampler"].GetString());
        }

        [Fact]
        public void VideoGenerationRequest_ExtensionData_CanStoreComplexParameters()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "video-model",
                Prompt = "a cat playing piano",
                ExtensionData = new Dictionary<string, JsonElement>
                {
                    ["negative_prompt"] = JsonDocument.Parse("\"blurry, distorted\"").RootElement,
                    ["seed"] = JsonDocument.Parse("12345").RootElement,
                    ["motion_bucket_id"] = JsonDocument.Parse("127").RootElement,
                    ["decode_chunk_size"] = JsonDocument.Parse("8").RootElement,
                    ["controlnet"] = JsonDocument.Parse("{\"model\": \"openpose\", \"strength\": 0.8}").RootElement
                }
            };

            // Assert
            Assert.NotNull(request.ExtensionData);
            Assert.Equal(5, request.ExtensionData.Count);
            Assert.Equal("blurry, distorted", request.ExtensionData["negative_prompt"].GetString());
            Assert.Equal(12345, request.ExtensionData["seed"].GetInt32());
            Assert.Equal(127, request.ExtensionData["motion_bucket_id"].GetInt32());
            Assert.Equal(8, request.ExtensionData["decode_chunk_size"].GetInt32());
            
            var controlnet = request.ExtensionData["controlnet"];
            Assert.Equal("openpose", controlnet.GetProperty("model").GetString());
            Assert.Equal(0.8, controlnet.GetProperty("strength").GetDouble());
        }

        [Fact]
        public void ExtensionData_WithFileReference_CanStoreBase64String()
        {
            // Arrange
            var base64Image = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
            var request = new ImageGenerationRequest
            {
                Model = "image-model",
                Prompt = "enhance this image",
                ExtensionData = new Dictionary<string, JsonElement>
                {
                    ["init_image"] = JsonDocument.Parse($"\"{base64Image}\"").RootElement,
                    ["strength"] = JsonDocument.Parse("0.75").RootElement
                }
            };

            // Assert
            Assert.NotNull(request.ExtensionData);
            Assert.Equal(base64Image, request.ExtensionData["init_image"].GetString());
            Assert.Equal(0.75, request.ExtensionData["strength"].GetDouble());
        }

        [Fact]
        public void ExtensionData_WithLoras_CanStoreArrayParameters()
        {
            // Arrange
            var request = new ImageGenerationRequest
            {
                Model = "sd-model",
                Prompt = "artistic style",
                ExtensionData = new Dictionary<string, JsonElement>
                {
                    ["loras"] = JsonDocument.Parse("[\"style_lora_v1\", \"detail_enhancer\"]").RootElement,
                    ["lora_weights"] = JsonDocument.Parse("[0.8, 0.5]").RootElement
                }
            };

            // Assert
            Assert.NotNull(request.ExtensionData);
            
            var loras = request.ExtensionData["loras"];
            Assert.Equal(2, loras.GetArrayLength());
            var lorasList = new List<string>();
            foreach (var lora in loras.EnumerateArray())
            {
                lorasList.Add(lora.GetString()!);
            }
            Assert.Contains("style_lora_v1", lorasList);
            Assert.Contains("detail_enhancer", lorasList);

            var weights = request.ExtensionData["lora_weights"];
            Assert.Equal(2, weights.GetArrayLength());
            var weightsList = new List<double>();
            foreach (var weight in weights.EnumerateArray())
            {
                weightsList.Add(weight.GetDouble());
            }
            Assert.Contains(0.8, weightsList);
            Assert.Contains(0.5, weightsList);
        }

        [Fact]
        public void ExtensionData_SerializationRoundTrip_PreservesAllTypes()
        {
            // Arrange
            var originalExtensionData = new Dictionary<string, JsonElement>
            {
                ["string_val"] = JsonDocument.Parse("\"test\"").RootElement,
                ["int_val"] = JsonDocument.Parse("42").RootElement,
                ["double_val"] = JsonDocument.Parse("3.14").RootElement,
                ["bool_val"] = JsonDocument.Parse("true").RootElement,
                ["null_val"] = JsonDocument.Parse("null").RootElement,
                ["array_val"] = JsonDocument.Parse("[1, \"two\", true]").RootElement,
                ["object_val"] = JsonDocument.Parse("{\"a\": 1, \"b\": \"test\"}").RootElement
            };

            var request = new ChatCompletionRequest
            {
                Model = "test",
                Messages = new List<Message> { new Message { Role = "user", Content = "test" } },
                ExtensionData = originalExtensionData
            };

            // Act - Serialize and deserialize
            var json = JsonSerializer.Serialize(request);
            var deserialized = JsonSerializer.Deserialize<ChatCompletionRequest>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.ExtensionData);
            Assert.Equal(originalExtensionData.Count, deserialized.ExtensionData.Count);
            
            Assert.Equal("test", deserialized.ExtensionData["string_val"].GetString());
            Assert.Equal(42, deserialized.ExtensionData["int_val"].GetInt32());
            Assert.Equal(3.14, deserialized.ExtensionData["double_val"].GetDouble(), 2);
            Assert.True(deserialized.ExtensionData["bool_val"].GetBoolean());
            Assert.Equal(JsonValueKind.Null, deserialized.ExtensionData["null_val"].ValueKind);
            
            var array = deserialized.ExtensionData["array_val"];
            Assert.Equal(3, array.GetArrayLength());
            
            var obj = deserialized.ExtensionData["object_val"];
            Assert.Equal(1, obj.GetProperty("a").GetInt32());
            Assert.Equal("test", obj.GetProperty("b").GetString());
        }
    }
}