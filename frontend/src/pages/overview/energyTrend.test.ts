import { describe, expect, it } from 'vitest';

import { describeEnergyTrend, type EnergyTrendPoint, toEnergyTrend } from './energyTrend';

function row(bucketStart: string, average: number) {
  return {
    __typename: 'EnergyStatistics' as const,
    bucketStart,
    samples: 60,
    energy: { __typename: 'MetricStatistics' as const, average, minimum: 10, maximum: 990 },
  };
}

// 10:20–11:05 touches the quarter hours starting 10:15, 10:30, 10:45 and 11:00.
const range = { from: new Date('2026-11-15T10:20:00Z'), to: new Date('2026-11-15T11:05:00Z') };

describe('toEnergyTrend', () => {
  it('puts the averages on the bucket grid and keeps the details for the tooltip', () => {
    const points = toEnergyTrend(
      [row('2026-11-15T10:15:00Z', 400), row('2026-11-15T11:00:00Z', 450.5)],
      range,
      'FIFTEEN_MINUTES',
    );

    expect(points).toEqual([
      {
        time: Date.parse('2026-11-15T10:15:00Z'),
        value: 400,
        bucket: { average: 400, minimum: 10, maximum: 990, samples: 60 },
      },
      { time: Date.parse('2026-11-15T10:30:00Z'), value: null, bucket: null },
      { time: Date.parse('2026-11-15T10:45:00Z'), value: null, bucket: null },
      {
        time: Date.parse('2026-11-15T11:00:00Z'),
        value: 450.5,
        bucket: { average: 450.5, minimum: 10, maximum: 990, samples: 60 },
      },
    ]);
  });

  it('is all gaps without rows', () => {
    const points = toEnergyTrend([], range, 'FIFTEEN_MINUTES');

    expect(points).toHaveLength(4);
    expect(points.every((point) => point.value === null && point.bucket === null)).toBe(true);
  });
});

describe('describeEnergyTrend', () => {
  const quarterHour = 15 * 60_000;

  function points(values: (number | null)[]): EnergyTrendPoint[] {
    return values.map((value, index) => ({
      time: index * quarterHour,
      value,
      bucket: value === null ? null : { average: value, minimum: 0, maximum: 0, samples: 1 },
    }));
  }

  it('names the lowest and highest average', () => {
    expect(describeEnergyTrend(points([400, 512.345, 455]))).toBe(
      'Average energy of all locations per 15 minutes over the last 24 hours, from 400.00 to 512.35.',
    );
  });

  it('counts one span without samples', () => {
    expect(describeEnergyTrend(points([400, null, null, 455]))).toMatch(
      /to 455\.00\. No samples in 1 period\.$/,
    );
  });

  it('counts several spans without samples', () => {
    expect(describeEnergyTrend(points([null, 400, null, 455]))).toMatch(
      /No samples in 2 periods\.$/,
    );
  });
});
