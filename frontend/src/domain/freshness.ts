export const STALE_AFTER_MS = 2 * 60_000;

export function isStale(latestReceivedAt: Date, now: Date): boolean {
  return now.getTime() - latestReceivedAt.getTime() > STALE_AFTER_MS;
}
