using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Tests.Core.Utilities;

public sealed class MediaStorageKeysTests
{
    [Fact]
    public void GenerateFlat_PreservesInMemoryLayout()
    {
        Assert.Equal(
            "image/content-hash.png",
            MediaStorageKeys.GenerateFlat("content-hash", MediaType.Image, ".png"));
    }

    [Fact]
    public void GenerateDatePartitioned_PreservesS3Layout()
    {
        Assert.Equal(
            "video/2026/07/26/content-hash.mp4",
            MediaStorageKeys.GenerateDatePartitioned(
                "content-hash",
                MediaType.Video,
                ".mp4",
                new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc)));
    }
}
