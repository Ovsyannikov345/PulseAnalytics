import { useEffect, useEffectEvent, useState } from 'react';

/** How often the Overview refreshes while the tab is visible (decision 5 in FRONTEND_PLAN.md). */
export const REFRESH_INTERVAL_MS = 30_000;

/**
 * The Overview's clock: the time of the page's last refresh. It starts when the page opens and moves
 * every 30 s while the tab is visible; a tab that becomes visible again after missing a refresh
 * refreshes at once. A hidden tab makes no requests.
 *
 * Each refresh first refetches the latest readings and only then moves `now`, which the time ranges
 * of the other widgets follow. So the stale check (newest sample against `now`) never compares samples
 * from before a long hidden period with the current time. A failed refetch still moves `now`: the
 * latest readings' widget shows its error, and the other widgets refresh as usual.
 */
export function useOverviewRefresh(refetchLatest: () => Promise<unknown>): Date {
  const [now, setNow] = useState(() => new Date());
  const refetch = useEffectEvent(refetchLatest);

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    let lastRefreshMs = Date.now();
    let refreshing = false;
    let disposed = false;

    const schedule = (delayMs: number) => {
      clearTimeout(timer);
      timer = setTimeout(() => void refresh(), delayMs);
    };

    async function refresh() {
      // A hidden tab waits; becoming visible schedules the missed refresh.
      if (document.visibilityState !== 'visible') {
        return;
      }

      refreshing = true;
      const startedAt = new Date();
      try {
        await refetch();
      } catch {
        // The latest readings' widget shows the error.
      }
      refreshing = false;

      if (!disposed) {
        lastRefreshMs = startedAt.getTime();
        setNow(startedAt);
        schedule(REFRESH_INTERVAL_MS);
      }
    }

    const onVisibilityChange = () => {
      if (document.visibilityState === 'visible' && !refreshing) {
        schedule(Math.max(0, lastRefreshMs + REFRESH_INTERVAL_MS - Date.now()));
      }
    };

    schedule(REFRESH_INTERVAL_MS);
    document.addEventListener('visibilitychange', onVisibilityChange);

    return () => {
      disposed = true;
      clearTimeout(timer);
      document.removeEventListener('visibilitychange', onVisibilityChange);
    };
  }, []);

  return now;
}
