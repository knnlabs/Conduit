'use client';

import { useVideoStore } from '../hooks/useVideoStore';
import { VideoResolutions, type VideoModel } from '../types';

interface VideoSettingsProps {
  models: VideoModel[];
}

export default function VideoSettings({ models }: VideoSettingsProps) {
  const { settings, updateSettings } = useVideoStore();
  
  const selectedModel = models.find(m => m.id === settings.model);
  const capabilities = selectedModel?.capabilities;

  return (
    <div className="video-settings-panel">
      <h3>Additional Video Settings</h3>
      <div className="video-settings-grid">
        {/* Duration */}
        <div className="setting-group">
          <label htmlFor="video-duration">
            Duration (seconds)
            {capabilities?.maxDuration && (
              <span className="label-hint"> (max: {capabilities.maxDuration}s)</span>
            )}
          </label>
          <input
            id="video-duration"
            type="number"
            min="1"
            max={capabilities?.maxDuration ?? 60}
            value={settings.duration}
            onChange={(e) => updateSettings({ duration: parseInt(e.target.value) || 5 })}
            className="form-input"
          />
        </div>

        {/* Resolution */}
        <div className="setting-group">
          <label htmlFor="video-resolution">Resolution</label>
          <select
            id="video-resolution"
            value={settings.size}
            onChange={(e) => updateSettings({ size: e.target.value })}
            className="form-select"
          >
            {capabilities?.supportedResolutions ? (
              capabilities.supportedResolutions.map((res) => (
                <option key={res} value={res}>
                  {res} {getResolutionLabel(res)}
                </option>
              ))
            ) : (
              Object.entries(VideoResolutions).map(([key, value]) => (
                <option key={value} value={value}>
                  {value} ({key.replace(/_/g, ' ')})
                </option>
              ))
            )}
          </select>
        </div>

        {/* FPS */}
        <div className="setting-group">
          <label htmlFor="video-fps">Frames Per Second</label>
          <select
            id="video-fps"
            value={settings.fps}
            onChange={(e) => updateSettings({ fps: parseInt(e.target.value) })}
            className="form-select"
          >
            {capabilities?.supportedFps ? (
              capabilities.supportedFps.map((fps) => (
                <option key={fps} value={String(fps)}>
                  {fps} FPS
                </option>
              ))
            ) : (
              [24, 30, 60].map((fps) => (
                <option key={fps} value={String(fps)}>
                  {fps} FPS
                </option>
              ))
            )}
          </select>
        </div>

        {/* Style */}
        {capabilities?.supportsCustomStyles !== false && (
          <div className="setting-group">
            <label htmlFor="video-style">Style (optional)</label>
            <input
              id="video-style"
              type="text"
              value={settings.style ?? ''}
              onChange={(e) => updateSettings({ style: e.target.value || undefined })}
              placeholder="e.g., cinematic, anime, realistic"
              className="form-input"
            />
          </div>
        )}

        {/* Response Format */}
        <div className="setting-group">
          <label htmlFor="video-format">Response Format</label>
          <select
            id="video-format"
            value={settings.responseFormat}
            onChange={(e) => updateSettings({ responseFormat: e.target.value as 'url' | 'b64_json' })}
            className="form-select"
          >
            <option value="url">URL (Recommended)</option>
            <option value="b64_json">Base64 JSON</option>
          </select>
        </div>
      </div>
    </div>
  );
}

function getResolutionLabel(resolution: string): string {
  const resolutionLabels = new Map([
    ['1280x720', '(HD)'],
    ['1920x1080', '(Full HD)'],
    ['720x1280', '(Vertical HD)'],
    ['1080x1920', '(Vertical Full HD)'],
    ['720x720', '(Square)'],
    ['720x480', '(SD)']
  ]);
  return resolutionLabels.get(resolution) ?? '';
}