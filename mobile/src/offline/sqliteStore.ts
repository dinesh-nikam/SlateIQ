/**
 * SlateIQ Mobile Client — Offline SQLite Store (SLQ-ARCH-011 §4, ADR-048)
 * Manages local offline storage, encrypted credentials reference, and mutation queue
 */

import { SyncGuard } from './syncGuard';
import type { LocalExpenseDraft, LocalMutation, OfflineDeviceState } from './types';

export const SQLITE_SCHEMA_DDL = `
CREATE TABLE IF NOT EXISTS local_expense_draft (
    local_id TEXT PRIMARY KEY,
    server_expense_id TEXT,
    root_expense_id TEXT,
    show_id TEXT NOT NULL,
    department_id TEXT NOT NULL,
    category_id TEXT NOT NULL,
    vendor_id TEXT,
    total_net_amount TEXT NOT NULL,
    total_tax_amount TEXT NOT NULL,
    currency TEXT NOT NULL DEFAULT 'INR',
    expense_date TEXT NOT NULL,
    receipt_photo_uri TEXT,
    receipt_sha256 TEXT,
    sync_status TEXT NOT NULL DEFAULT 'DRAFT',
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS mutation_queue (
    mutation_id TEXT PRIMARY KEY,
    entity_type TEXT NOT NULL,
    action TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    attempt_count INTEGER NOT NULL DEFAULT 0,
    last_error TEXT,
    created_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS device_sync_state (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    last_server_sync_timestamp INTEGER NOT NULL DEFAULT 0,
    device_fingerprint TEXT NOT NULL,
    enrolled_user_id TEXT,
    enrolled_org_id TEXT
);
`;

export interface ISqlDatabase {
  execAsync(sql: string): Promise<void>;
  runAsync(sql: string, ...params: any[]): Promise<{ rowsAffected: number; lastInsertRowId?: number }>;
  getFirstAsync<T>(sql: string, ...params: any[]): Promise<T | null>;
  getAllAsync<T>(sql: string, ...params: any[]): Promise<T[]>;
}

export class OfflineSqliteStore {
  constructor(private readonly db: ISqlDatabase) {}

  /**
   * Initializes the SQLite schema and ensures singleton sync state row exists.
   */
  public async initialize(): Promise<void> {
    await this.db.execAsync(SQLITE_SCHEMA_DDL);
    const existing = await this.db.getFirstAsync<any>('SELECT id FROM device_sync_state WHERE id = 1;');
    if (!existing) {
      await this.db.runAsync(
        'INSERT INTO device_sync_state (id, last_server_sync_timestamp, device_fingerprint) VALUES (1, ?, ?);',
        Date.now(),
        'dev-fingerprint-001'
      );
    }
  }

  /**
   * Reads current offline state to evaluate sync guard boundaries.
   */
  public async getDeviceState(): Promise<OfflineDeviceState> {
    const row = await this.db.getFirstAsync<{ last_server_sync_timestamp: number }>(
      'SELECT last_server_sync_timestamp FROM device_sync_state WHERE id = 1;'
    );
    const queueRow = await this.db.getFirstAsync<{ count: number }>(
      'SELECT COUNT(1) as count FROM mutation_queue;'
    );

    const lastSync = row?.last_server_sync_timestamp ?? 0;
    const queueCount = queueRow?.count ?? 0;
    const guardCheck = SyncGuard.canCaptureOffline({
      lastServerSyncTimestamp: lastSync,
      offlineQueueCount: queueCount,
      isLockedDueToOfflineWindow: false,
    });

    return {
      lastServerSyncTimestamp: lastSync,
      offlineQueueCount: queueCount,
      isLockedDueToOfflineWindow: !guardCheck.allowed && guardCheck.reason === 'SEVEN_DAY_WINDOW_EXCEEDED',
    };
  }

