namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Bidirectional MIME type &lt;-&gt; file extension mappings for media (image/audio/video)
    /// and a few common document types. Consolidates the previously duplicated per-service
    /// switch statements. Lookups are case-insensitive; unknown values return null so each
    /// call site can apply its own fallback.
    /// </summary>
    public static class MediaContentTypes
    {
        private static readonly Dictionary<string, string> MimeToExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/gif"] = ".gif",
            ["image/webp"] = ".webp",
            ["image/bmp"] = ".bmp",
            ["image/svg+xml"] = ".svg",
            // Video
            ["video/mp4"] = ".mp4",
            ["video/webm"] = ".webm",
            ["video/quicktime"] = ".mov",
            ["video/x-msvideo"] = ".avi",
            ["video/x-matroska"] = ".mkv",
            ["video/x-flv"] = ".flv",
            ["video/x-ms-wmv"] = ".wmv",
            ["video/x-m4v"] = ".m4v",
            // Audio
            ["audio/mpeg"] = ".mp3",
            ["audio/wav"] = ".wav",
            ["audio/webm"] = ".weba",
            ["audio/ogg"] = ".ogg",
            ["audio/mp4"] = ".m4a",
            ["audio/flac"] = ".flac",
            ["audio/aac"] = ".aac",
            // Documents
            ["application/pdf"] = ".pdf",
            ["application/json"] = ".json",
            ["text/plain"] = ".txt",
            ["text/csv"] = ".csv",
            ["application/xml"] = ".xml",
            ["text/html"] = ".html",
        };

        private static readonly Dictionary<string, string> ExtensionToMime = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".bmp"] = "image/bmp",
            [".svg"] = "image/svg+xml",
            // Video
            [".mp4"] = "video/mp4",
            [".webm"] = "video/webm",
            [".mov"] = "video/quicktime",
            [".avi"] = "video/x-msvideo",
            [".mkv"] = "video/x-matroska",
            [".flv"] = "video/x-flv",
            [".wmv"] = "video/x-ms-wmv",
            [".m4v"] = "video/x-m4v",
            // Audio
            [".mp3"] = "audio/mpeg",
            [".wav"] = "audio/wav",
            [".weba"] = "audio/webm",
            [".ogg"] = "audio/ogg",
            [".m4a"] = "audio/mp4",
            [".flac"] = "audio/flac",
            [".aac"] = "audio/aac",
            // Documents
            [".pdf"] = "application/pdf",
            [".json"] = "application/json",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".xml"] = "application/xml",
            [".html"] = "text/html",
        };

        /// <summary>
        /// Gets the file extension (with leading dot, e.g. ".jpg") for a MIME content type.
        /// </summary>
        /// <param name="mimeType">The MIME content type (e.g. "image/jpeg"). Case-insensitive.</param>
        /// <returns>The extension with a leading dot, or null when the MIME type is unknown.</returns>
        public static string? GetExtension(string? mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
            {
                return null;
            }

            return MimeToExtension.TryGetValue(mimeType, out var extension) ? extension : null;
        }

        /// <summary>
        /// Gets the MIME content type for a file extension.
        /// </summary>
        /// <param name="extension">The file extension, with or without a leading dot (e.g. ".png" or "png"). Case-insensitive.</param>
        /// <returns>The MIME content type, or null when the extension is unknown.</returns>
        public static string? GetContentType(string? extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return null;
            }

            var normalized = extension.StartsWith('.') ? extension : "." + extension;
            return ExtensionToMime.TryGetValue(normalized, out var mimeType) ? mimeType : null;
        }
    }
}
