import { useEffect, useState } from 'react';

export const CLOCK_INTERVAL_MS = 10_000;

/** The current time, moved every 10 s: often enough for the 2-minute stale check. */
export function useNow(): Date {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = setInterval(() => {
      setNow(new Date());
    }, CLOCK_INTERVAL_MS);

    return () => {
      clearInterval(timer);
    };
  }, []);

  return now;
}
