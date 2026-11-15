/**
 * The ingestor stores samples every 15 s, so newer ones are always expected. When the newest is older
 * than this, WeakApp (or the pipeline behind it) has stopped delivering. A poll returns all meters or
 * none, so freshness is judged once for all meters, not per meter.
 */
export const STALE_AFTER_MS = 2 * 60_000;

/** Whether the newest sample, checked at `now`, is older than `STALE_AFTER_MS`. */
export function isStale(latestReceivedAt: Date, now: Date): boolean {
  return now.getTime() - latestReceivedAt.getTime() > STALE_AFTER_MS;
}
