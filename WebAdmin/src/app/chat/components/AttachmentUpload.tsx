'use client';

import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  FileButton,
  Group,
  Image,
  Modal,
  Paper,
  Select,
  Stack,
  Text,
  TextInput,
  Tooltip,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import {
  IconFile,
  IconFileTypePdf,
  IconHeadphones,
  IconLink,
  IconPhoto,
  IconPlus,
  IconVideo,
  IconX,
} from '@tabler/icons-react';
import { useEffect, useRef, useState } from 'react';
import { formatters } from '@/lib/utils/formatters';

import type { ChatAttachment } from '../types';

type AttachmentKind = NonNullable<ChatAttachment['kind']>;
type PdfParser = NonNullable<ChatAttachment['parser']>;

const MAX_BYTES: Record<AttachmentKind, number> = {
  image: 10 * 1024 * 1024,
  pdf: 20 * 1024 * 1024,
  audio: 10 * 1024 * 1024,
  video: 20 * 1024 * 1024,
};

const MIME_BY_KIND: Record<AttachmentKind, string> = {
  image: 'image/jpeg',
  pdf: 'application/pdf',
  audio: 'audio/wav',
  video: 'video/mp4',
};

function inferKind(file: Pick<File, 'type' | 'name'>): AttachmentKind | null {
  if (file.type === 'application/pdf' || file.name.toLowerCase().endsWith('.pdf')) return 'pdf';
  if (file.type.startsWith('image/')) return 'image';
  if (file.type.startsWith('audio/')) return 'audio';
  if (file.type.startsWith('video/')) return 'video';
  return null;
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error ?? new Error('Unable to read attachment'));
    reader.onload = () => {
      const result = reader.result;
      if (typeof result !== 'string') {
        reject(new Error('Attachment did not produce a data URL'));
        return;
      }
      resolve(result.slice(result.indexOf(',') + 1));
    };
    reader.readAsDataURL(file);
  });
}

function KindIcon({ kind, size = 18 }: { kind: AttachmentKind; size?: number }) {
  if (kind === 'image') return <IconPhoto size={size} />;
  if (kind === 'pdf') return <IconFileTypePdf size={size} />;
  if (kind === 'audio') return <IconHeadphones size={size} />;
  if (kind === 'video') return <IconVideo size={size} />;
  return <IconFile size={size} />;
}

export function AttachmentPreview({
  attachments,
  onRemove,
}: {
  attachments: ChatAttachment[];
  onRemove?: (index: number) => void;
}) {
  return (
    <Group gap="xs">
      {attachments.map((attachment, index) => {
        const kind = attachment.kind ?? 'image';
        return (
          <Paper key={`${attachment.name}-${index}`} p="xs" withBorder radius="sm">
            <Group gap="xs" wrap="nowrap">
              {kind === 'image' && attachment.url ? (
                <Image src={attachment.url} alt={attachment.name} w={40} h={40} radius="sm" fit="cover" />
              ) : (
                <KindIcon kind={kind} />
              )}
              <div>
                <Text size="xs" fw={500} lineClamp={1}>{attachment.name}</Text>
                <Badge size="xs" variant="light">{kind}</Badge>
              </div>
              {onRemove && (
                <ActionIcon
                  size="sm"
                  variant="subtle"
                  color="red"
                  aria-label={`Remove ${attachment.name}`}
                  onClick={() => onRemove(index)}
                >
                  <IconX size={14} />
                </ActionIcon>
              )}
            </Group>
          </Paper>
        );
      })}
    </Group>
  );
}

interface AttachmentUploadProps {
  attachments: ChatAttachment[];
  onAttachmentsChange: (attachments: ChatAttachment[]) => void;
  disabled?: boolean;
  supportsImage: boolean;
  supportsPdf: boolean;
  supportsAudio: boolean;
  supportsVideo: boolean;
}

