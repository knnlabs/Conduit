'use client';

import React from 'react';
import { Button, type ButtonProps } from '@mantine/core';
import { IconDownload } from '@tabler/icons-react';
import { downloadMedia, type MediaDownloadOptions } from '../utils/download';

export interface MediaDownloaderProps extends Omit<ButtonProps, 'onClick'> {
  /** Download options for the media */
  downloadOptions: MediaDownloadOptions;
  /** Icon size for the download icon */
  iconSize?: number;
  /** Whether to show the icon */
  showIcon?: boolean;
  /** Custom onClick handler (called after download starts) */
  onDownload?: () => void;
  /** Custom error handler */
  onError?: (error: Error) => void;
}

/**
 * Component for downloading media files with a button interface
 */
export function MediaDownloader({
  downloadOptions,
  iconSize = 16,
  showIcon = true,
  onDownload,
  onError,
  children = 'Download',
  leftSection,
  disabled,
  ...buttonProps
}: MediaDownloaderProps) {
  const handleDownload = async () => {
    try {
      await downloadMedia(downloadOptions);
      onDownload?.();
    } catch (error) {
      onError?.(error instanceof Error ? error : new Error('Download failed'));
    }
  };

  const isDisabled = disabled ?? (!downloadOptions.url && !downloadOptions.b64_json);

  return (
    <Button
      onClick={() => void handleDownload()}
      leftSection={leftSection ?? (showIcon ? <IconDownload size={iconSize} /> : undefined)}
      disabled={isDisabled}
      {...buttonProps}
    >
      {children}
    </Button>
  );
}

export default MediaDownloader;