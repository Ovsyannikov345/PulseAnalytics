import { useEffect, useEffectEvent, useState } from 'react';

export const REFRESH_INTERVAL_MS = 30_000;

/**
 * The time of the last refresh, every 30 s while the tab is visible. The latest readings are refetched
 * before `now` moves, so the stale check never compares old samples with a new time.
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
      if (document.visibilityState !== 'visible') {
        return;
      }

      refreshing = true;
      const startedAt = new Date();
      try {
        await refetch();
      } catch {
        // Shown by the latest readings' widget.
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
