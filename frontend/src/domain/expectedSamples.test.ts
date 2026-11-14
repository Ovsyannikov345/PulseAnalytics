import { describe, expect, it } from 'vitest';

import { POLL_PERIOD_MS, expectedSamples, expectedSamplesInBucket } from './expectedSamples';
import { METER_COUNT, METERS_PER_TYPE } from './meters';
import { presetRange, type TimeRange } from './timeRanges';

function range(from: string, to: string): TimeRange {
  return { from: new Date(from), to: new Date(to) };
}

describe('expectedSamples', () => {
  it('assumes one poll every 15 s', () => {
    expect(POLL_PERIOD_MS).toBe(15_000);
  });

  it('expects 240 polls per hour and meter', () => {
    expect(expectedSamples(range('2026-11-14T10:00:00Z', '2026-11-14T11:00:00Z'), 1)).toBe(240);
  });

  it('expects 103,680 samples of all 18 meters in 24 hours', () => {
    const last24Hours = presetRange('24h', new Date('2026-11-14T11:29:47Z'));

    expect(expectedSamples(last24Hours, METER_COUNT)).toBe(103_680);
  });

  it('counts a partial poll period as a fraction', () => {
    expect(expectedSamples(range('2026-11-14T10:00:00Z', '2026-11-14T10:00:07.5Z'), 2)).toBe(1);
  });

  it('expects nothing for an empty or reversed range', () => {
    expect(expectedSamples(range('2026-11-14T10:00:00Z', '2026-11-14T10:00:00Z'), 18)).toBe(0);
    expect(expectedSamples(range('2026-11-14T11:00:00Z', '2026-11-14T10:00:00Z'), 18)).toBe(0);
  });
});

describe('expectedSamplesInBucket', () => {
  // The Overview's coverage chart: motion samples per hour over the last 24 hours.
  const last24Hours = range('2026-11-13T11:29:47Z', '2026-11-14T11:29:47Z');

  it('expects 240 polls × 6 motion meters in a whole hour', () => {
    const start = new Date('2026-11-13T15:00:00Z');

    expect(expectedSamplesInBucket(start, 'HOUR', last24Hours, METERS_PER_TYPE)).toBe(1440);
  });

  it('scales the first and last hour to the part inside the range', () => {
    const first = new Date('2026-11-13T11:00:00Z');
    const last = new Date('2026-11-14T11:00:00Z');

    // 30 min 13 s and 29 min 47 s of polls: 120.87 and 119.13 polls per meter.
    expect(expectedSamplesInBucket(first, 'HOUR', last24Hours, METERS_PER_TYPE)).toBeCloseTo(725.2);
    expect(expectedSamplesInBucket(last, 'HOUR', last24Hours, METERS_PER_TYPE)).toBeCloseTo(714.8);
  });

  it('expects nothing for a bucket outside the range', () => {
    const start = new Date('2026-11-14T12:00:00Z');

    expect(expectedSamplesInBucket(start, 'HOUR', last24Hours, METERS_PER_TYPE)).toBe(0);
  });
});
