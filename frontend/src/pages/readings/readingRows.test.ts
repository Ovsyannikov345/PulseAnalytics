import { describe, expect, it } from 'vitest';

import { toReadingsPageData } from './readingRows';

const range = { from: new Date('2026-11-15T10:30:00Z'), to: new Date('2026-11-15T11:30:00Z') };

const common = { location: 'KITCHEN' as const, receivedAt: '2026-11-15T11:29:47Z' };

describe('toReadingsPageData', () => {
  it('maps each reading type to its values, keeping the order', () => {
    const data = toReadingsPageData(
      {
        __typename: 'MeterReadingConnection',
        edges: [
          {
            __typename: 'MeterReadingEdge',
            cursor: 'c1',
            node: {
              __typename: 'AirQualityReading',
              id: 'a',
              type: 'AIR_QUALITY',
              ...common,
              co2: 803,
              pm25: 25,
              humidity: 36,
            },
          },
          {
            __typename: 'MeterReadingEdge',
            cursor: 'c2',
            node: { __typename: 'EnergyReading', id: 'e', type: 'ENERGY', ...common, energy: 1.5 },
          },
          {
            __typename: 'MeterReadingEdge',
            cursor: 'c3',
            node: {
              __typename: 'MotionReading',
              id: 'm',
              type: 'MOTION',
              ...common,
              motionDetected: true,
            },
          },
        ],
        pageInfo: { __typename: 'PageInfo', hasNextPage: true, endCursor: 'c3' },
      },
      range,
    );

    expect(data).toEqual({
      rows: [
        {
          id: 'a',
          type: 'AIR_QUALITY',
          location: 'KITCHEN',
          receivedAt: new Date('2026-11-15T11:29:47Z'),
          values: { kind: 'airQuality', co2: 803, pm25: 25, humidity: 36 },
        },
        expect.objectContaining({ id: 'e', values: { kind: 'energy', energy: 1.5 } }),
        expect.objectContaining({ id: 'm', values: { kind: 'motion', motionDetected: true } }),
      ],
      hasNextPage: true,
      range,
    });
  });

  it('has no rows without a connection or edges', () => {
    expect(toReadingsPageData(null, range)).toEqual({ rows: [], hasNextPage: false, range });
    expect(
      toReadingsPageData(
        {
          __typename: 'MeterReadingConnection',
          edges: null,
          pageInfo: { __typename: 'PageInfo', hasNextPage: false, endCursor: null },
        },
        range,
      ),
    ).toEqual({ rows: [], hasNextPage: false, range });
  });
});
