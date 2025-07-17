'use client';

import { useState } from 'react';
import {
  Modal,
  Stack,
  Alert,
  FileInput,
  Table,
  ScrollArea,
  Button,
  Group,
  Text,
  Badge,
  LoadingOverlay,
  Center,
  Card,
} from '@mantine/core';
import { IconUpload, IconFileTypeCsv, IconAlertCircle, IconCheck } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { useModelCostsApi } from '../hooks/useModelCostsApi';
import { CreateModelCostDto } from '../types/modelCost';
import { parseCSVContent, convertParsedToDto, ParsedModelCost } from '../utils/csvHelpers';

interface ImportModelCostsModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}


export function ImportModelCostsModal({ isOpen, onClose, onSuccess }: ImportModelCostsModalProps) {
  const { importModelCosts } = useModelCostsApi();
  const [file, setFile] = useState<File | null>(null);
  const [parsedData, setParsedData] = useState<ParsedModelCost[]>([]);
  const [isParsing, setIsParsing] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [parseError, setParseError] = useState<string | null>(null);

  const parseCSV = async (csvFile: File) => {
    setIsParsing(true);
    setParseError(null);
    
    try {
      const text = await csvFile.text();
      const parsed = parseCSVContent(text);
      setParsedData(parsed);
    } catch (error) {
      setParseError(error instanceof Error ? error.message : 'Failed to parse CSV file');
      setParsedData([]);
    } finally {
      setIsParsing(false);
    }
  };


  const handleFileChange = (newFile: File | null) => {
    setFile(newFile);
    setParsedData([]);
    setParseError(null);
    
    if (newFile) {
      parseCSV(newFile);
    }
  };

  const handleImport = async () => {
    const validData = parsedData.filter(d => d.isValid);
    if (validData.length === 0) {
      notifications.show({
        title: 'Error',
        message: 'No valid data to import',
        color: 'red',
      });
      return;
    }

    setIsImporting(true);
    
    try {
      const modelCosts = convertParsedToDto(validData);
      await importModelCosts(modelCosts);
      
      onSuccess?.();
      onClose();
      setFile(null);
      setParsedData([]);
    } catch (error) {
      // Error handling is done in the hook
    } finally {
      setIsImporting(false);
    }
  };

  const validCount = parsedData.filter(d => d.isValid).length;
  const invalidCount = parsedData.filter(d => !d.isValid).length;

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Import Model Pricing from CSV"
      size="xl"
    >
      <Stack gap="md">
        <Alert icon={<IconAlertCircle size={16} />} color="blue">
          Upload a CSV file with model pricing data. The file should include columns for:
          Model Pattern, Provider, Model Type, and relevant cost fields.
          Download the current data as CSV to see the expected format.
        </Alert>

        <FileInput
          label="Select CSV File"
          placeholder="Click to browse files"
          leftSection={<IconFileTypeCsv size={16} />}
          accept=".csv"
          value={file}
          onChange={handleFileChange}
        />

        {parseError && (
          <Alert color="red" icon={<IconAlertCircle size={16} />}>
            {parseError}
          </Alert>
        )}

        {parsedData.length > 0 && (
          <>
            <Card withBorder>
              <Group justify="space-between">
                <Text fw={600}>Import Preview</Text>
                <Group gap="xs">
                  {validCount > 0 && (
                    <Badge color="green" variant="light">
                      {validCount} valid
                    </Badge>
                  )}
                  {invalidCount > 0 && (
                    <Badge color="red" variant="light">
                      {invalidCount} invalid
                    </Badge>
                  )}
                </Group>
              </Group>
            </Card>

            <ScrollArea h={300}>
              <LoadingOverlay visible={isParsing} />
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th w={40}></Table.Th>
                    <Table.Th>Model Pattern</Table.Th>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Type</Table.Th>
                    <Table.Th>Input/Output Cost</Table.Th>
                    <Table.Th>Priority</Table.Th>
                    <Table.Th>Status</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {parsedData.map((cost, index) => (
                    <Table.Tr key={index} style={{ 
                      backgroundColor: cost.isValid ? undefined : 'var(--mantine-color-red-0)' 
                    }}>
                      <Table.Td>
                        {cost.isValid ? (
                          <IconCheck size={16} color="green" />
                        ) : (
                          <IconAlertCircle size={16} color="red" />
                        )}
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{cost.modelPattern}</Text>
                        {!cost.isValid && (
                          <Text size="xs" c="red">
                            {cost.errors.join(', ')}
                          </Text>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <Badge variant="light" size="sm">{cost.provider}</Badge>
                      </Table.Td>
                      <Table.Td>
                        <Badge variant="outline" size="sm">{cost.modelType}</Badge>
                      </Table.Td>
                      <Table.Td>
                        {cost.modelType === 'chat' && (
                          <Text size="xs">
                            ${cost.inputCostPer1K}/1K • ${cost.outputCostPer1K}/1K
                          </Text>
                        )}
                        {cost.modelType === 'image' && cost.imageCostPerImage && (
                          <Text size="xs">${cost.imageCostPerImage}/image</Text>
                        )}
                        {cost.modelType === 'video' && cost.videoCostPerSecond && (
                          <Text size="xs">${cost.videoCostPerSecond}/sec</Text>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{cost.priority}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Badge 
                          size="sm" 
                          variant="light"
                          color={cost.active ? 'green' : 'gray'}
                        >
                          {cost.active ? 'Active' : 'Inactive'}
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>

            {validCount > 0 && (
              <Alert icon={<IconCheck size={16} />} color="green">
                Ready to import {validCount} valid pricing configuration{validCount !== 1 ? 's' : ''}.
                This will create new entries or update existing ones based on model pattern.
              </Alert>
            )}
          </>
        )}

        <Group justify="flex-end" mt="md">
          <Button variant="subtle" onClick={onClose}>
            Cancel
          </Button>
          <Button 
            onClick={handleImport}
            loading={isImporting}
            disabled={validCount === 0}
          >
            Import {validCount > 0 ? `${validCount} Items` : ''}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}