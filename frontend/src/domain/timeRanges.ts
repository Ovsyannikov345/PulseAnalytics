const HOUR_MS = 60 * 60 * 1000;
const DAY_MS = 24 * HOUR_MS;

/** A time range from `from` (inclusive) to `to` (exclusive), as the gateway's filters take it. */
export interface TimeRange {
  from: Date;
  to: Date;
}

/** The gateway's limit for statistics (`StatisticsLimits`). */
export const MAX_STATISTICS_RANGE_MS = 31 * DAY_MS;

/** The gateway's limit for the CSV export. */
export const MAX_EXPORT_RANGE_MS = 7 * DAY_MS;

/** Preset ranges ending now; the ids are also the `range` values in the URL. */
export type RangePreset = '1h' | '6h' | '24h' | '7d' | '31d';

const presetDefinitions: Record<RangePreset, { label: string; durationMs: number }> = {
  '1h': { label: '1 h', durationMs: HOUR_MS },
  '6h': { label: '6 h', durationMs: 6 * HOUR_MS },
  '24h': { label: '24 h', durationMs: DAY_MS },
  '7d': { label: '7 d', durationMs: 7 * DAY_MS },
  '31d': { label: '31 d', durationMs: 31 * DAY_MS },
};

export const rangePresets = Object.keys(presetDefinitions) as readonly RangePreset[];

export function isRangePreset(value: string): value is RangePreset {
  return Object.hasOwn(presetDefinitions, value);
}

export function rangePresetLabel(preset: RangePreset): string {
  return presetDefinitions[preset].label;
}

/** The preset's range ending at `now`; `now` is taken once per applied filter, not on every render. */
export function presetRange(preset: RangePreset, now: Date): TimeRange {
  return { from: new Date(now.getTime() - presetDefinitions[preset].durationMs), to: now };
}

export function rangeDurationMs(range: TimeRange): number {
  return range.to.getTime() - range.from.getTime();
}

export type RangeProblem = 'notIncreasing' | 'tooLong';

/** Checks a range against the gateway's rules: `from < to` and at most `maxDurationMs` long. */
export function checkRange(range: TimeRange, maxDurationMs: number): RangeProblem | null {
  const durationMs = rangeDurationMs(range);

  // NaN: an invalid date, e.g. from an empty input.
  if (Number.isNaN(durationMs) || durationMs <= 0) {
    return 'notIncreasing';
  }

  return durationMs > maxDurationMs ? 'tooLong' : null;
}
