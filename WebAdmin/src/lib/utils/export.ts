function csvValueToString(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }

  if (Array.isArray(value)) {
    return value.map(csvValueToString).join('; ');
  }

  if (typeof value === 'object') {
    return JSON.stringify(value) ?? '';
  }

  if (typeof value === 'string') {
    return value;
  }

  if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
    return value.toString();
  }

  if (typeof value === 'symbol') {
    return value.description ?? '';
  }

  if (typeof value === 'function') {
    return value.name;
  }

  return '';
}

export function escapeCsvField(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }

  const isStringLike = typeof value === 'string' || Array.isArray(value);
  let text = csvValueToString(value);

  if (isStringLike && /^[=+\-@\t\r]/.test(text)) {
    text = `'${text}`;
  }

  if (/[,"\n\r]/.test(text)) {
    return `"${text.replace(/"/g, '""')}"`;
  }

  return text;
}

/**
 * Triggers a browser download for a Blob using the safe object-URL pattern:
 * createObjectURL -> hidden anchor -> appendChild -> click -> removeChild,
 * revoking the object URL after a short delay so the download can start.
 */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  try {
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  } finally {
    setTimeout(() => {
      URL.revokeObjectURL(url);
    }, 100);
  }
}

export function exportToCSV<T extends Record<string, unknown>>(
  data: T[],
  filename: string,
  columns?: { key: keyof T; label: string }[]
) {
  if (!data || data.length === 0) {
    return;
  }

  // If columns not specified, use all keys from first object
  const headers = columns 
    ? columns.map(col => col.label)
    : Object.keys(data[0]);
  
  const keys = columns
    ? columns.map(col => col.key)
    : Object.keys(data[0]);

  // Create CSV content
  const csvContent = [
    headers.map(escapeCsvField).join(','),
    ...data.map(row => 
      keys.map(key => escapeCsvField(row[key])).join(',')
    )
  ].join('\n');

  // Create blob and download
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  downloadBlob(blob, `${filename}.csv`);
}

export function exportToJSON<T>(data: T, filename: string) {
  const jsonContent = JSON.stringify(data, null, 2);
  const blob = new Blob([jsonContent], { type: 'application/json' });
  downloadBlob(blob, `${filename}.json`);
}

export function formatDateForExport(date: string | Date | undefined): string {
  if (!date) return '';
  const d = new Date(date);
  return d.toISOString();
}
