namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Audio service wrapper that adds comprehensive monitoring and observability.
    /// </summary>
    /// <remarks>
    /// This class wraps audio service clients with monitoring, metrics collection, and tracing capabilities.
    /// <para>
    /// This class is split into multiple partial files:
    /// - MonitoringAudioService.cs: Main class declaration
    /// - MonitoringAudioService.Core.cs: Core functionality, dependencies, and initialization
    /// - MonitoringAudioService.Transcription.cs: Audio transcription monitoring (IAudioTranscriptionClient)
    /// - MonitoringAudioService.TextToSpeech.cs: Text-to-speech monitoring (ITextToSpeechClient)
    /// - MonitoringAudioService.Realtime.cs: Realtime audio monitoring (IRealtimeAudioClient)
    /// - MonitoringAudioService.Utilities.cs: Utility classes and monitored stream wrapper
    /// </para>
    /// </remarks>
    public partial class MonitoringAudioService
    {
        // All implementation is in partial class files
    }
}