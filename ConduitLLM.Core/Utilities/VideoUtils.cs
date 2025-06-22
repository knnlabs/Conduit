using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Utility methods for working with video files and metadata.
    /// </summary>
    public static class VideoUtils
    {
        private static readonly Dictionary<string, string> MimeTypeToExtension = new()
        {
            { "video/mp4", ".mp4" },
            { "video/mpeg", ".mpeg" },
            { "video/quicktime", ".mov" },
            { "video/x-msvideo", ".avi" },
            { "video/x-ms-wmv", ".wmv" },
            { "video/webm", ".webm" },
            { "video/ogg", ".ogv" },
            { "video/3gpp", ".3gp" },
            { "video/3gpp2", ".3g2" },
            { "video/x-matroska", ".mkv" },
            { "video/x-flv", ".flv" },
            { "video/mp2t", ".ts" },
            { "video/x-m4v", ".m4v" }
        };

        private static readonly HashSet<string> SupportedVideoMimeTypes = new(MimeTypeToExtension.Keys);

        private static readonly Dictionary<string, string> ExtensionToMimeType = 
            MimeTypeToExtension.ToDictionary(kvp => kvp.Value.ToLowerInvariant(), kvp => kvp.Key);

        /// <summary>
        /// Checks if a MIME type is a supported video format.
        /// </summary>
        /// <param name="mimeType">The MIME type to check.</param>
        /// <returns>True if the MIME type is a supported video format.</returns>
        public static bool IsVideoMimeType(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
                return false;

            // Handle MIME types with parameters (e.g., "video/mp4; codecs=avc1")
            var baseMimeType = mimeType.Split(';')[0].Trim().ToLowerInvariant();
            return SupportedVideoMimeTypes.Contains(baseMimeType) || baseMimeType.StartsWith("video/");
        }

        /// <summary>
        /// Gets the file extension for a video MIME type.
        /// </summary>
        /// <param name="mimeType">The MIME type.</param>
        /// <returns>The file extension including the dot, or null if not found.</returns>
        public static string? GetVideoExtension(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
                return null;

            var baseMimeType = mimeType.Split(';')[0].Trim().ToLowerInvariant();
            return MimeTypeToExtension.TryGetValue(baseMimeType, out var extension) ? extension : null;
        }

        /// <summary>
        /// Gets the MIME type for a video file extension.
        /// </summary>
        /// <param name="extension">The file extension (with or without dot).</param>
        /// <returns>The MIME type, or null if not found.</returns>
        public static string? GetMimeTypeFromExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return null;

            // Ensure extension starts with a dot
            if (!extension.StartsWith('.'))
                extension = "." + extension;

            return ExtensionToMimeType.TryGetValue(extension.ToLowerInvariant(), out var mimeType) ? mimeType : null;
        }

        /// <summary>
        /// Validates a video file stream.
        /// </summary>
        /// <param name="stream">The video stream to validate.</param>
        /// <param name="maxSizeBytes">Maximum allowed file size in bytes.</param>
        /// <returns>True if the video file is valid.</returns>
        public static bool ValidateVideoFile(Stream stream, long maxSizeBytes)
        {
            if (stream == null || !stream.CanRead)
                return false;

            // Check file size
            if (stream.Length > maxSizeBytes)
                return false;

            // Basic validation - check for common video file signatures
            var position = stream.Position;
            try
            {
                var buffer = new byte[12];
                stream.Position = 0;
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                
                if (bytesRead < 4)
                    return false;

                // Check for common video file signatures
                // MP4: ftyp
                if (buffer[4] == 0x66 && buffer[5] == 0x74 && buffer[6] == 0x79 && buffer[7] == 0x70)
                    return true;
                
                // WebM: 0x1A 0x45 0xDF 0xA3
                if (buffer[0] == 0x1A && buffer[1] == 0x45 && buffer[2] == 0xDF && buffer[3] == 0xA3)
                    return true;
                
                // AVI: RIFF....AVI
                if (buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 &&
                    bytesRead >= 12 && buffer[8] == 0x41 && buffer[9] == 0x56 && buffer[10] == 0x49)
                    return true;
                
                // MOV: ....moov or ....mdat or ....ftyp
                if (bytesRead >= 8 && 
                    ((buffer[4] == 0x6D && buffer[5] == 0x6F && buffer[6] == 0x6F && buffer[7] == 0x76) ||
                     (buffer[4] == 0x6D && buffer[5] == 0x64 && buffer[6] == 0x61 && buffer[7] == 0x74)))
                    return true;

                // OGG: OggS
                if (buffer[0] == 0x4F && buffer[1] == 0x67 && buffer[2] == 0x67 && buffer[3] == 0x53)
                    return true;

                return false;
            }
            finally
            {
                stream.Position = position;
            }
        }

        /// <summary>
        /// Parses a duration string into a TimeSpan.
        /// Supports formats like "5s", "1m30s", "01:30", "1:30:00".
        /// </summary>
        /// <param name="duration">The duration string to parse.</param>
        /// <returns>The parsed TimeSpan.</returns>
        public static TimeSpan ParseDuration(string duration)
        {
            if (string.IsNullOrWhiteSpace(duration))
                throw new ArgumentException("Duration cannot be empty", nameof(duration));

            // Try standard TimeSpan format first (hh:mm:ss or mm:ss)
            if (TimeSpan.TryParse(duration, out var timeSpan))
                return timeSpan;

            // Try parsing with units (e.g., "5s", "1m30s", "2h15m30s")
            var regex = new Regex(@"(?:(\d+)h)?(?:(\d+)m)?(?:(\d+(?:\.\d+)?)s)?", RegexOptions.IgnoreCase);
            var match = regex.Match(duration);
            
            if (match.Success)
            {
                var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
                var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                var seconds = match.Groups[3].Success ? double.Parse(match.Groups[3].Value) : 0;
                
                return new TimeSpan(hours, minutes, (int)seconds) + TimeSpan.FromMilliseconds((seconds % 1) * 1000);
            }

            // Try parsing as total seconds
            if (double.TryParse(duration, out var totalSeconds))
                return TimeSpan.FromSeconds(totalSeconds);

            throw new FormatException($"Unable to parse duration: {duration}");
        }

        /// <summary>
        /// Formats a TimeSpan as a readable duration string.
        /// </summary>
        /// <param name="duration">The duration to format.</param>
        /// <param name="includeMilliseconds">Whether to include milliseconds in the output.</param>
        /// <returns>A formatted duration string.</returns>
        public static string FormatDuration(TimeSpan duration, bool includeMilliseconds = false)
        {
            if (duration.TotalDays >= 1)
            {
                return includeMilliseconds 
                    ? $"{(int)duration.TotalDays}d {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}.{duration.Milliseconds:000}"
                    : $"{(int)duration.TotalDays}d {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
            }
            else if (duration.TotalHours >= 1)
            {
                return includeMilliseconds 
                    ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}.{duration.Milliseconds:000}"
                    : $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}";
            }
            else
            {
                return includeMilliseconds 
                    ? $"{duration.Minutes:00}:{duration.Seconds:00}.{duration.Milliseconds:000}"
                    : $"{duration.Minutes:00}:{duration.Seconds:00}";
            }
        }

        /// <summary>
        /// Calculates the estimated file size for a video.
        /// </summary>
        /// <param name="durationSeconds">Duration of the video in seconds.</param>
        /// <param name="bitrate">Bitrate in bits per second.</param>
        /// <returns>Estimated file size in bytes.</returns>
        public static long EstimateVideoFileSize(double durationSeconds, long bitrate)
        {
            // File size (bytes) = (bitrate (bits/s) * duration (s)) / 8
            return (long)((bitrate * durationSeconds) / 8);
        }

        /// <summary>
        /// Parses a video resolution string (e.g., "1920x1080") into width and height.
        /// </summary>
        /// <param name="resolution">The resolution string.</param>
        /// <returns>A tuple of (width, height).</returns>
        public static (int width, int height) ParseResolution(string resolution)
        {
            if (string.IsNullOrWhiteSpace(resolution))
                throw new ArgumentException("Resolution cannot be empty", nameof(resolution));

            var parts = resolution.ToLowerInvariant().Split('x');
            if (parts.Length != 2)
                throw new FormatException($"Invalid resolution format: {resolution}. Expected format: WIDTHxHEIGHT");

            if (!int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height))
                throw new FormatException($"Invalid resolution values: {resolution}");

            if (width <= 0 || height <= 0)
                throw new ArgumentException($"Resolution dimensions must be positive: {resolution}");

            return (width, height);
        }

        /// <summary>
        /// Gets a list of common video resolutions.
        /// </summary>
        /// <returns>Dictionary of resolution names to resolution strings.</returns>
        public static Dictionary<string, string> GetCommonResolutions()
        {
            return new Dictionary<string, string>
            {
                { "SD (480p)", "854x480" },
                { "HD (720p)", "1280x720" },
                { "Full HD (1080p)", "1920x1080" },
                { "2K", "2560x1440" },
                { "4K (2160p)", "3840x2160" },
                { "8K (4320p)", "7680x4320" },
                { "Square", "1080x1080" },
                { "Portrait HD", "720x1280" },
                { "Portrait Full HD", "1080x1920" }
            };
        }

        /// <summary>
        /// Validates if a frame rate is within acceptable bounds.
        /// </summary>
        /// <param name="fps">Frames per second to validate.</param>
        /// <returns>True if the frame rate is valid.</returns>
        public static bool IsValidFrameRate(int fps)
        {
            // Common frame rates: 24, 25, 30, 48, 50, 60, 120
            return fps > 0 && fps <= 240;
        }
    }
}