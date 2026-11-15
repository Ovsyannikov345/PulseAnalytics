import { describe, expect, it } from 'vitest';

import { toAirQualitySeries } from './airQualitySeries';

function metric(average: number) {
  return {
    __typename: 'MetricStatistics' as const,
    average,
    minimum: average - 1,
    maximum: average + 1,
  };
}

function row(bucketStart: string, samples: number, co2: number, pm25: number, humidity: number) {
  return {
    __typename: 'AirQualityStatistics' as const,
    bucketStart,
    samples,
    co2: metric(co2),
    pm25: metric(pm25),
    humidity: metric(humidity),
  };
}

const range = { from: new Date('2026-11-15T10:00:00Z'), to: new Date('2026-11-15T13:00:00Z') };

describe('toAirQualitySeries', () => {
  const series = toAirQualitySeries(
    [row('2026-11-15T10:00:00Z', 100, 600, 20, 40), row('2026-11-15T12:00:00Z', 300, 800, 10, 60)],
    range,
    'HOUR',
    'BEDROOM',
  );

  it('keeps the filters it was loaded for', () => {
    expect(series.range).toBe(range);
    expect(series.bucket).toBe('HOUR');
    expect(series.location).toBe('BEDROOM');
  });

  it('gives each metric its points on the bucket grid, with gaps', () => {
    expect(series.metrics.co2.points).toEqual([
      {
        time: Date.parse('2026-11-15T10:00:00Z'),
        stats: { average: 600, minimum: 599, maximum: 601, samples: 100 },
      },
      { time: Date.parse('2026-11-15T11:00:00Z'), stats: null },
      {
        time: Date.parse('2026-11-15T12:00:00Z'),
        stats: { average: 800, minimum: 799, maximum: 801, samples: 300 },
      },
    ]);
    expect(series.metrics.pm25.points[2]?.stats?.average).toBe(10);
    expect(series.metrics.humidity.points[0]?.stats?.maximum).toBe(41);
  });

  it('averages each metric over the range, weighted by samples', () => {
    expect(series.metrics.co2.average).toBe(750);
    expect(series.metrics.pm25.average).toBe(12.5);
    expect(series.metrics.humidity.average).toBe(55);
  });

  it('has no average without samples', () => {
    const empty = toAirQualitySeries([], range, 'HOUR', null);

    expect(empty.metrics.co2.average).toBeNull();
    expect(empty.metrics.co2.points.every((point) => point.stats === null)).toBe(true);
  });
});
