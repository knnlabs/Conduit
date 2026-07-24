using Amazon.S3.Model;

using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services;

public partial class S3MediaStorageService : IMediaStorageHealthProbe
{
    /// <inheritdoc />
    public async Task<MediaStorageHealthProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        await _s3Client.HeadBucketAsync(
            new HeadBucketRequest { BucketName = _bucketName },
            cancellationToken);
        return new MediaStorageHealthProbeResult(
            "healthy",
            "s3",
            "S3 bucket is reachable",
            false,
            _bucketName,
            _options.ServiceUrl);
    }
}
