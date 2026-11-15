import { describe, expect, it } from 'vitest';

import {
  type CoverageHour,
  lowCoverageHour,
  overallShare,
  toIngestionCoverage,
} from './ingestionCoverage';

function row(bucketStart: string, samples: number) {
  return { __typename: 'MotionStatistics' as const, bucketStart, samples };
}

// 6 meters at one poll per 15 s: 1,440 samples in a full hour.
const meterCount = 6;

describe('toIngestionCoverage', () => {
  // 10:30–13:30: half hours at both ends, two full hours between.
  const range = { from: new Date('2026-11-15T10:30:00Z'), to: new Date('2026-11-15T13:30:00Z') };

  it('divides the stored samples by those expected in the part of each hour inside the range', () => {
    const hours = toIngestionCoverage(
      [
        row('2026-11-15T10:00:00Z', 540),
        row('2026-11-15T11:00:00Z', 1109),
        row('2026-11-15T13:00:00Z', 720),
      ],
      range,
      'HOUR',
      meterCount,
    );

    expect(hours).toEqual([
      {
        span: { from: new Date('2026-11-15T10:30:00Z'), to: new Date('2026-11-15T11:00:00Z') },
        samples: 540,
        expected: 720,
        share: 0.75,
        full: false,
      },
      {
        span: { from: new Date('2026-11-15T11:00:00Z'), to: new Date('2026-11-15T12:00:00Z') },
        samples: 1109,
        expected: 1440,
        share: 1109 / 1440,
        full: true,
      },
      {
        span: { from: new Date('2026-11-15T12:00:00Z'), to: new Date('2026-11-15T13:00:00Z') },
        samples: 0,
        expected: 1440,
        share: 0,
        full: true,
      },
      {
        span: { from: new Date('2026-11-15T13:00:00Z'), to: new Date('2026-11-15T13:30:00Z') },
        samples: 720,
        expected: 720,
        share: 1,
        full: false,
      },
    ]);
  });

  it('caps the share at 100 %', () => {
    const [first] = toIngestionCoverage(
      [row('2026-11-15T10:00:00Z', 800)],
      range,
      'HOUR',
      meterCount,
    );

    expect(first?.share).toBe(1);
  });

  it('leaves out a partial hour with less than a minute inside the range', () => {
    const hours = toIngestionCoverage(
      [],
      { from: new Date('2026-11-15T10:59:01Z'), to: new Date('2026-11-15T12:00:30Z') },
      'HOUR',
      meterCount,
    );

    expect(hours.map((hour) => hour.span.from.toISOString())).toEqual(['2026-11-15T11:00:00.000Z']);
  });

  it('keeps a partial hour with exactly a minute inside the range', () => {
    const hours = toIngestionCoverage(
      [],
      { from: new Date('2026-11-15T10:59:00Z'), to: new Date('2026-11-15T12:00:00Z') },
      'HOUR',
      meterCount,
    );

    expect(hours).toHaveLength(2);
    expect(hours[0]).toMatchObject({ expected: 24, full: false });
  });
});

function hour(start: string, share: number, full = true): CoverageHour {
  const from = new Date(start);
  return {
    span: { from, to: new Date(from.getTime() + 3_600_000) },
    samples: share * 1440,
    expected: 1440,
    share,
    full,
  };
}

describe('lowCoverageHour', () => {
  it('is the full hour with the lowest share below 50 %', () => {
    const lowest = hour('2026-11-15T05:00:00Z', 0.41);

    expect(
      lowCoverageHour([
        hour('2026-11-15T04:00:00Z', 0.77),
        lowest,
        hour('2026-11-15T06:00:00Z', 0.45),
      ]),
    ).toBe(lowest);
  });

  it('ignores partial hours, whose share says little', () => {
    expect(lowCoverageHour([hour('2026-11-15T04:00:00Z', 0.1, false)])).toBeNull();
  });

  it('is none when every full hour has at least half', () => {
    expect(lowCoverageHour([hour('2026-11-15T04:00:00Z', 0.5)])).toBeNull();
  });
});

describe('overallShare', () => {
  it('weighs each hour by its expected samples', () => {
    const hours = [
      hour('2026-11-15T04:00:00Z', 1),
      { ...hour('2026-11-15T05:00:00Z', 0, false), expected: 720 },
    ];

    expect(overallShare(hours)).toBeCloseTo(1440 / 2160, 9);
  });

  it('is 0 without hours', () => {
    expect(overallShare([])).toBe(0);
  });
});
