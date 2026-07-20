namespace ConduitLLM.Gateway.Middleware;

/// <summary>
/// Captures legacy response bodies up to a fixed limit, while sending SSE responses
/// directly to the original response stream. The mode is selected permanently by the
/// first write or flush after the response content type has been set.
/// </summary>
public sealed class ConditionalResponseCaptureStream : Stream
{
    private readonly HttpResponse _response;
    private readonly Stream _originalBody;
    private readonly MemoryStream _capturedBody;
    private readonly long _maximumCaptureBytes;
    private CaptureMode _mode;

    public ConditionalResponseCaptureStream(
        HttpResponse response,
        Stream originalBody,
        long maximumCaptureBytes)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(originalBody);
        if (maximumCaptureBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCaptureBytes));
        }

        _response = response;
        _originalBody = originalBody;
        _maximumCaptureBytes = maximumCaptureBytes;
        _capturedBody = new MemoryStream((int)Math.Min(maximumCaptureBytes, 64 * 1024));
    }

    public bool IsPassthrough => _mode is CaptureMode.Passthrough or CaptureMode.CaptureOverflowed;

    public bool CaptureLimitExceeded => _mode == CaptureMode.CaptureOverflowed;

    public MemoryStream CapturedBody => _capturedBody;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => _originalBody.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        SelectMode();
        if (IsPassthrough)
        {
            _originalBody.FlushAsync().GetAwaiter().GetResult();
        }
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        SelectMode();
        if (IsPassthrough)
        {
            await _originalBody.FlushAsync(cancellationToken);
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        SelectMode();

        if (_mode == CaptureMode.Capture && WouldExceedLimit(count))
        {
            SwitchCaptureToPassthrough();
        }

        if (IsPassthrough)
        {
            _originalBody.WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }
        else
        {
            _capturedBody.Write(buffer, offset, count);
        }
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        SelectMode();

        if (_mode == CaptureMode.Capture && WouldExceedLimit(buffer.Length))
        {
            await SwitchCaptureToPassthroughAsync(cancellationToken);
        }

        if (IsPassthrough)
        {
            await _originalBody.WriteAsync(buffer, cancellationToken);
        }
        else
        {
            await _capturedBody.WriteAsync(buffer, cancellationToken);
        }
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public async Task CopyCapturedBodyToOriginalAsync(CancellationToken cancellationToken = default)
    {
        if (IsPassthrough)
        {
            return;
        }

        _capturedBody.Position = 0;
        await _capturedBody.CopyToAsync(_originalBody, cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _capturedBody.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _capturedBody.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private void SelectMode()
    {
        if (_mode != CaptureMode.Undecided)
        {
            return;
        }

        _mode = IsServerSentEvents(_response.ContentType)
            ? CaptureMode.Passthrough
            : CaptureMode.Capture;
    }

    private bool WouldExceedLimit(int bytesToWrite) =>
        bytesToWrite > _maximumCaptureBytes - _capturedBody.Length;

    private void SwitchCaptureToPassthrough()
    {
        _capturedBody.Position = 0;
        _capturedBody.CopyToAsync(_originalBody).GetAwaiter().GetResult();
        _mode = CaptureMode.CaptureOverflowed;
    }

    private async Task SwitchCaptureToPassthroughAsync(CancellationToken cancellationToken)
    {
        _capturedBody.Position = 0;
        await _capturedBody.CopyToAsync(_originalBody, cancellationToken);
        _mode = CaptureMode.CaptureOverflowed;
    }

    private static bool IsServerSentEvents(string? contentType) =>
        contentType?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true;

    private enum CaptureMode
    {
        Undecided,
        Capture,
        Passthrough,
        CaptureOverflowed
    }
}
