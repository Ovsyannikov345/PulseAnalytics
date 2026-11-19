import { describe, expect, it } from 'vitest';

import {
  type AirQualityLiveReading,
  type EnergyLiveReading,
  type LiveReading,
  meterKey,
  mergeReadings,
  type MotionLiveReading,
  newestReceivedAt,
  noLiveReadings,
  toRoomViews,
} from './liveReadings';

function energy(id: string, receivedAt: string, value: number): EnergyLiveReading {
  return { type: 'ENERGY', id, location: 'OFFICE', receivedAt, energy: value };
}

const motion: MotionLiveReading = {
  type: 'MOTION',
  id: 'm-1',
  location: 'GARAGE',
  receivedAt: '2026-11-17T10:15:30.123+00:00',
  motionDetected: true,
};

const airQuality: AirQualityLiveReading = {
  type: 'AIR_QUALITY',
  id: 'a-1',
  location: 'LIVING_ROOM',
  receivedAt: '2026-11-17T10:15:30.123+00:00',
  co2: 803,
  pm25: 25,
  humidity: 36,
};

const older = energy('e-1', '2026-11-17T10:15:15.000+00:00', 100);
const newer = energy('e-2', '2026-11-17T10:15:30.000+00:00', 200);

function merge(...batches: LiveReading[][]) {
  return batches.reduce(mergeReadings, noLiveReadings);
}

describe('mergeReadings', () => {
  it('keeps one reading per meter', () => {
    const merged = merge([older, motion, airQuality]);

    expect([...merged.keys()]).toEqual([
      meterKey('ENERGY', 'OFFICE'),
      meterKey('MOTION', 'GARAGE'),
      meterKey('AIR_QUALITY', 'LIVING_ROOM'),
    ]);
  });

  it('keeps the meters apart by type and location', () => {
    const kitchen = { ...older, id: 'e-3', location: 'KITCHEN' } as const;

    expect(merge([older, kitchen]).size).toBe(2);
  });

  it.each([
    ['query then push', [older], [newer]],
    ['push then query', [newer], [older]],
    ['both in one batch', [older, newer], []],
    ['both in one batch, newer first', [newer, older], []],
  ])('keeps the newer reading: %s', (_, first, second) => {
    expect(merge(first, second).get(meterKey('ENERGY', 'OFFICE'))).toBe(newer);
  });

  it('ignores the same reading again and returns the same map', () => {
    const merged = merge([newer]);

    expect(mergeReadings(merged, [{ ...newer }])).toBe(merged);
  });

  it('returns the same map when only older readings arrive', () => {
    const merged = merge([newer]);

    expect(mergeReadings(merged, [older])).toBe(merged);
  });

  it('returns the same map for nothing', () => {
    expect(mergeReadings(noLiveReadings, [])).toBe(noLiveReadings);
  });

  it('compares instants, not the offsets they are written with', () => {
    const sameInstant = energy('e-3', '2026-11-17T13:15:30.000+03:00', 300);

    expect(merge([newer], [sameInstant]).get(meterKey('ENERGY', 'OFFICE'))).toBe(newer);
  });

  it('accepts a later instant written with a smaller clock time', () => {
    const later = energy('e-3', '2026-11-17T07:15:31.000-03:00', 300);

    expect(merge([newer], [later]).get(meterKey('ENERGY', 'OFFICE'))).toBe(later);
  });

  it('leaves the current map unchanged', () => {
    const merged = merge([older]);

    mergeReadings(merged, [newer]);

    expect(merged.get(meterKey('ENERGY', 'OFFICE'))).toBe(older);
  });
});

describe('toRoomViews', () => {
  it('lists every room in the gateway order, without values before their first sample', () => {
    const rooms = toRoomViews(noLiveReadings);

    expect(rooms.map((room) => room.location)).toEqual([
      'KITCHEN',
      'LIVING_ROOM',
      'BEDROOM',
      'GARAGE',
      'OFFICE',
      'CORRIDOR',
    ]);
    expect(rooms[0]).toEqual({
      location: 'KITCHEN',
      energy: null,
      airQuality: null,
      motionDetected: null,
    });
  });

  it('puts each meter value in its room', () => {
    const rooms = toRoomViews(merge([newer, motion, airQuality]));

    expect(rooms.find((room) => room.location === 'OFFICE')?.energy).toBe(200);
    expect(rooms.find((room) => room.location === 'GARAGE')?.motionDetected).toBe(true);
    expect(rooms.find((room) => room.location === 'LIVING_ROOM')?.airQuality).toEqual({
      co2: 803,
      pm25: 25,
      humidity: 36,
    });
  });
});

describe('newestReceivedAt', () => {
  it('is null without readings', () => {
    expect(newestReceivedAt(noLiveReadings)).toBeNull();
  });

  it('is the newest instant over all meters', () => {
    const early = { ...motion, receivedAt: '2026-11-17T13:16:00.000+03:00' };

    expect(newestReceivedAt(merge([airQuality, early, older]))).toEqual(
      new Date('2026-11-17T10:16:00.000Z'),
    );
  });
});
