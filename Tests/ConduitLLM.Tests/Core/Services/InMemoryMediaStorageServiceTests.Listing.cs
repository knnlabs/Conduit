using System.Text;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Services;

public partial class InMemoryMediaStorageServiceTests
{
    [Fact]
    public async Task ListObjectsAsync_PaginatesStoredObjects()
    {
        var firstStored = await _service.StoreAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("first")),
            new MediaMetadata
            {
                ContentType = "image/png",
                MediaType = MediaType.Image
            });
        var secondStored = await _service.StoreAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("second")),
            new MediaMetadata
            {
                ContentType = "video/mp4",
                MediaType = MediaType.Video
            });

        var firstPage = await _service.ListObjectsAsync(pageSize: 1);
        var secondPage = await _service.ListObjectsAsync(
            firstPage.NextContinuationToken,
            pageSize: 1);

        Assert.Single(firstPage.Objects);
        Assert.NotNull(firstPage.NextContinuationToken);
        Assert.Single(secondPage.Objects);
        Assert.Null(secondPage.NextContinuationToken);
        Assert.Equal(
            new[] { firstStored.StorageKey, secondStored.StorageKey }.Order(),
            firstPage.Objects.Concat(secondPage.Objects)
                .Select(item => item.StorageKey)
                .Order());
        Assert.All(
            firstPage.Objects.Concat(secondPage.Objects),
            item => Assert.NotEqual(default, item.LastModifiedUtc));
    }
}
