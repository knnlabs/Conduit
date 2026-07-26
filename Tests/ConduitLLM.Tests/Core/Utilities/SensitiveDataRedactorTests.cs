using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Tests.Core.Utilities;

public sealed class SensitiveDataRedactorTests
{
    [Fact]
    public void Redact_RemovesInlineMediaLongBase64AndSignedQueries()
    {
        var rawBase64 = new string('A', 256);
        var input =
            $$"""{"image":"data:image/png;base64,{{rawBase64}}","audio":"{{rawBase64}}","url":"https://example.com/file.pdf?signature=secret&expires=1"}""";

        var result = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain(rawBase64, result);
        Assert.DoesNotContain("signature=secret", result);
        Assert.Contains("data:image/png;base64,[REDACTED]", result);
        Assert.Contains("https://example.com/file.pdf?[REDACTED]", result);
    }
}
