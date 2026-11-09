// mobile/src/offline/syncGuard.ts
// Enforces 7-day offline lock (NFR-MOB-004, TS-29) and 500-item queue cap (D-015, TS-30)

import type { OfflineDeviceState } from './types';

export const SEVEN_DAYS_MS = 7 * 24 * 60 * 60 * 1000;
export const MAX_OFFLINE_QUEUE_ITEMS = 500;

export class SyncGuard {
  /**
   * Asserts whether the device is permitted to capture new offline expenses.
   * Throws an error if offline window exceeded or queue is full.
   */
  public static canCaptureOffline(state: OfflineDeviceState, currentTimestamp: number = Date.now()): {
    allowed: boolean;
    reason?: 'SEVEN_DAY_WINDOW_EXCEEDED' | 'QUEUE_CAP_REACHED';
  } {
    // 1. Check 7-day hard offline ceiling (NFR-MOB-004, ADR-048)
    if (state.lastServerSyncTimestamp > 0) {
      const offlineDuration = currentTimestamp - state.lastServerSyncTimestamp;
      if (offlineDuration > SEVEN_DAYS_MS) {
        return { allowed: false, reason: 'SEVEN_DAY_WINDOW_EXCEEDED' };
      }
    }

    // 2. Check 500 queued items limit (D-015)
    if (state.offlineQueueCount >= MAX_OFFLINE_QUEUE_ITEMS) {
      return { allowed: false, reason: 'QUEUE_CAP_REACHED' };
    }

    return { allowed: true };
  }
}
