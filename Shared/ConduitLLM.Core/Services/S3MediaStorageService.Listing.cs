using Amazon.S3.Model;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services;

public partial class S3MediaStorageService
{
    /// <inheritdoc/>
    public async Task<MediaStorageObjectPage> ListObjectsAsync(
        string? continuationToken = null,
        int pageSize = 1000,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketInitializedAsync();

        var response = await _s3Client.ListObjectsV2Async(
            new ListObjectsV2Request
            {
                BucketName = _bucketName,
                ContinuationToken = continuationToken,
                MaxKeys = Math.Clamp(pageSize, 1, 1000)
            },
            cancellationToken);

        return new MediaStorageObjectPage
        {
            Objects = (response.S3Objects ?? [])
                .Select(item => new MediaStorageObject
                {
                    StorageKey = item.Key,
                    SizeBytes = item.Size ?? 0,
                    // An object without a modification timestamp must be protected by
                    // reconciliation rather than treated as old enough to delete.
                    LastModifiedUtc = item.LastModified?.ToUniversalTime() ??
                        DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)
                })
                .ToList(),
            NextContinuationToken = response.IsTruncated == true
                ? response.NextContinuationToken
                : null
        };
    }
}
