/**
 * Represents information about a database backup
 */
export interface BackupInfo {
  /** Unique identifier for the backup */
  id: string;
  /** File name of the backup */
  fileName: string;
  /** When the backup was created */
  createdAt: Date;
  /** Size of the backup file in bytes */
  sizeBytes: number;
  /** Human-readable formatted size of the backup file */
  sizeFormatted: string;
}

/**
 * Represents the result of a backup operation
 */
export interface BackupResult {
  /** Whether the backup operation was successful */
  success: boolean;
  /** Error message if the backup operation failed */
  errorMessage?: string;
  /** Backup information if the operation was successful */
  backupInfo?: BackupInfo;
}

/**
 * Represents the result of a restore operation
 */
export interface RestoreResult {
  /** Whether the restore operation was successful */
  success: boolean;
  /** Error message if the restore operation failed */
  errorMessage?: string;
  /** Additional details about the restore operation */
  details?: string;
}

/**
 * Options for backup operations
 */
export interface BackupOptions {
  /** Whether to include schema in the backup */
  includeSchema?: boolean;
  /** Whether to include data in the backup */
  includeData?: boolean;
  /** Whether to compress the backup file */
  compress?: boolean;
  /** Custom description for the backup */
  description?: string;
  /** Tags to associate with the backup */
  tags?: string[];
}

/**
 * Options for restore operations
 */
export interface RestoreOptions {
  /** Whether to overwrite existing data during restore */
  overwriteExisting?: boolean;
  /** Whether to verify the backup before restoring */
  verifyBeforeRestore?: boolean;
  /** Whether to create a backup before restoring */
  backupBeforeRestore?: boolean;
  /** Specific tables to restore (empty list means all tables) */
  specificTables?: string[];
}

/**
 * Represents backup validation results
 */
export interface BackupValidationResult {
  /** Whether the backup is valid */
  isValid: boolean;
  /** Validation errors if any */
  errors: string[];
  /** Validation warnings if any */
  warnings: string[];
  /** Metadata about the backup contents */
  metadata?: BackupMetadata;
}

/**
 * Represents metadata about backup contents
 */
export interface BackupMetadata {
  /** Database type (SQLite, PostgreSQL, etc.) */
  databaseType: string;
  /** Database version */
  databaseVersion: string;
  /** Backup format version */
  backupFormatVersion: string;
  /** List of tables included in the backup */
  tables: string[];
  /** Total number of records in the backup */
  totalRecords: number;
  /** When the backup was created */
  createdAt: Date;
}

/**
 * Represents backup storage statistics
 */
export interface BackupStorageStats {
  /** Total number of backups */
  totalBackups: number;
  /** Total storage size in bytes */
  totalSizeBytes: number;
  /** Formatted total storage size */
  totalSizeFormatted: string;
  /** Average backup size in bytes */
  averageSizeBytes: number;
  /** Formatted average backup size */
  averageSizeFormatted: string;
  /** Date of the oldest backup */
  oldestBackup?: Date;
  /** Date of the newest backup */
  newestBackup?: Date;
  /** Information about the largest backup */
  largestBackup?: BackupInfo;
}

/**
 * Response for backup download operations
 */
export interface BackupDownloadResponse {
  /** The backup file content as ArrayBuffer */
  data: ArrayBuffer;
  /** Content type of the backup file */
  contentType: string;
  /** File name of the backup */
  fileName: string;
  /** Size of the backup file in bytes */
  sizeBytes: number;
}

/**
 * Filter options for backup queries
 */
export interface BackupFilters {
  /** Start date for filtering backups */
  startDate?: Date;
  /** End date for filtering backups */
  endDate?: Date;
  /** Minimum size in bytes */
  minSizeBytes?: number;
  /** Maximum size in bytes */
  maxSizeBytes?: number;
  /** Tags to filter by */
  tags?: string[];
  /** Sort field */
  sortBy?: 'createdAt' | 'sizeBytes' | 'fileName';
  /** Sort direction */
  sortDirection?: 'asc' | 'desc';
  /** Page number for pagination */
  page?: number;
  /** Page size for pagination */
  pageSize?: number;
}

/**
 * Backup operation progress information
 */
export interface BackupProgress {
  /** Operation ID for tracking */
  operationId: string;
  /** Current operation stage */
  stage: BackupStage;
  /** Progress percentage (0-100) */
  percentage: number;
  /** Current status message */
  message: string;
  /** Whether the operation is complete */
  isComplete: boolean;
  /** Whether the operation failed */
  isFailed: boolean;
  /** Error message if operation failed */
  errorMessage?: string;
  /** Estimated time remaining in seconds */
  estimatedTimeRemaining?: number;
}

/**
 * Backup operation stages
 */
export enum BackupStage {
  /** Initializing backup operation */
  Initializing = 'initializing',
  /** Analyzing database structure */
  Analyzing = 'analyzing',
  /** Backing up schema */
  BackingUpSchema = 'backing_up_schema',
  /** Backing up data */
  BackingUpData = 'backing_up_data',
  /** Compressing backup file */
  Compressing = 'compressing',
  /** Finalizing backup */
  Finalizing = 'finalizing',
  /** Backup completed successfully */
  Completed = 'completed',
  /** Backup failed */
  Failed = 'failed'
}

/**
 * Summary of backup system status
 */
export interface BackupSystemStatus {
  /** Whether backup system is operational */
  isOperational: boolean;
  /** Last successful backup date */
  lastBackupDate?: Date;
  /** Number of available backups */
  availableBackups: number;
  /** Total storage used by backups */
  totalStorageUsed: string;
  /** Available storage space */
  availableStorage?: string;
  /** Current backup operation in progress */
  currentOperation?: BackupProgress;
  /** System health status */
  healthStatus: 'healthy' | 'warning' | 'error';
  /** Status messages or warnings */
  statusMessages: string[];
}