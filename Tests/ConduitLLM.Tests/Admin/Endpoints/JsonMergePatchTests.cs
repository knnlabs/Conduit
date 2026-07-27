using System.Text.Json;
using System.Text;

using ConduitLLM.Admin.Endpoints;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Tests.Admin.Endpoints;

public sealed class JsonMergePatchTests
{
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonMergePatchRequestConverterFactory());
        return options;
    }

    [Fact]
    public void Converter_PreservesAbsenceClearsNullAndMergesNestedObjects()
    {
        var request = JsonSerializer.Deserialize<UpdateProviderRequest>(
            """
            {
              "baseUrl": null,
              "settings": {
                "removed": null,
                "added": "new"
              }
            }
            """,
            Options())!;

        request.IsDefined(nameof(request.ProviderName)).Should().BeFalse();
        request.IsDefined(nameof(request.BaseUrl)).Should().BeTrue();

        request.TryGetPatchedProperty(
                nameof(request.BaseUrl),
                "https://old.example",
                out string? baseUrl)
            .Should().BeTrue();
        baseUrl.Should().BeNull();

        request.TryGetPatchedProperty(
                nameof(request.Settings),
                new Dictionary<string, string>
                {
                    ["kept"] = "original",
                    ["removed"] = "old"
                },
                out Dictionary<string, string>? settings)
            .Should().BeTrue();
        settings.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["kept"] = "original",
            ["added"] = "new"
        });
    }

    [Fact]
    public void Converter_RejectsUnknownProperties()
    {
        var act = () => JsonSerializer.Deserialize<UpdateProviderRequest>(
            """{"notAProviderProperty":true}""",
            Options());

        act.Should().Throw<JsonException>()
            .WithMessage("*notAProviderProperty*not writable*");
    }

    [Fact]
    public void Converter_RejectsNullForNonNullableRequestProperties()
    {
        var act = () => JsonSerializer.Deserialize<UpdateProviderRequest>(
            """{"isEnabled":null}""",
            Options());

        act.Should().Throw<JsonException>()
            .WithMessage("*isEnabled*");
    }

    [Fact]
    public async Task Binder_UsesMergePatchParsingAndRequiresItsMediaType()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(Microsoft.Extensions.Options.Options.Create(
                    new Microsoft.AspNetCore.Http.Json.JsonOptions()))
                .BuildServiceProvider()
        };
        context.Request.ContentType = "application/merge-patch+json; charset=utf-8";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            """{"settings":{"added":"value"}}"""));

        var patch = await JsonMergePatch<UpdateProviderRequest>.BindAsync(context, null!);

        patch.Value.TryGetPatchedProperty(
                nameof(UpdateProviderRequest.Settings),
                new Dictionary<string, string> { ["kept"] = "value" },
                out Dictionary<string, string>? settings)
            .Should().BeTrue();
        settings.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["kept"] = "value",
            ["added"] = "value"
        });

        context.Request.ContentType = "application/json";
        var act = async () => await JsonMergePatch<UpdateProviderRequest>.BindAsync(context, null!);
        (await act.Should().ThrowAsync<BadHttpRequestException>())
            .Which.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }
}