export function AttachmentUpload({
  attachments,
  onAttachmentsChange,
  disabled,
  supportsImage,
  supportsPdf,
  supportsAudio,
  supportsVideo,
}: AttachmentUploadProps) {
  const [urlOpened, { open: openUrl, close: closeUrl }] = useDisclosure(false);
  const [url, setUrl] = useState('');
  const [urlKind, setUrlKind] = useState<AttachmentKind>('image');
  const [urlName, setUrlName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const objectUrls = useRef(new Set<string>());

  useEffect(() => () => {
    objectUrls.current.forEach((value) => URL.revokeObjectURL(value));
  }, []);

  const supportedKinds = [
    supportsImage && { value: 'image', label: 'Image' },
    supportsPdf && { value: 'pdf', label: 'PDF' },
    supportsAudio && { value: 'audio', label: 'Audio' },
    supportsVideo && { value: 'video', label: 'Video' },
  ].filter(Boolean) as Array<{ value: string; label: string }>;
  // OpenRouter accepts audio as raw base64 rather than as a remote URL.
  const urlKinds = supportedKinds.filter(({ value }) => value !== 'audio');

  const addFiles = async (files: File[]) => {
    setError(null);
    const additions: ChatAttachment[] = [];
    const supportedByKind: Record<AttachmentKind, boolean> = {
      image: supportsImage,
      pdf: supportsPdf,
      audio: supportsAudio,
      video: supportsVideo,
    };
    for (const file of files) {
      const kind = inferKind(file);
      const supported = kind === null ? false : supportedByKind[kind];
      if (!kind || !supported) {
        setError(`${file.name} is not supported by the selected model.`);
        continue;
      }
      if (file.size > MAX_BYTES[kind]) {
        setError(
          `${file.name} exceeds the ${formatters.fileSize(MAX_BYTES[kind], { binary: true })} ${kind} limit.`,
        );
        continue;
      }

      const previewUrl = URL.createObjectURL(file);
      objectUrls.current.add(previewUrl);
      additions.push({
        kind,
        url: previewUrl,
        base64: await fileToBase64(file),
        mimeType: file.type === '' ? MIME_BY_KIND[kind] : file.type,
        size: file.size,
        name: file.name,
        detail: kind === 'image' ? 'auto' : undefined,
        parser: kind === 'pdf' ? 'auto' : undefined,
      });
    }
    if (additions.length) onAttachmentsChange([...attachments, ...additions]);
  };

  const remove = (index: number) => {
    const removed = attachments[index];
    if (removed?.url.startsWith('blob:')) {
      URL.revokeObjectURL(removed.url);
      objectUrls.current.delete(removed.url);
    }
    onAttachmentsChange(attachments.filter((attachment) => attachment !== removed));
  };

  const addUrl = () => {
    if (!url.startsWith('https://')) {
      setError('Attachment URLs must use HTTPS.');
      return;
    }
    const requestedName = urlName.trim();
    const pathName = url.split('/').pop()?.split('?')[0];
    let displayName = `${urlKind} URL`;
    if (pathName) displayName = pathName;
    if (requestedName) displayName = requestedName;

    onAttachmentsChange([
      ...attachments,
      {
        kind: urlKind,
        url,
        mimeType: MIME_BY_KIND[urlKind],
        size: 0,
        name: displayName,
        detail: urlKind === 'image' ? 'auto' : undefined,
        parser: urlKind === 'pdf' ? 'auto' : undefined,
      },
    ]);
    setUrl('');
    setUrlName('');
    closeUrl();
  };

  const parser = attachments.find((attachment) => attachment.kind === 'pdf')?.parser ?? 'auto';
  const setParser = (value: string | null) => {
    const selected = (value ?? 'auto') as PdfParser;
    onAttachmentsChange(attachments.map((attachment) =>
      attachment.kind === 'pdf' ? { ...attachment, parser: selected } : attachment));
  };

  if (supportedKinds.length === 0) return null;

  return (
    <Stack gap="xs">
      <Group gap="xs">
        <FileButton
          multiple
          accept="image/png,image/jpeg,image/webp,image/gif,application/pdf,audio/*,video/mp4,video/mpeg,video/quicktime,video/webm"
          onChange={(files) => void addFiles(files)}
          disabled={disabled}
        >
          {(props) => (
            <Button {...props} size="xs" variant="light" leftSection={<IconPlus size={14} />}>
              Attach
            </Button>
          )}
        </FileButton>
        <Tooltip label="Attach a public HTTPS URL">
          <Button
            size="xs"
            variant="default"
            leftSection={<IconLink size={14} />}
            onClick={openUrl}
            disabled={disabled ? true : urlKinds.length === 0}
          >
            URL
          </Button>
        </Tooltip>
      </Group>

      {attachments.length > 0 && <AttachmentPreview attachments={attachments} onRemove={remove} />}

      {attachments.some((attachment) => attachment.kind === 'pdf') && (
        <>
          <Select
            label="PDF parser"
            size="xs"
            value={parser}
            onChange={setParser}
            data={[
              { value: 'auto', label: 'Auto (native, then Cloudflare AI)' },
              { value: 'native', label: 'Native model processing' },
              { value: 'cloudflare-ai', label: 'Cloudflare AI (free text extraction)' },
              { value: 'mistral-ocr', label: 'Mistral OCR (paid per page)' },
            ]}
          />
          {parser === 'mistral-ocr' && (
            <Alert color="yellow" p="xs">
              <Text size="xs">Mistral OCR adds OpenRouter per-page charges, including for BYOK requests.</Text>
            </Alert>
          )}
        </>
      )}

      {error && <Alert color="red" p="xs"><Text size="xs">{error}</Text></Alert>}

      <Modal opened={urlOpened} onClose={closeUrl} title="Attach URL" size="sm">
        <Stack>
          <Select
            label="Attachment type"
            data={urlKinds}
            value={urlKind}
            onChange={(value) => setUrlKind((value ?? urlKinds[0]?.value ?? 'image') as AttachmentKind)}
          />
          <TextInput label="HTTPS URL" value={url} onChange={(event) => setUrl(event.currentTarget.value)} />
          <TextInput label="Display name" value={urlName} onChange={(event) => setUrlName(event.currentTarget.value)} />
          <Group justify="flex-end">
            <Button variant="default" onClick={closeUrl}>Cancel</Button>
            <Button onClick={addUrl}>Attach</Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}
