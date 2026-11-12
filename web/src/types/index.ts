export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  activeTenantId: string;
  roles: string[];
}

export interface Money {
  amount: string;
  currency: string;
}

export interface ShowSummary {
  id: string;
  code: string;
  title: string;
  status: 'PLANNING' | 'ACTIVE' | 'COMPLETED' | 'CLOSING' | 'CLOSED' | 'ARCHIVED';
  totalBudget: Money;
  totalActual: Money;
  totalCommitted: Money;
  variance: Money;
}

export type FindingSeverity = 'CRITICAL' | 'HIGH' | 'MEDIUM' | 'LOW';
export type FindingStatus = 'DRAFT' | 'OPEN' | 'UNDER_REVIEW' | 'RESOLVED' | 'CLOSED' | 'WAIVED';

export interface AuditFinding {
  id: string;
  findingNumber: string;
  showId: string;
  showCode: string;
  severity: FindingSeverity;
  category: string;
  title: string;
  description: string;
  visibility: 'SHARED_WITH_PRODUCTION' | 'INTERNAL_AUDIT_ONLY';
  status: FindingStatus;
  department?: string;
  linkedEntityType?: string;
  linkedEntityRef?: string;
  identifiedDate: string;
  dueDate?: string;
  resolutionNotes?: string;
}

export type BatchStatus = 'UPLOADING' | 'VALIDATING' | 'PREVIEW_READY' | 'COMMITTED' | 'REJECTED';

export interface ExcelImportBatch {
  id: string;
  showCode: string;
  templateType: 'DAILY_EXPENSE' | 'BUDGET_LINES' | 'VENDOR_DIRECTORY';
  filename: string;
  fileSizeBytes: number;
  totalRows: number;
  validRows: number;
  errorRows: number;
  status: BatchStatus;
  uploadedAt: string;
  completedAt?: string;
}

export interface ExcelRowError {
  rowNumber: number;
  fieldName: string;
  errorCode: string;
  rawValue: string;
  errorMessage: string;
}

export type VendorStatus = 'CANDIDATE' | 'ACTIVE' | 'BLOCKED' | 'MERGED';

export interface VendorSummary {
  id: string;
  vendorCode: string;
  legalName: string;
  tradeName?: string;
  panNumber: string;
  gstin?: string;
  msmeCategory?: 'MICRO' | 'SMALL' | 'MEDIUM' | 'NOT_APPLICABLE';
  riskScore: number;
  status: VendorStatus;
  totalCommitted: string;
  totalPaid: string;
}

export interface CommitmentSummary {
  id: string;
  commitmentNumber: string;
  showCode: string;
  vendorName: string;
  department: string;
  originalAmount: string;
  consumedAmount: string;
  remainingAmount: string;
  currency: string;
  status: 'DRAFT' | 'ACTIVE' | 'FULFILLED' | 'CANCELLED';
}
