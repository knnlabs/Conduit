'use client';

import { useState } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
  FileInput,
  Group,
  List,
  LoadingOverlay,
  Modal,
  ScrollArea,
  Stack,
  Table,
  Text,
} from '@mantine/core';
import { IconAlertCircle, IconBraces, IconCheck } from '@tabler/icons-react';
import { withAdminClient } from '@/lib/client/adminClient';
import { notify } from '@/lib/notifications';
import type { ProviderToolImportResult } from '@/lib/admin-api';
import {
  LARGE_PROVIDER_TOOL_IMPORT_ROWS,
  parseProviderToolImport,
  type ProviderToolImportPreview,
} from './providerToolImport';

interface ImportProviderToolsModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

const PREVIEW_ROW_LIMIT = 100;

export function ImportProviderToolsModal({
  isOpen,
  onClose,
  onSuccess,
}: ImportProviderToolsModalProps) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ProviderToolImportPreview | null>(null);
  const [parseError, setParseError] = useState<string | null>(null);
  const [isParsing, setIsParsing] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [result, setResult] = useState<ProviderToolImportResult | null>(null);

  const reset = () => {
    setFile(null);
    setPreview(null);
    setParseError(null);
    setResult(null);
    setIsParsing(false);
    setIsImporting(false);
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const handleFileChange = async (selectedFile: File | null) => {
    setFile(selectedFile);
    setPreview(null);
    setParseError(null);
    setResult(null);

    if (!selectedFile) return;

    setIsParsing(true);
    try {
      setPreview(parseProviderToolImport(await selectedFile.text()));
    } catch (error) {
      setParseError(error instanceof Error ? error.message : 'Unable to read the selected file.');
    } finally {
      setIsParsing(false);
    }
  };

  const handleImport = async () => {
    if (!preview?.validTools.length) return;

    setIsImporting(true);
    try {
      const importResult = await withAdminClient(client =>
        client.providerTools.importProviderTools(preview.validTools),
      );
      setResult(importResult);

      if ((importResult.imported ?? 0) > 0) {
        onSuccess();
        notify.success(
          `Imported ${importResult.imported ?? 0} provider tool${importResult.imported === 1 ? '' : 's'}.`,
          'Import complete',
        );
      }
    } catch (error) {
      notify.error(error, 'Failed to import provider tools');
    } finally {
      setIsImporting(false);
    }
  };

  const invalidCount = preview ? preview.rows.length - preview.validTools.length : 0;
  const visibleRows = preview?.rows.slice(0, PREVIEW_ROW_LIMIT) ?? [];

  return (
    <Modal opened={isOpen} onClose={handleClose} title="Import Provider Tools" size="xl">
      <Stack gap="md">
        <Alert color="blue" icon={<IconBraces size={16} />}>
          Select a JSON array exported from Provider Tools. Records are validated locally before
          anything is sent to the server.
        </Alert>

        <FileInput
          label="Select JSON file"
          placeholder="Choose a .json file"
          accept="application/json,.json"
          leftSection={<IconBraces size={16} />}
          value={file}
          onChange={selectedFile => void handleFileChange(selectedFile)}
          disabled={isImporting}
        />

        <LoadingOverlay visible={isParsing} />

        {parseError && (
          <Alert color="red" icon={<IconAlertCircle size={16} />}>
            {parseError}
          </Alert>
        )}

        {preview && (
          <>
            <Card withBorder>
              <Group justify="space-between">
                <Text fw={600}>{preview.rows.length.toLocaleString()} records found</Text>
                <Group gap="xs">
                  <Badge color="green">{preview.validTools.length} valid</Badge>
                  {invalidCount > 0 && <Badge color="red">{invalidCount} invalid</Badge>}
                  {preview.duplicateCount > 0 && (
                    <Badge color="orange">{preview.duplicateCount} duplicates</Badge>
                  )}
                </Group>
              </Group>
            </Card>

            {preview.rows.length >= LARGE_PROVIDER_TOOL_IMPORT_ROWS && (
              <Alert color="yellow" title="Large import">
                Validation is complete. Importing {preview.validTools.length.toLocaleString()} valid
                records may take a while; keep this window open until the result appears.
              </Alert>
            )}

            <ScrollArea h={300}>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Row</Table.Th>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Tool name</Table.Th>
                    <Table.Th>Status</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {visibleRows.map(row => (
                    <Table.Tr key={row.rowNumber}>
                      <Table.Td>{row.rowNumber}</Table.Td>
                      <Table.Td>{row.providerLabel}</Table.Td>
                      <Table.Td>{row.toolName}</Table.Td>
                      <Table.Td>
                        {row.errors.length === 0 ? (
                          <Badge color="green" variant="light">Valid</Badge>
                        ) : (
                          <Text c="red" size="sm">{row.errors.join(' ')}</Text>
                        )}
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>

            {preview.rows.length > PREVIEW_ROW_LIMIT && (
              <Text c="dimmed" size="xs">
                Showing the first {PREVIEW_ROW_LIMIT} records. All {preview.rows.length.toLocaleString()} records were validated.
              </Text>
            )}

            {invalidCount > 0 && (
              <Alert color="yellow" icon={<IconAlertCircle size={16} />}>
                Invalid and duplicate records will not be submitted. Correct the file and select it
                again if you want to import those records.
              </Alert>
            )}
          </>
        )}

        {result && (
          <Alert color={(result.errors?.length ?? 0) > 0 ? 'yellow' : 'green'} icon={<IconCheck size={16} />} title="Import result">
            <Group gap="xs" mb={result.errors?.length ? 'xs' : 0}>
              <Badge color="green">{result.imported ?? 0} imported</Badge>
              <Badge color="orange">{result.skipped ?? 0} skipped</Badge>
              <Badge color="gray">{result.total ?? preview?.validTools.length ?? 0} submitted</Badge>
            </Group>
            {result.errors && result.errors.length > 0 && (
              <List size="sm">
                {result.errors.map((error, index) => <List.Item key={`${index}-${error}`}>{error}</List.Item>)}
              </List>
            )}
          </Alert>
        )}

        <Group justify="flex-end">
          <Button variant="subtle" onClick={handleClose} disabled={isImporting}>
            {result ? 'Done' : 'Cancel'}
          </Button>
          <Button
            onClick={() => void handleImport()}
            loading={isImporting}
            disabled={!preview?.validTools.length || result !== null}
          >
            Confirm import{preview?.validTools.length ? ` (${preview.validTools.length})` : ''}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
