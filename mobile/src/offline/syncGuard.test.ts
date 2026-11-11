import { describe, it, expect } from 'vitest';
import { SyncGuard, SEVEN_DAYS_MS, MAX_OFFLINE_QUEUE_ITEMS } from './syncGuard';
import { OfflineSqliteStore, type ISqlDatabase } from './sqliteStore';
import type { LocalExpenseDraft } from './types';

class MockSqlDatabase implements ISqlDatabase {
  private tables: Record<string, any[]> = {
    local_expense_draft: [],
    mutation_queue: [],
    device_sync_state: [],
  };

  async execAsync(_sql: string): Promise<void> {}

  async runAsync(sql: string, ...params: any[]): Promise<{ rowsAffected: number; lastInsertRowId?: number }> {
    if (sql.includes('INSERT INTO device_sync_state')) {
      this.tables.device_sync_state = [{ id: 1, last_server_sync_timestamp: params[0], device_fingerprint: params[1] }];
    } else if (sql.includes('UPDATE device_sync_state')) {
      if (this.tables.device_sync_state[0]) {
        this.tables.device_sync_state[0].last_server_sync_timestamp = params[0];
      }
    } else if (sql.includes('INSERT INTO local_expense_draft')) {
      this.tables.local_expense_draft.push({ local_id: params[0], ...params });
    } else if (sql.includes('INSERT INTO mutation_queue')) {
      this.tables.mutation_queue.push({
        mutation_id: params[0],
        entity_type: params[1],
        action: params[2],
        payload_json: params[3],
        attempt_count: 0,
        created_at: params[4],
      });
    } else if (sql.includes('DELETE FROM mutation_queue')) {
      this.tables.mutation_queue = this.tables.mutation_queue.filter((m) => m.mutation_id !== params[0]);
    }
    return { rowsAffected: 1 };
  }

  async getFirstAsync<T>(sql: string, ..._params: any[]): Promise<T | null> {
    if (sql.includes('FROM device_sync_state')) {
      return (this.tables.device_sync_state[0] as T) || null;
    }
    if (sql.includes('COUNT(1) as count FROM mutation_queue')) {
      return { count: this.tables.mutation_queue.length } as unknown as T;
    }
    return null;
  }

  async getAllAsync<T>(sql: string, ..._params: any[]): Promise<T[]> {
    if (sql.includes('FROM mutation_queue')) {
      return this.tables.mutation_queue.map((m) => ({
        mutationId: m.mutation_id,
        entityType: m.entity_type,
        action: m.action,
        payloadJson: m.payload_json,
        attemptCount: m.attempt_count,
        createdAt: m.created_at,
      })) as unknown as T[];
    }
    return [];
  }
}

describe('SyncGuard (NFR-MOB-004, TS-29, TS-30)', () => {
  it('allows offline capture within 7 days and under 500 queued items', () => {
    const now = Date.now();
    const result = SyncGuard.canCaptureOffline(
      {
        lastServerSyncTimestamp: now - 3 * 24 * 60 * 60 * 1000, // 3 days ago
        offlineQueueCount: 42,
        isLockedDueToOfflineWindow: false,
      },
      now
    );

    expect(result.allowed).toBe(true);
  });

  it('blocks offline capture when offline for more than 7 days (TS-29)', () => {
    const now = Date.now();
    const result = SyncGuard.canCaptureOffline(
      {
        lastServerSyncTimestamp: now - (SEVEN_DAYS_MS + 1000), // 7 days + 1 second
        offlineQueueCount: 10,
        isLockedDueToOfflineWindow: false,
      },
      now
    );

    expect(result.allowed).toBe(false);
    expect(result.reason).toBe('SEVEN_DAY_WINDOW_EXCEEDED');
  });

  it('blocks offline capture when queue reaches 500 items (TS-30, D-015)', () => {
    const now = Date.now();
    const result = SyncGuard.canCaptureOffline(
      {
        lastServerSyncTimestamp: now - 1000,
        offlineQueueCount: MAX_OFFLINE_QUEUE_ITEMS,
        isLockedDueToOfflineWindow: false,
      },
      now
    );

    expect(result.allowed).toBe(false);
    expect(result.reason).toBe('QUEUE_CAP_REACHED');
  });
});

describe('OfflineSqliteStore', () => {
  it('enqueues an expense draft and mutation when conditions are healthy', async () => {
    const db = new MockSqlDatabase();
    const store = new OfflineSqliteStore(db);
    await store.initialize();

    const draft: LocalExpenseDraft = {
      localId: 'draft-001',
      showId: 'show-100',
      departmentId: 'dept-camera',
      categoryId: 'cat-lenses',
      totalNetAmount: '15000.0000',
      totalTaxAmount: '2700.0000',
      currency: 'INR',
      expenseDate: '2026-11-11',
      syncStatus: 'DRAFT',
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };

    await store.enqueueExpenseDraft(draft);

    const pending = await store.getPendingMutations();
    expect(pending.length).toBe(1);
    expect(pending[0].mutationId).toBe('mut_draft-001');
    expect(pending[0].action).toBe('CREATE');
  });
});
