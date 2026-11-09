// mobile/src/offline/types.ts
// Offline SQLite schema and mutation queue types (SLQ-ARCH-011 §4)

export interface LocalExpenseDraft {
  localId: string;
  serverExpenseId?: string;
  rootExpenseId?: string;
  showId: string;
  departmentId: string;
  categoryId: string;
  vendorId?: string;
  totalNetAmount: string; // Scale 4
  totalTaxAmount: string; // Scale 4
  currency: string;
  expenseDate: string; // YYYY-MM-DD
  receiptPhotoUri?: string;
  receiptSha256?: string;
  syncStatus: 'DRAFT' | 'QUEUED' | 'SYNCING' | 'SYNCED' | 'FAILED_RETRYABLE' | 'CONFLICT';
  createdAt: number; // Unix timestamp ms
  updatedAt: number;
}

export interface LocalMutation {
  mutationId: string;
  entityType: 'EXPENSE' | 'EVIDENCE' | 'REPORT';
  action: 'CREATE' | 'AMEND' | 'ATTACH';
  payloadJson: string;
  attemptCount: number;
  lastError?: string;
  createdAt: number;
}

export interface OfflineDeviceState {
  lastServerSyncTimestamp: number;
  offlineQueueCount: number; // Max 500 items (D-015)
  isLockedDueToOfflineWindow: boolean; // > 7 days triggers hard lock (NFR-MOB-004)
}
