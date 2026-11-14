import { describe, expect, it } from 'vitest';

import {
  MAX_EXPORT_RANGE_MS,
  MAX_STATISTICS_RANGE_MS,
  checkRange,
  isRangePreset,
  presetRange,
  rangeDurationMs,
  rangePresetLabel,
  rangePresets,
  type TimeRange,
} from './timeRanges';

const HOUR_MS = 3_600_000;
const DAY_MS = 24 * HOUR_MS;

function rangeOf(durationMs: number): TimeRange {
  const from = new Date('2026-11-01T00:00:00Z');
  return { from, to: new Date(from.getTime() + durationMs) };
}

describe('range presets', () => {
  it('offers 1 h, 6 h, 24 h, 7 d and 31 d', () => {
    expect(rangePresets).toEqual(['1h', '6h', '24h', '7d', '31d']);
    expect(rangePresets.map(rangePresetLabel)).toEqual(['1 h', '6 h', '24 h', '7 d', '31 d']);
  });

  it.each([
    ['1h', HOUR_MS],
    ['6h', 6 * HOUR_MS],
    ['24h', DAY_MS],
    ['7d', 7 * DAY_MS],
    ['31d', 31 * DAY_MS],
  ] as const)('makes %s end at now', (preset, durationMs) => {
    const now = new Date('2026-11-14T12:34:56.789Z');

    const range = presetRange(preset, now);

    expect(range.to).toBe(now);
    expect(rangeDurationMs(range)).toBe(durationMs);
  });

  it.each([
    ['24h', true],
    ['31d', true],
    ['2h', false],
    ['', false],
    ['toString', false],
  ])('recognizes %j as a preset: %s', (value, expected) => {
    expect(isRangePreset(value)).toBe(expected);
  });
});

describe('checkRange', () => {
  it.each([
    ['statistics', MAX_STATISTICS_RANGE_MS, 31 * DAY_MS],
    ['the export', MAX_EXPORT_RANGE_MS, 7 * DAY_MS],
  ])('accepts %s up to exactly its limit and no more', (_, maxMs, expectedMaxMs) => {
    expect(maxMs).toBe(expectedMaxMs);
    expect(checkRange(rangeOf(maxMs), maxMs)).toBeNull();
    expect(checkRange(rangeOf(maxMs + 1), maxMs)).toBe('tooLong');
  });

  it('accepts a range of one millisecond', () => {
    expect(checkRange(rangeOf(1), MAX_STATISTICS_RANGE_MS)).toBeNull();
  });

  it.each([
    ['from equals to', 0],
    ['from is after to', -HOUR_MS],
  ])('rejects a range where %s', (_, durationMs) => {
    expect(checkRange(rangeOf(durationMs), MAX_STATISTICS_RANGE_MS)).toBe('notIncreasing');
  });

  it('rejects an invalid date', () => {
    const range = { from: new Date(''), to: new Date('2026-11-14T00:00:00Z') };

    expect(checkRange(range, MAX_STATISTICS_RANGE_MS)).toBe('notIncreasing');
  });
});
