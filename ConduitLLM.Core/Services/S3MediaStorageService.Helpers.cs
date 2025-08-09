using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    public partial class S3MediaStorageService
    {
        /// <summary>
        /// Stream wrapper that reports progress during read operations.
        /// </summary>
        private class ProgressStream : Stream
        {
            private readonly Stream _innerStream;
            private readonly Action<long>? _progressCallback;
            private long _totalBytesRead;

            public ProgressStream(Stream innerStream, Action<long>? progressCallback)
            {
                _innerStream = innerStream;
                _progressCallback = progressCallback;
                _totalBytesRead = 0;
            }

            public override bool CanRead => _innerStream.CanRead;
            public override bool CanSeek => _innerStream.CanSeek;
            public override bool CanWrite => _innerStream.CanWrite;
            public override long Length => _innerStream.Length;
            public override long Position 
            { 
                get => _innerStream.Position;
                set => _innerStream.Position = value;
            }

            public override void Flush() => _innerStream.Flush();
            
            public override int Read(byte[] buffer, int offset, int count)
            {
                var bytesRead = _innerStream.Read(buffer, offset, count);
                if (bytesRead > 0)
                {
                    _totalBytesRead += bytesRead;
                    _progressCallback?.Invoke(_totalBytesRead);
                }
                return bytesRead;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
                if (bytesRead > 0)
                {
                    _totalBytesRead += bytesRead;
                    _progressCallback?.Invoke(_totalBytesRead);
                }
                return bytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
            public override void SetLength(long value) => _innerStream.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);
            
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _innerStream?.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Stream wrapper that reports progress using IProgress interface.
        /// </summary>
        private class ProgressReportingStream : Stream
        {
            private readonly Stream _innerStream;
            private readonly IProgress<long> _progress;
            private long _totalBytesRead;

            public Stream InnerStream => _innerStream;

            public ProgressReportingStream(Stream innerStream, IProgress<long> progress)
            {
                _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
                _progress = progress ?? throw new ArgumentNullException(nameof(progress));
                _totalBytesRead = 0;
            }

            public override bool CanRead => _innerStream.CanRead;
            public override bool CanSeek => _innerStream.CanSeek;
            public override bool CanWrite => _innerStream.CanWrite;
            public override long Length => _innerStream.Length;
            public override long Position 
            { 
                get => _innerStream.Position;
                set => _innerStream.Position = value;
            }

            public override void Flush() => _innerStream.Flush();
            
            public override int Read(byte[] buffer, int offset, int count)
            {
                var bytesRead = _innerStream.Read(buffer, offset, count);
                if (bytesRead > 0)
                {
                    _totalBytesRead += bytesRead;
                    _progress.Report(_totalBytesRead);
                }
                return bytesRead;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
                if (bytesRead > 0)
                {
                    _totalBytesRead += bytesRead;
                    _progress.Report(_totalBytesRead);
                }
                return bytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
            public override void SetLength(long value) => _innerStream.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);
            
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _innerStream?.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Configures CORS rules for the S3 bucket to allow browser access to media files.
        /// </summary>
        private async Task ConfigureBucketCorsAsync()
        {
            if (!_options.AutoConfigureCors)
            {
                _logger.LogInformation("Auto CORS configuration is disabled");
                return;
            }

            try
            {
                // Check if bucket already has CORS configuration
                try
                {
                    var corsConfig = await _s3Client.GetCORSConfigurationAsync(_bucketName);
                    if (corsConfig.Configuration?.Rules?.Any() == true)
                    {
                        _logger.LogInformation("Bucket {BucketName} already has {RuleCount} CORS rules configured", 
                            _bucketName, corsConfig.Configuration.Rules.Count);
                        return;
                    }
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // No CORS configured, proceed with setup
                    _logger.LogInformation("No CORS configuration found for bucket {BucketName}, configuring default rules", _bucketName);
                }
                catch (AmazonS3Exception ex) when (ex.ErrorCode == "AccessDenied")
                {
                    _logger.LogWarning("Access denied when checking CORS configuration for bucket {BucketName}. " +
                        "Ensure the IAM policy includes s3:GetBucketCors permission", _bucketName);
                    return;
                }

                // Apply default CORS configuration
                var putCorsRequest = new PutCORSConfigurationRequest
                {
                    BucketName = _bucketName,
                    Configuration = new CORSConfiguration
                    {
                        Rules = new List<CORSRule>
                        {
                            new CORSRule
                            {
                                AllowedMethods = _options.CorsAllowedMethods,
                                AllowedOrigins = _options.CorsAllowedOrigins,
                                AllowedHeaders = new List<string> { "*" },
                                ExposeHeaders = _options.CorsExposeHeaders,
                                MaxAgeSeconds = _options.CorsMaxAgeSeconds
                            }
                        }
                    }
                };

                await _s3Client.PutCORSConfigurationAsync(putCorsRequest);
                _logger.LogInformation("Successfully configured CORS for bucket {BucketName} with origins: {Origins}", 
                    _bucketName, string.Join(", ", _options.CorsAllowedOrigins));
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "AccessDenied")
            {
                _logger.LogWarning(ex, "Access denied when configuring CORS for bucket {BucketName}. " +
                    "Ensure the IAM policy includes s3:PutBucketCors permission. " +
                    "The service will continue without CORS configuration", _bucketName);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NotImplemented" || ex.ErrorCode == "MethodNotAllowed")
            {
                // Some S3-compatible services (like certain MinIO configurations) might not support CORS
                _logger.LogInformation("CORS configuration not supported by this S3 provider. " +
                    "This is common with some S3-compatible services like MinIO in certain configurations");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error configuring CORS for bucket {BucketName}. " +
                    "The service will continue without CORS configuration", _bucketName);
            }
        }
    }
}