using Amazon.S3.Model;
using Moq;

namespace ConduitLLM.Tests.Core.Services;

public partial class S3MediaStorageServiceTests
{
    [Fact]
    public async Task ListObjectsAsync_MapsPageAndContinuationToken()
    {
        var modified = new DateTime(2026, 7, 20, 10, 30, 0, DateTimeKind.Utc);
        _mockS3Client.Setup(client => client.ListObjectsV2Async(
                It.Is<ListObjectsV2Request>(request =>
                    request.BucketName == _options.BucketName &&
                    request.ContinuationToken == "page-1" &&
                    request.MaxKeys == 25),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                IsTruncated = true,
                NextContinuationToken = "page-2",
                S3Objects =
                [
                    new S3Object
                    {
                        Key = "image/example.png",
                        Size = 321,
                        LastModified = modified
                    }
                ]
            });

        var result = await _service.ListObjectsAsync("page-1", 25);

        var item = Assert.Single(result.Objects);
        Assert.Equal("image/example.png", item.StorageKey);
        Assert.Equal(321, item.SizeBytes);
        Assert.Equal(modified, item.LastModifiedUtc);
        Assert.Equal("page-2", result.NextContinuationToken);
    }
}