  /**
   * Saves a new expense draft and queues it for synchronization, guarded by 7-day & 500-item limits.
   */
  public async enqueueExpenseDraft(draft: LocalExpenseDraft): Promise<void> {
    const deviceState = await this.getDeviceState();
    const guard = SyncGuard.canCaptureOffline(deviceState);

    if (!guard.allowed) {
      if (guard.reason === 'SEVEN_DAY_WINDOW_EXCEEDED') {
        throw new Error('OFFLINE_WINDOW_EXCEEDED: Device has been offline for over 7 days. Sync required before new entries (NFR-MOB-004).');
      }
      if (guard.reason === 'QUEUE_CAP_REACHED') {
        throw new Error('QUEUE_CAP_REACHED: Offline queue reached maximum capacity of 500 entries (D-015).');
      }
    }

    // 1. Insert/Update local draft
    await this.db.runAsync(
      `INSERT INTO local_expense_draft (
        local_id, server_expense_id, root_expense_id, show_id, department_id,
        category_id, vendor_id, total_net_amount, total_tax_amount, currency,
        expense_date, receipt_photo_uri, receipt_sha256, sync_status, created_at, updated_at
      ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'QUEUED', ?, ?)
      ON CONFLICT(local_id) DO UPDATE SET
        total_net_amount = excluded.total_net_amount,
        total_tax_amount = excluded.total_tax_amount,
        sync_status = 'QUEUED',
        updated_at = excluded.updated_at;`,
      draft.localId,
      draft.serverExpenseId ?? null,
      draft.rootExpenseId ?? null,
      draft.showId,
      draft.departmentId,
      draft.categoryId,
      draft.vendorId ?? null,
      draft.totalNetAmount,
      draft.totalTaxAmount,
      draft.currency,
      draft.expenseDate,
      draft.receiptPhotoUri ?? null,
      draft.receiptSha256 ?? null,
      draft.createdAt,
      draft.updatedAt
    );

    // 2. Enqueue mutation
    const mutation: LocalMutation = {
      mutationId: `mut_${draft.localId}`,
      entityType: 'EXPENSE',
      action: draft.serverExpenseId ? 'AMEND' : 'CREATE',
      payloadJson: JSON.stringify(draft),
      attemptCount: 0,
      createdAt: Date.now(),
    };

    await this.db.runAsync(
      `INSERT INTO mutation_queue (mutation_id, entity_type, action, payload_json, attempt_count, created_at)
       VALUES (?, ?, ?, ?, 0, ?);`,
      mutation.mutationId,
      mutation.entityType,
      mutation.action,
      mutation.payloadJson,
      mutation.createdAt
    );
  }

  /**
   * Fetches pending mutations ordered by creation time (FIFO).
   */
  public async getPendingMutations(limit: number = 50): Promise<LocalMutation[]> {
    return this.db.getAllAsync<LocalMutation>(
      'SELECT mutation_id as mutationId, entity_type as entityType, action, payload_json as payloadJson, attempt_count as attemptCount, last_error as lastError, created_at as createdAt FROM mutation_queue ORDER BY created_at ASC LIMIT ?;',
      limit
    );
  }

  /**
   * Removes mutation after successful server replication.
   */
  public async completeMutation(mutationId: string, localId: string, serverExpenseId: string): Promise<void> {
    await this.db.runAsync('DELETE FROM mutation_queue WHERE mutation_id = ?;', mutationId);
    await this.db.runAsync(
      "UPDATE local_expense_draft SET sync_status = 'SYNCED', server_expense_id = ?, updated_at = ? WHERE local_id = ?;",
      serverExpenseId,
      Date.now(),
      localId
    );
  }

  /**
   * Records a successful sync heartbeat.
   */
  public async updateLastSyncTimestamp(timestamp: number = Date.now()): Promise<void> {
    await this.db.runAsync(
      'UPDATE device_sync_state SET last_server_sync_timestamp = ? WHERE id = 1;',
      timestamp
    );
  }
}
