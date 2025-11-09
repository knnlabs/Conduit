'use client';

import { useEffect, useRef } from 'react';
import videojs from 'video.js';
import Player from 'video.js/dist/types/player';
import 'video.js/dist/video-js.css';

interface VideoPlayerProps {
  src: string;
  poster?: string;
  title?: string;
}

export default function VideoPlayer({ src, poster, title }: VideoPlayerProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const playerRef = useRef<Player | null>(null);

  // Debug logging
  console.warn('VideoPlayer rendering with src:', src);

  useEffect(() => {
    // Make sure Video.js player is only initialized once
    if (!playerRef.current && videoRef.current) {
      const videoElement = videoRef.current;
      
      console.warn('Initializing Video.js player with source:', src);
      
      playerRef.current = videojs(videoElement, {
        controls: true,
        responsive: true,
        fluid: true,
        preload: 'auto',
        poster: poster,
        sources: [{
          src: src,
          type: 'video/mp4'
        }],
        controlBar: {
          volumePanel: {
            inline: false
          },
          pictureInPictureToggle: false
        }
      });

      // Add error handler
      playerRef.current.on('error', () => {
        const error = playerRef.current?.error();
        console.error('Video.js player error:', {
          code: error?.code,
          message: error?.message,
          src: src
        });
      });

      // Add custom class for styling
      playerRef.current.addClass('vjs-conduit-skin');
    } else if (playerRef.current) {
      // Update source if it changes
      playerRef.current.src({ src, type: 'video/mp4' });
    }
  }, [src, poster]);

  // Dispose the Video.js player when the component unmounts
  useEffect(() => {
    return () => {
      if (playerRef.current) {
        playerRef.current.dispose();
        playerRef.current = null;
      }
    };
  }, []);

  return (
    <div data-vjs-player>
      <video
        ref={videoRef}
        className="video-js vjs-big-play-centered"
        title={title}
      />
    </div>
  );
}