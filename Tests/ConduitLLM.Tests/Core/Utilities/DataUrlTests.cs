using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Tests.Core.Utilities;

public sealed class DataUrlTests
{
    [Fact]
    public void TryParse_AcceptsCaseInsensitiveSchemeAndBase64Marker()
    {
        Assert.True(DataUrl.TryParse("DATA:image/png;BASE64,AQID", out var parsed));
        Assert.Equal("image/png", parsed.MediaType);
        Assert.True(parsed.IsBase64);
        Assert.Equal("AQID", parsed.Data);
    }

    [Fact]
    public void TryParse_RequiresPayloadSeparator()
    {
        Assert.False(DataUrl.TryParse("data:image/png;base64", out _));
    }

    [Fact]
    public void ImageUrl_UsesSharedParser()
    {
        var image = new ImageUrl { Url = "DATA:image/png;BASE64,AQID" };

        Assert.True(image.IsBase64DataUrl);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal("AQID", image.Base64Data);
    }

    [Fact]
    public void ExtractImageData_RejectsNonBase64DataUrl()
    {
        var result = ImageUtility.ExtractImageDataFromDataUrl(
            "data:image/png,not-base64",
            out var mimeType);

        Assert.Null(result);
        Assert.Null(mimeType);
    }
}
