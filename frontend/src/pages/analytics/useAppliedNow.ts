import { useMemo, useState } from 'react';

/** Navigation state of an applied filter: the time it was applied, taken in the event handler. */
export interface AppliedAtState {
  appliedAt: number;
}

export function appliedNowState(): AppliedAtState {
  return { appliedAt: Date.now() };
}

function appliedAtOf(state: unknown): number {
  return typeof state === 'object' &&
    state !== null &&
    'appliedAt' in state &&
    typeof state.appliedAt === 'number'
    ? state.appliedAt
    : 0;
}

/**
 * The end of preset ranges: the time the page opened, or the time a filter was applied on it (kept in
 * the history entry's state). Renders never take the time themselves, so query variables stay stable,
 * and the back button returns to the same window, which Apollo answers from its cache. Entries from
 * before the page opened (a reload, or an earlier visit) use the opening time.
 */
export function useAppliedNow(locationState: unknown): Date {
  const [openedAt] = useState(() => Date.now());
  const appliedAt = appliedAtOf(locationState);

  return useMemo(() => new Date(Math.max(openedAt, appliedAt)), [openedAt, appliedAt]);
}
